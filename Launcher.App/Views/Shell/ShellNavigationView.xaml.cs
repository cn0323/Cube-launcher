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
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Launcher.App.Models;

namespace Launcher.App.Views.Shell;

public partial class ShellNavigationView : UserControl
{
    /// <summary>透镜直径，和 XAML 里的 Width/Height 保持一致。</summary>
    private const double LensSize = 96;

    /// <summary>透镜里内容的放大倍率，和 XAML 里的 LensScale 保持一致。</summary>
    private const double LensZoom = 1.5;

    private int downloadTaskPulseDispatchQueued;
    private bool isDownloadTaskPulseRunning;
    private DateTimeOffset lastDownloadTaskPulseAt = DateTimeOffset.MinValue;
    private MainViewModel? viewModel;

    private double lensY = double.NaN;
    private int lensRetry;
    private bool introPlayed;

    public ShellNavigationView()
    {
        InitializeComponent();

        LensScale.ScaleX = LensZoom;
        LensScale.ScaleY = LensZoom;

        DataContextChanged += ShellNavigationView_DataContextChanged;
        Loaded += (_, _) =>
        {
            PlayIntro();
            RefreshLens(animate: false);
        };
        NavRoot.SizeChanged += (_, _) => RefreshLens(animate: false);
        DockBody.SizeChanged += (_, _) => RefreshLens(animate: false);
        Unloaded += (_, _) => AttachViewModel(null);
    }

    /// <summary>进场：胶囊从左侧滑入并淡出，缓动收尾时有一点回弹。</summary>
    private void PlayIntro()
    {
        if (introPlayed)
            return;

        introPlayed = true;

        DockRoot.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            },
            HandoffBehavior.SnapshotAndReplace);

        DockIntroOffset.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(-18, 0, TimeSpan.FromMilliseconds(560))
            {
                EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
            },
            HandoffBehavior.SnapshotAndReplace);
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

        // 透镜里是选中项的一份放大副本，切页时连内容一起换。
        UpdateLensContent(selected);

        // 原项让位给透镜，否则半透明玻璃下面会叠出第二个图标和第二个标题。
        UpdateSelectionVisibility(selected);

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

    /// <summary>选中项在胶囊里隐藏，只由透镜显示，避免重影。</summary>
    private void UpdateSelectionVisibility(NavigationItem selected)
    {
        if (viewModel is null)
            return;

        foreach (var item in viewModel.NavigationItems)
        {
            var button = FindItemButton(DockBody, item);
            if (button is not null)
                button.Opacity = ReferenceEquals(item, selected) ? 0 : 1;
        }

        var downloadButton = FindItemButton(DockBody, viewModel.DownloadTasksNavigationItem);
        if (downloadButton is not null)
        {
            downloadButton.Opacity = ReferenceEquals(viewModel.DownloadTasksNavigationItem, selected)
                ? 0
                : 1;
        }
    }

    /// <summary>把选中项的图标、头像与标题同步到透镜里那份放大副本。</summary>
    private void UpdateLensContent(NavigationItem item)
    {
        var hasAvatar = item.HasAvatar;
        var hasIcon = !string.IsNullOrEmpty(item.IconKey);
        var showIcon = hasIcon && !hasAvatar;

        LensIcon.IconKey = item.IconKey;
        LensIconCyan.IconKey = item.IconKey;
        LensIconMagenta.IconKey = item.IconKey;

        LensIcon.Visibility = showIcon ? Visibility.Visible : Visibility.Collapsed;
        LensIconCyan.Visibility = LensIcon.Visibility;
        LensIconMagenta.Visibility = LensIcon.Visibility;

        LensIconFont.Text = item.Icon;
        LensIconFont.Visibility = !hasIcon && !hasAvatar ? Visibility.Visible : Visibility.Collapsed;

        LensAvatar.Visibility = hasAvatar ? Visibility.Visible : Visibility.Collapsed;
        if (hasAvatar)
        {
            BindingOperations.SetBinding(
                LensAvatarImage,
                Image.SourceProperty,
                new Binding(nameof(NavigationItem.AvatarUrl)) { Source = item });
        }
        else
        {
            BindingOperations.ClearBinding(LensAvatarImage, Image.SourceProperty);
            LensAvatarImage.Source = null;
        }

        LensLabel.Text = FindResource("PageTitleConverter") is IValueConverter converter
            ? converter.Convert(item.Page, typeof(string), null, CultureInfo.CurrentUICulture) as string ?? item.Title
            : item.Title;
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

        // 收尾给一点回弹，像一颗珠子滑过去停住，而不是硬生生刹停。
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(520))
        {
            EasingFunction = new BackEase { Amplitude = 0.24, EasingMode = EasingMode.EaseOut }
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
