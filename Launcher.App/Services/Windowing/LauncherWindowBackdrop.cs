/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using Launcher.Domain.Models;

namespace Launcher.App.Services;

public static class LauncherWindowBackdrop
{
    public static void Attach(Window window, IThemeService themeService)
    {
        const NativeBackdrop.DwmSystemBackdropType backdropType =
            NativeBackdrop.DwmSystemBackdropType.TransientWindow;

        window.SourceInitialized += (_, _) => Apply(window, themeService, backdropType);
        Apply(window, themeService, backdropType);

        EventHandler<EffectiveThemeChangedEventArgs> themeChangedHandler =
            (_, _) => Apply(window, themeService, backdropType);
        EventHandler<BackgroundEffectChangedEventArgs> backgroundEffectChangedHandler =
            (_, _) => Apply(window, themeService, backdropType);
        EventHandler<LiquidGlassChangedEventArgs> liquidGlassChangedHandler =
            (_, _) => Apply(window, themeService, backdropType);

        themeService.EffectiveThemeChanged += themeChangedHandler;
        themeService.BackgroundEffectChanged += backgroundEffectChangedHandler;
        themeService.LiquidGlassChanged += liquidGlassChangedHandler;
        window.Closed += (_, _) =>
        {
            themeService.EffectiveThemeChanged -= themeChangedHandler;
            themeService.BackgroundEffectChanged -= backgroundEffectChangedHandler;
            themeService.LiquidGlassChanged -= liquidGlassChangedHandler;
        };
    }

    private static void Apply(
        Window window,
        IThemeService themeService,
        NativeBackdrop.DwmSystemBackdropType enabledBackdropType)
    {
        // 液态玻璃同样需要系统级模糊作为底衬，否则半透明面板只会透出桌面壁纸本身。
        var isBackdropEnabled = LauncherBackgroundEffects.IsAcrylic(themeService.BackgroundEffect)
                                || themeService.IsLiquidGlassEnabled;
        var backdropType = isBackdropEnabled
            ? enabledBackdropType
            : NativeBackdrop.DwmSystemBackdropType.None;

        if (!window.Dispatcher.CheckAccess())
        {
            window.Dispatcher.Invoke(() => ApplyCore(
                window,
                backdropType,
                themeService.EffectiveTheme,
                isBackdropEnabled));
            return;
        }

        ApplyCore(window, backdropType, themeService.EffectiveTheme, isBackdropEnabled);
    }

    private static void ApplyCore(
        Window window,
        NativeBackdrop.DwmSystemBackdropType backdropType,
        EffectiveTheme theme,
        bool isBackdropEnabled)
    {
        ApplyWindowChrome(window, isBackdropEnabled);
        ApplyWindowBackground(window, theme, isBackdropEnabled);
        NativeBackdrop.ApplyToWindow(window, backdropType, theme, isBackdropEnabled);
    }

    private static void ApplyWindowChrome(Window window, bool isBackdropEnabled)
    {
        var chrome = WindowChrome.GetWindowChrome(window);
        if (chrome is null)
            return;

        chrome.GlassFrameThickness = isBackdropEnabled
            ? new Thickness(-1)
            : new Thickness(0);
    }

    private static void ApplyWindowBackground(
        Window window,
        EffectiveTheme theme,
        bool isBackdropEnabled)
    {
        if (isBackdropEnabled)
        {
            window.Background = Brushes.Transparent;
            return;
        }

        if (global::System.Windows.Application.Current?.TryFindResource("Brush.Surface.Window") is Brush surfaceBrush)
        {
            window.Background = surfaceBrush;
            return;
        }

        window.Background = new SolidColorBrush(NativeBackdrop.GetOpaqueWindowBackgroundColor(theme));
    }
}
