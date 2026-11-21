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

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.Services.Ai;
using Launcher.Application.Services;
using Launcher.Application.Services.Ai;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Ai;

/// <summary>
/// AI 助手页面。聊天状态和输入在这里，接口配置在 <c>AiAssistantPageViewModel.Settings.cs</c>。
/// 真正的请求编排交给 Application 的 <see cref="AiAssistantService"/>，ViewModel 不碰网络。
/// </summary>
public sealed partial class AiAssistantPageViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// 回传给模型的历史条数。太长会让首字延迟明显变高，也不利于成本控制。
    /// </summary>
    private const int MaxHistoryMessages = 16;

    private readonly ISettingsService settingsService;
    private readonly AiAssistantService assistantService;
    private readonly IStatusService statusService;
    private readonly IClipboardService clipboardService;
    private readonly ILogger<AiAssistantPageViewModel> logger;
    private CancellationTokenSource? pendingRequest;
    private LauncherSettings currentSettings = new();
    private bool hasLoadedSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private string inputText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private bool isBusy;

    [ObservableProperty]
    private bool isSettingsOpen;

    [ObservableProperty]
    private bool hasMessages;

    [ObservableProperty]
    private bool isOnlineMode;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string headerSubtitle = string.Empty;

    public AiAssistantPageViewModel(
        ISettingsService settingsService,
        AiAssistantService assistantService,
        IStatusService statusService,
        IClipboardService clipboardService,
        ILogger<AiAssistantPageViewModel>? logger = null)
    {
        this.settingsService = settingsService;
        this.assistantService = assistantService;
        this.statusService = statusService;
        this.clipboardService = clipboardService;
        this.logger = logger ?? NullLogger<AiAssistantPageViewModel>.Instance;

        Suggestions = assistantService.Suggestions;
        Messages.CollectionChanged += Messages_CollectionChanged;
        ResetConversation();
        UpdateModeIndicators();
    }

    public ObservableCollection<AiChatEntryViewModel> Messages { get; } = [];

    public IReadOnlyList<string> Suggestions { get; }

    public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    /// <summary>
    /// 首次进入页面时加载设置，决定显示在线还是离线状态。
    /// </summary>
    public async Task PrimeAsync()
    {
        if (hasLoadedSettings)
            return;

        try
        {
            currentSettings = await settingsService.LoadAsync().ConfigureAwait(true);
            hasLoadedSettings = true;
            LoadSettingsIntoEditor();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load settings for the AI assistant page.");
        }

        UpdateModeIndicators();
        statusService.Report(Strings.Ai_StatusReady);
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var question = InputText?.Trim() ?? string.Empty;
        if (question.Length == 0)
            return;

        if (IsBusy)
        {
            statusService.Report(Strings.Ai_StatusBusy);
            return;
        }

        if (question.Length > 4000)
        {
            statusService.Report(Strings.Ai_StatusTooLong);
            return;
        }

        InputText = string.Empty;
        Messages.Add(new AiChatEntryViewModel(question, isUser: true));
        UpdateModeIndicators();

        var pending = new AiChatEntryViewModel(Strings.Ai_Thinking, isUser: false) { IsPending = true };
        Messages.Add(pending);

        pendingRequest?.Dispose();
        pendingRequest = new CancellationTokenSource();
        IsBusy = true;

        var history = BuildHistory();
        try
        {
            var reply = await assistantService
                .AskAsync(BuildOptions(), history, question, pendingRequest.Token)
                .ConfigureAwait(true);

            Messages.Remove(pending);

            if (reply.IsSuccess)
            {
                Messages.Add(new AiChatEntryViewModel(reply.Content, isUser: false, isOffline: reply.IsOffline));
                if (reply.IsOffline && reply.FailureCode is not null)
                    statusService.Report(DescribeFailure(reply.FailureCode));
                else if (reply.IsOffline)
                    statusService.Report(Strings.Ai_StatusOfflineAnswer);
                else
                    statusService.Report(Strings.Ai_StatusAnswerReady);
            }
            else
            {
                Messages.Add(new AiChatEntryViewModel(DescribeFailure(reply.FailureCode), isUser: false, isOffline: true));
                statusService.Report(Strings.Ai_StatusFailed);
            }
        }
        catch (OperationCanceledException)
        {
            Messages.Remove(pending);
            Messages.Add(new AiChatEntryViewModel(Strings.Ai_Cancelled, isUser: false, isOffline: true));
            statusService.Report(Strings.Ai_StatusCancelled);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while asking the AI assistant.");
            Messages.Remove(pending);
            Messages.Add(new AiChatEntryViewModel(Strings.Ai_ErrorUnexpected, isUser: false, isOffline: true));
            statusService.Report(Strings.Ai_StatusFailed);
        }
        finally
        {
            IsBusy = false;
            UpdateModeIndicators();
        }
    }

    [RelayCommand]
    private void Stop()
    {
        pendingRequest?.Cancel();
    }

    [RelayCommand]
    private void Clear()
    {
        pendingRequest?.Cancel();
        ResetConversation();
        statusService.Report(Strings.Ai_StatusCleared);
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (IsSettingsOpen)
            LoadSettingsIntoEditor();
    }

    [RelayCommand]
    private async Task UseSuggestionAsync(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion))
            return;

        InputText = suggestion;
        await SendAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CopyMessageAsync(AiChatEntryViewModel? entry)
    {
        if (entry is null || entry.IsPending)
            return;

        var copied = await clipboardService.CopyTextAsync(entry.Content).ConfigureAwait(true);
        statusService.Report(copied ? Strings.Ai_StatusCopied : Strings.Ai_StatusCopyFailed);
    }

    /// <summary>
    /// 页面级的上下文提示，让模型知道用户在用哪个启动器版本、哪个目录。
    /// </summary>
    internal AiChatOptions BuildOptions()
    {
        return new AiChatOptions(
            currentSettings.AiAssistantEndpoint,
            currentSettings.AiAssistantApiKey,
            currentSettings.AiAssistantModel,
            AiAssistantSystemPrompt.Build(currentSettings));
    }

    private List<AiChatMessage> BuildHistory()
    {
        var history = new List<AiChatMessage>(MaxHistoryMessages);

        // 索引 0 是欢迎语，不属于对话内容；回传给模型只会让上下文变脏。
        for (var i = 1; i < Messages.Count; i++)
        {
            var entry = Messages[i];
            if (entry.IsPending)
                continue;

            history.Add(new AiChatMessage(
                entry.IsUser ? AiChatRole.User : AiChatRole.Assistant,
                entry.Content));
        }

        if (history.Count > MaxHistoryMessages)
            history.RemoveRange(0, history.Count - MaxHistoryMessages);

        return history;
    }

    private void ResetConversation()
    {
        Messages.Clear();
        Messages.Add(new AiChatEntryViewModel(assistantService.Greeting, isUser: false));
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasMessages = Messages.Count > 1;
    }

    private void UpdateModeIndicators()
    {
        var options = BuildOptions();
        IsOnlineMode = options.IsConfigured;
        StatusText = IsOnlineMode
            ? string.Format(Strings.Ai_StatusOnlineFormat, currentSettings.AiAssistantModel)
            : Strings.Ai_StatusOfflineMode;
        HeaderSubtitle = IsOnlineMode ? Strings.Ai_SubtitleOnline : Strings.Ai_SubtitleOffline;
    }

    private static string DescribeFailure(string? code) =>
        code switch
        {
            AiFailureCodes.Unauthorized => Strings.Ai_ErrorUnauthorized,
            AiFailureCodes.RateLimited => Strings.Ai_ErrorRateLimited,
            AiFailureCodes.ModelNotFound => Strings.Ai_ErrorModelNotFound,
            AiFailureCodes.InvalidEndpoint => Strings.Ai_ErrorInvalidEndpoint,
            AiFailureCodes.EmptyResponse => Strings.Ai_ErrorEmptyResponse,
            AiFailureCodes.TooLong => Strings.Ai_ErrorTooLong,
            AiFailureCodes.Network => Strings.Ai_ErrorNetwork,
            AiFailureCodes.NotConfigured => Strings.Ai_ErrorNotConfigured,
            _ => Strings.Ai_ErrorUnexpected
        };

    public void Dispose()
    {
        Messages.CollectionChanged -= Messages_CollectionChanged;
        pendingRequest?.Cancel();
        pendingRequest?.Dispose();
        pendingRequest = null;
    }
}
