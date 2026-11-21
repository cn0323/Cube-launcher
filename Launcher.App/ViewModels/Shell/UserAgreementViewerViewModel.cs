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
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;

namespace Launcher.App.ViewModels.Shell;

/// <summary>
/// 用户协议内置阅读窗口。协议全文随程序发布，点击后直接显示，不再跳转外部网页。
/// </summary>
public sealed partial class UserAgreementViewerViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isOpen;

    /// <summary>
    /// 协议全文（Markdown 格式），由嵌入资源提供。
    /// </summary>
    public string Markdown => UserAgreementContent.Markdown;

    public void Show() => IsOpen = true;

    public void Close() => IsOpen = false;

    [RelayCommand]
    private void CloseViewer() => Close();
}
