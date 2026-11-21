/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 * SPDX-License-Identifier: GPL-3.0-only
 */

using Launcher.Domain.Models;

namespace Launcher.App.Services;

internal readonly record struct LauncherBackgroundPresentation(
    string Effect,
    bool IsWindowBackdropEnabled,
    bool IsImageBackgroundEnabled,
    bool IsImageControlBlurEnabled,
    int PageBackgroundOpacityPercent);

internal static class LauncherBackgroundPresentationPolicy
{
    public static LauncherBackgroundPresentation Resolve(
        string? backgroundEffect,
        int preferredOpacityPercent,
        bool enableImageControlBlur)
    {
        var effect = LauncherBackgroundEffects.Normalize(backgroundEffect);
        var isAcrylic = LauncherBackgroundEffects.IsAcrylic(effect);
        // 视频背景与图片背景共用同一套“媒体底板”处理：页面底板让位，控件背景可模糊。
        var isMedia = LauncherBackgroundEffects.IsMedia(effect);
        return new LauncherBackgroundPresentation(
            effect,
            isAcrylic,
            isMedia,
            isMedia && enableImageControlBlur,
            isAcrylic ? NormalizeOpacity(preferredOpacityPercent) : 100);
    }

    private static int NormalizeOpacity(int opacityPercent) => Math.Clamp(opacityPercent, 0, 100);
}
