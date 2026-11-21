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

namespace Launcher.Application.Services;

public sealed class GameLaunchSession
{
    private int exitHandled;

    public GameLaunchSession(
        string instanceId,
        string instanceName,
        Task<LaunchExitResult> exitTask,
        IReadOnlyList<LaunchWarningKind>? warnings = null,
        int processId = 0)
    {
        InstanceId = instanceId;
        InstanceName = instanceName;
        ExitTask = exitTask;
        Warnings = warnings ?? [];
        ProcessId = processId;
    }

    public string InstanceId { get; }

    public string InstanceName { get; }

    public Task<LaunchExitResult> ExitTask { get; }

    public IReadOnlyList<LaunchWarningKind> Warnings { get; }

    /// <summary>
    /// Minecraft 进程 ID。启动器用它显示运行态信息（网速、时长）并在「停止游戏」时结束进程树。
    /// 0 表示调用方未提供，运行态面板会退化为只显示时长。
    /// </summary>
    public int ProcessId { get; }

    public bool TryMarkExitHandled()
    {
        return Interlocked.Exchange(ref exitHandled, 1) == 0;
    }
}
