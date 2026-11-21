/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 *
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services.Networking;
using Launcher.Application.Services;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Home;

/// <summary>
/// 游戏运行态：进程拉起后首页切换成「网速 + 已运行时长 + 停止游戏」，进程退出后自动复位。
/// 与原启动进度共用同一张启动卡片，避免出现两个互相打断的状态面板。
/// </summary>
public sealed partial class HomePageViewModel
{
    private readonly NetworkThroughputSampler networkSampler = new();
    private DispatcherTimer? gameRunTimer;
    private int runningProcessId;
    private DateTimeOffset gameRunStartedAt;
    private bool gameRunWatchActive;

    [ObservableProperty]
    private bool isGameRunning;

    [ObservableProperty]
    private string gameNetworkSpeedText = string.Empty;

    [ObservableProperty]
    private string gameElapsedText = string.Empty;

    [ObservableProperty]
    private string gameRunInstanceName = string.Empty;

    /// <summary>运行态下启动按钮让位给停止按钮。</summary>
    public bool IsGameIdle => !IsGameRunning;

    public bool HasGameNetworkSpeed => IsGameRunning && !string.IsNullOrWhiteSpace(GameNetworkSpeedText);

    partial void OnIsGameRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGameIdle));
        OnPropertyChanged(nameof(HasGameNetworkSpeed));
        OnPropertyChanged(nameof(CanLaunchSelectedGame));
        LaunchCommand.NotifyCanExecuteChanged();
        StopGameCommand.NotifyCanExecuteChanged();
    }

    partial void OnGameNetworkSpeedTextChanged(string value) => OnPropertyChanged(nameof(HasGameNetworkSpeed));

    private void BeginGameRunWatch(GameLaunchSession session)
    {
        runningProcessId = session.ProcessId;
        gameRunStartedAt = DateTimeOffset.Now;
        GameRunInstanceName = session.InstanceName;
        GameElapsedText = "00:00";
        GameNetworkSpeedText = string.Empty;
        networkSampler.Reset();
        IsGameRunning = true;
        gameRunWatchActive = true;

        EnsureGameRunTimer();
        gameRunTimer?.Start();
        UpdateGameRunSnapshot();

        // 进程被玩家自己关掉时（不是点停止按钮），同样要回到待启动状态。
        _ = ObserveSessionExitAsync(session);
    }

    private async Task ObserveSessionExitAsync(GameLaunchSession session)
    {
        try
        {
            await session.ExitTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Game session exit task faulted. InstanceId={InstanceId}", session.InstanceId);
        }
        finally
        {
            uiDispatcher.Post(() =>
            {
                // 只有当前这次运行还没被停止按钮结束时才复位，避免重复清理打断新一轮启动。
                if (gameRunWatchActive)
                    EndGameRunWatch();
            });
        }
    }

    private void EnsureGameRunTimer()
    {
        if (gameRunTimer is not null)
            return;

        gameRunTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        gameRunTimer.Tick += (_, _) => UpdateGameRunSnapshot();
    }

    private void UpdateGameRunSnapshot()
    {
        if (!gameRunWatchActive)
            return;

        var elapsed = DateTimeOffset.Now - gameRunStartedAt;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        GameElapsedText = elapsed.TotalHours >= 1d
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";

        var throughput = networkSampler.Sample();
        GameNetworkSpeedText = throughput.IsZero
            ? Strings.Home_GameNetworkIdle
            : string.Format(
                Strings.Home_GameNetworkFormat,
                FormatRate(throughput.ReceivedBytesPerSecond),
                FormatRate(throughput.SentBytesPerSecond));
    }

    internal static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond < 0d)
            bytesPerSecond = 0d;

        if (bytesPerSecond >= 1024d * 1024d)
            return string.Format("{0:0.0} MB/s", bytesPerSecond / (1024d * 1024d));

        if (bytesPerSecond >= 1024d)
            return string.Format("{0:0} KB/s", bytesPerSecond / 1024d);

        return string.Format("{0:0} B/s", bytesPerSecond);
    }

    [RelayCommand(CanExecute = nameof(IsGameRunning))]
    private async Task StopGameAsync()
    {
        var processId = runningProcessId;
        if (processId <= 0)
        {
            EndGameRunWatch();
            return;
        }

        statusService.Report(Strings.Status_GameStopping);

        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            floatingMessageService.Show(Strings.Status_GameStopped);
        }
        catch (ArgumentException)
        {
            // 进程已经自行退出，属于正常路径。
        }
        catch (InvalidOperationException)
        {
            // 进程句柄在结束前失效，同样视为已退出。
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to stop Minecraft process. ProcessId={ProcessId}", processId);
            floatingMessageService.Show(Strings.Status_StopGameFailed);
        }
        finally
        {
            EndGameRunWatch();
        }
    }

    private void EndGameRunWatch()
    {
        if (!gameRunWatchActive && !IsGameRunning)
            return;

        gameRunWatchActive = false;
        gameRunTimer?.Stop();
        runningProcessId = 0;
        GameNetworkSpeedText = string.Empty;
        GameElapsedText = string.Empty;
        GameRunInstanceName = string.Empty;
        networkSampler.Reset();
        IsGameRunning = false;
    }
}
