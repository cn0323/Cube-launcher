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

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Launcher.App.ViewModels.Ai;

namespace Launcher.App.Views.Ai;

/// <summary>
/// 聊天页的纯 UI 行为：新消息自动滚到底、Enter 发送、点击遮罩关闭设置面板。
/// </summary>
public partial class AiAssistantView : UserControl
{
    private INotifyCollectionChanged? observedMessages;

    public AiAssistantView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += (_, _) =>
        {
            AttachMessageWatcher();
            ScrollToEnd();
        };
        Unloaded += (_, _) => DetachMessageWatcher();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachMessageWatcher();
        ScrollToEnd();
    }

    private void AttachMessageWatcher()
    {
        DetachMessageWatcher();

        if (DataContext is AiAssistantPageViewModel viewModel)
        {
            observedMessages = viewModel.Messages;
            observedMessages.CollectionChanged += Messages_CollectionChanged;
        }
    }

    private void DetachMessageWatcher()
    {
        if (observedMessages is null)
            return;

        observedMessages.CollectionChanged -= Messages_CollectionChanged;
        observedMessages = null;
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 等布局完成再滚，否则新增气泡的高度还没计算出来。
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ScrollToEnd));
    }

    private void ScrollToEnd()
    {
        MessageScroll?.ScrollToEnd();
    }

    private void InputBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;

        if (DataContext is not AiAssistantPageViewModel viewModel)
            return;

        e.Handled = true;
        if (viewModel.SendCommand.CanExecute(null))
            viewModel.SendCommand.Execute(null);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 页面第一次显示时把焦点放进输入框，用户可以立刻打字。
        if (e.NewValue is true)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusInputBox));
    }

    private void FocusInputBox()
    {
        if (InputBox is null || !InputBox.IsVisible)
            return;

        InputBox.Focus();
        Keyboard.Focus(InputBox);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Esc 统一关闭设置面板，避免浮层看起来“关不掉”。
        if (e.Key != Key.Escape)
            return;

        if (DataContext is not AiAssistantPageViewModel viewModel || !viewModel.IsSettingsOpen)
            return;

        e.Handled = true;
        viewModel.IsSettingsOpen = false;
    }

    private void SettingsOverlay_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AiAssistantPageViewModel viewModel && viewModel.IsSettingsOpen)
            viewModel.IsSettingsOpen = false;
    }
}
