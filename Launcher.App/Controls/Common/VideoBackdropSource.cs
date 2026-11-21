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
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 *
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Launcher.App.Controls;

/// <summary>
/// 启动器视频背景播放器。
/// 在 <see cref="MediaElement"/> 之上补充三项能力：
/// 用 <see cref="IsPlaying"/> 控制播放/暂停、用 0-100 的 <see cref="VolumePercent"/> 直接绑定音量、
/// 以及播放结束后自动从头循环。
/// </summary>
public sealed class VideoBackdropSource : MediaElement
{
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying),
        typeof(bool),
        typeof(VideoBackdropSource),
        new PropertyMetadata(false, OnIsPlayingChanged));

    public static readonly DependencyProperty VolumePercentProperty = DependencyProperty.Register(
        nameof(VolumePercent),
        typeof(int),
        typeof(VideoBackdropSource),
        new PropertyMetadata(60, OnVolumePercentChanged));

    public VideoBackdropSource()
    {
        LoadedBehavior = MediaState.Manual;
        UnloadedBehavior = MediaState.Manual;
        ScrubbingEnabled = false;
        Stretch = Stretch.UniformToFill;
        IsMuted = false;

        MediaOpened += OnMediaOpened;
        MediaEnded += OnMediaEnded;
        MediaFailed += OnMediaFailed;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <summary>
    /// 音量百分比（0-100），映射到 <see cref="MediaElement.Volume"/> 的 0-1。
    /// </summary>
    public int VolumePercent
    {
        get => (int)GetValue(VolumePercentProperty);
        set => SetValue(VolumePercentProperty, value);
    }

    private static void OnIsPlayingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not VideoBackdropSource source)
            return;

        if ((bool)e.NewValue)
            source.TryPlay();
        else
            source.SafePause();
    }

    private static void OnVolumePercentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not VideoBackdropSource source)
            return;

        source.Volume = Math.Clamp((int)e.NewValue, 0, 100) / 100d;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Volume = Math.Clamp(VolumePercent, 0, 100) / 100d;
        if (IsPlaying)
            TryPlay();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // 窗口关闭或背景切换时停止播放，避免视频声音残留在后台。
        SafePause();
    }

    private void OnMediaOpened(object? sender, RoutedEventArgs e)
    {
        if (IsPlaying)
            TryPlay();
    }

    private void OnMediaEnded(object? sender, RoutedEventArgs e)
    {
        if (!IsPlaying)
            return;

        // 循环播放：回到开头继续，淡入淡出不需要，保持与原片一致的节奏。
        try
        {
            Position = TimeSpan.Zero;
            Play();
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // 解码器被移除或文件被占用时停止播放，不影响启动器其它功能。
        }
    }

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        // 单个视频解码失败不应该影响界面，交由用户换一个文件。
        SafePause();
    }

    private void TryPlay()
    {
        if (!IsLoaded || Source is null)
            return;

        try
        {
            Play();
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // 忽略播放器在未就绪状态下的调用。
        }
    }

    private void SafePause()
    {
        try
        {
            Pause();
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
        }
    }
}
