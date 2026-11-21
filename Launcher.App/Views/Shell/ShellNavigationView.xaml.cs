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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Launcher.App.Models;

namespace Launcher.App.Views.Shell;

public partial class ShellNavigationView : UserControl
{
    /// <summary>透镜直径，和 XAML 里的 Width/Height 保持一致。</summary>
    private const double LensSize = 60;

    private int downloadTaskPulseDispatchQueued;
    private bool isDownloadTaskPulseRunning;
    private DateTimeOffset lastDownloadTaskPulseAt = DateTimeOffset.MinValue;
    private MainViewModel? viewModel;

    private double lensY = double.NaN;
    private int lensRetry;

    public ShellNavigationView()
    {
        InitializeComponent();

        DataContextChanged += ShellNavigationView_DataContextChanged;
        Loaded += (_, _) => RefreshLens(animate: false);
        NavRoot.SizeChanged += (_, _) => RefreshLens(animate: false);
        DockBody.SizeChanged += (_, _) => RefreshLens(animate: false);
        Unloaded += (_, _) => AttachViewModel(null);
    }

    private void ShellNavigationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(e.NewValue as MainViewModel);
    }

    private void AttachViewModel(MainViewModel? nextViewModel)
    {
        if (ReferenceEquals(viewModel, nextViewModel))
            return;

        if (viewModel is not null)
        {
            viewModel.NavigationItems.CollectionChanged -= NavigationItems_CollectionChanged;
            viewModel.DownloadTasksNavigationItem.PropertyChanged -= NavigationItem_PropertyChanged;
            viewModel.DownloadTasksPage.TaskStarted -= DownloadTasksPage_TaskStarted;

            foreach (var item in viewModel.NavigationItems)
                item.PropertyChanged -= NavigationItem_PropertyChanged;
        }

        viewModel = nextViewModel;

        if (viewModel is not null)
        {
            viewModel.NavigationItems.CollectionChanged += NavigationItems_CollectionChanged;
            viewModel.DownloadTasksNavigationItem.PropertyChanged += NavigationItem_PropertyChanged;
            viewModel.DownloadTasksPage.TaskStarted += DownloadTasksPage_TaskStarted;

            foreach (var item in viewModel.NavigationItems)
                item.PropertyChanged += NavigationItem_PropertyChanged;

            Dispatcher.BeginInvoke(() => RefreshLens(animate: false), DispatcherPriority.Loaded);
        }
    }

    private void NavigationItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (NavigationItem item in e.OldItems)
                item.PropertyChanged -= NavigationItem_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (NavigationItem item in e.NewItems)
                item.PropertyChanged += NavigationItem_PropertyChanged;
        }

        RefreshLens(animate: false);
    }

    private void NavigationItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(NavigationItem.IsSelected) or nameof(NavigationItem.AvatarUrl)))
            return;

        if (e.PropertyName == nameof(NavigationItem.IsSelected))
            Dispatcher.BeginInvoke(() => RefreshLens(animate: true), DispatcherPriority.Loaded);
    }

    /* ------------------------------------------------------------------
       透镜定位
       ------------------------------------------------------------------ */

    private void RefreshLens(bool animate)
    {
        if (viewModel is null)
        {
            DockLens.Opacity = 0;
            return;
        }

        var selected = FindSelectedItem();
        if (selected is null)
        {
            DockLens.Opacity = 0;
            return;
        }

        var button = FindItemButton(DockBody, selected);
        if (button is null || button.ActualHeight <= 0)
        {
            // 容器还没生成好：下一帧再试，最多重试几次，避免无限排队
            if (lensRetry++ < 3)
                Dispatcher.BeginInvoke(() => RefreshLens(animate: false), DispatcherPriority.Loaded);

            return;
        }

        lensRetry = 0;

        var offset = button.TransformToAncestor(DockBody).Transform(new Point(0, 0));
        var target = offset.Y + ((button.ActualHeight - LensSize) / 2);

        MoveLens(target, animate);
        DockLens.Opacity = 1;
    }

    private NavigationItem? FindSelectedItem()
    {
        if (viewModel is null)
            return null;

        if (viewModel.DownloadTasksNavigationItem.IsSelected)
            return viewModel.DownloadTasksNavigationItem;

        foreach (var item in viewModel.NavigationItems)
        {
            if (item.IsSelected)
                return item;
        }

        return null;
    }

    private static Button? FindItemButton(DependencyObject root, object dataContext)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button button && ReferenceEquals(button.DataContext, dataContext))
                return button;

            var nested = FindItemButton(child, dataContext);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void MoveLens(double target, bool animate)
    {
        if (double.IsNaN(lensY))
            animate = false;

        lensY = target;

        if (!animate)
        {
            LensOffset.BeginAnimation(TranslateTransform.YProperty, null);
            LensOffset.Y = target;
            return;
        }

        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(430))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        LensOffset.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    /* ------------------------------------------------------------------
       下载任务脉冲（原有行为）
       ------------------------------------------------------------------ */

    private void DownloadTasksPage_TaskStarted(object? sender, DownloadTaskItem e)
    {
        if (Interlocked.Exchange(ref downloadTaskPulseDispatchQueued, 1) == 1)
            return;

        Dispatcher.BeginInvoke(TryRunDownloadTaskPulse, DispatcherPriority.Render);
    }

    private void TryRunDownloadTaskPulse()
    {
        Interlocked.Exchange(ref downloadTaskPulseDispatchQueued, 0);

        if (isDownloadTaskPulseRunning)
            return;

        var now = DateTimeOffset.UtcNow;
        if (now - lastDownloadTaskPulseAt < TimeSpan.FromMilliseconds(950))
            return;

        lastDownloadTaskPulseAt = now;
        RunDownloadTaskPulse();
    }

    private void RunDownloadTaskPulse()
    {
        const double initialOpacity = 1;
        isDownloadTaskPulseRunning = true;
        var duration = TimeSpan.FromMilliseconds(850);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        DownloadTaskPulseCircle.BeginAnimation(UIElement.OpacityProperty, null);
        DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        DownloadTaskPulseCircle.Opacity = initialOpacity;
        DownloadTaskPulseScale.ScaleX = 0.9;
        DownloadTaskPulseScale.ScaleY = 0.9;

        var opacityAnimation = new DoubleAnimation(initialOpacity, 0, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        opacityAnimation.Completed += (_, _) =>
        {
            DownloadTaskPulseCircle.BeginAnimation(UIElement.OpacityProperty, null);
            DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            DownloadTaskPulseCircle.Opacity = 0;
            DownloadTaskPulseScale.ScaleX = 1;
            DownloadTaskPulseScale.ScaleY = 1;
            isDownloadTaskPulseRunning = false;
        };

        DownloadTaskPulseCircle.BeginAnimation(
            UIElement.OpacityProperty,
            opacityAnimation,
            HandoffBehavior.SnapshotAndReplace);

        var scaleAnimation = new DoubleAnimation(0.9, 1, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
        DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone(), HandoffBehavior.SnapshotAndReplace);
    }
}
