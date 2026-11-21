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

using System.Windows.Controls;
using System.Windows.Input;
using Launcher.App.ViewModels.Home;

namespace Launcher.App.Views.Home;

public partial class HomeLaunchPanelView : UserControl
{
    public HomeLaunchPanelView()
    {
        InitializeComponent();
    }

    private void HomeAccountCard_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 长按拖动换位时宿主会在预览事件里吞掉抬起，这里只在普通点击时跳转。
        if (DataContext is not HomePageViewModel viewModel)
            return;

        if (!viewModel.OpenAccountPageCommand.CanExecute(null))
            return;

        viewModel.OpenAccountPageCommand.Execute(null);
        e.Handled = true;
    }
}

