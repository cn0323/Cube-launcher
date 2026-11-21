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

using CommunityToolkit.Mvvm.ComponentModel;

namespace Launcher.App.ViewModels.Ai;

/// <summary>
/// 对话列表中的一条消息。只承载展示需要的数据，发送流程由页面 ViewModel 负责。
/// </summary>
public sealed partial class AiChatEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string content;

    [ObservableProperty]
    private bool isPending;

    public AiChatEntryViewModel(string content, bool isUser, bool isOffline = false, DateTimeOffset? timestamp = null)
    {
        this.content = content;
        IsUser = isUser;
        IsOffline = isOffline;
        TimeText = (timestamp ?? DateTimeOffset.Now).ToString("HH:mm");
    }

    public bool IsUser { get; }

    public bool IsAssistant => !IsUser;

    /// <summary>
    /// 该条回答由本地知识库产生。界面据此显示离线标记，避免用户误以为来自在线模型。
    /// </summary>
    public bool IsOffline { get; }

    public string TimeText { get; }

    public bool IsOfflineVisible => IsOffline && IsAssistant;
}
