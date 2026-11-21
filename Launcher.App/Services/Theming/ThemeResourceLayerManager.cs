/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.Windows;
using Launcher.Domain.Models;

namespace Launcher.App.Services;

internal sealed class ThemeResourceLayerManager
{
    internal const string PageBackgroundOpacityResourceKey = "Opacity.Page.Background";

    /// <summary>
    /// 液态玻璃开启后页面底板本身已经更透，这里再收一点，让系统模糊与光晕透上来。
    /// </summary>
    private const double LiquidGlassPageOpacityScale = 0.8d;

    private const string DarkThemeSource =
        "pack://application:,,,/CubeLauncher_x64;component/Resources/Themes/Dark.xaml";

    private const string LightThemeSource =
        "pack://application:,,,/CubeLauncher_x64;component/Resources/Themes/Light.xaml";

    private const string AccentThemeSourcePrefix =
        "pack://application:,,,/CubeLauncher_x64;component/Resources/Themes/Accents/";

    private const string ImageBackgroundSource =
        "pack://application:,,,/CubeLauncher_x64;component/Resources/Themes/Backgrounds/Image.xaml";

    private const string ImageBlurSource =
        "pack://application:,,,/CubeLauncher_x64;component/Resources/Themes/Backgrounds/ImageBlur.xaml";

    private const string LiquidGlassSourcePrefix =
        "pack://application:,,,/CubeLauncher_x64;component/Resources/Themes/Glass/";

    private readonly Func<string, ResourceDictionary> dictionaryFactory;
    private ResourceDictionary? themeLayer;
    private string? themeLayerSource;
    private ResourceDictionary? accentLayer;
    private string? accentLayerSource;
    private ResourceDictionary? imageLayer;
    private ResourceDictionary? imageBlurLayer;
    private ResourceDictionary? liquidGlassLayer;
    private string? liquidGlassLayerSource;

    public ThemeResourceLayerManager()
        : this(source => new ResourceDictionary { Source = new Uri(source, UriKind.Absolute) })
    {
    }

    internal ThemeResourceLayerManager(Func<string, ResourceDictionary> dictionaryFactory)
    {
        this.dictionaryFactory = dictionaryFactory ?? throw new ArgumentNullException(nameof(dictionaryFactory));
    }

    public void ApplyLayers(
        ResourceDictionary applicationResources,
        EffectiveTheme theme,
        string accentColor,
        bool imageBackgroundEnabled,
        bool imageControlBlurEnabled,
        bool liquidGlassEnabled)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);

        var dictionaries = applicationResources.MergedDictionaries;
        var nextThemeSource = GetThemeSource(theme);
        var nextAccentLayer = GetAccentLayer(accentColor);
        var accentIndex = accentLayer is null ? -1 : dictionaries.IndexOf(accentLayer);
        if (themeLayer is not null
            && string.Equals(themeLayerSource, nextThemeSource, StringComparison.Ordinal)
            && dictionaries.Contains(themeLayer)
            && accentIndex >= 0
            && (imageLayer is not null && dictionaries.Contains(imageLayer)) == imageBackgroundEnabled
            && (imageBlurLayer is not null && dictionaries.Contains(imageBlurLayer)) == (imageBackgroundEnabled && imageControlBlurEnabled))
        {
            if (!ReferenceEquals(accentLayer, nextAccentLayer))
                dictionaries[accentIndex] = nextAccentLayer;
            accentLayer = nextAccentLayer;
            RefreshLiquidGlassLayer(dictionaries, theme, liquidGlassEnabled);
            return;
        }

        RemoveLayer(dictionaries, themeLayer);
        RemoveLayer(dictionaries, accentLayer);
        RemoveLayer(dictionaries, imageLayer);
        RemoveLayer(dictionaries, imageBlurLayer);
        RemoveLayer(dictionaries, liquidGlassLayer);

        themeLayer = GetOrCreateLayer(themeLayer, ref themeLayerSource, nextThemeSource);
        accentLayer = nextAccentLayer;
        dictionaries.Add(themeLayer);
        dictionaries.Add(accentLayer);
        if (imageBackgroundEnabled)
        {
            imageLayer ??= dictionaryFactory(ImageBackgroundSource);
            dictionaries.Add(imageLayer);
            if (imageControlBlurEnabled)
            {
                imageBlurLayer ??= dictionaryFactory(ImageBlurSource);
                dictionaries.Add(imageBlurLayer);
            }
        }

        RefreshLiquidGlassLayer(dictionaries, theme, liquidGlassEnabled);
    }

    /// <summary>
    /// 玻璃层必须排在最后：它要覆盖主题层与图片背景层里的面板笔刷。
    /// </summary>
    private void RefreshLiquidGlassLayer(
        ICollection<ResourceDictionary> dictionaries,
        EffectiveTheme theme,
        bool liquidGlassEnabled)
    {
        if (!liquidGlassEnabled)
        {
            RemoveLayer(dictionaries, liquidGlassLayer);
            liquidGlassLayer = null;
            return;
        }

        var nextSource = GetLiquidGlassSource(theme);
        if (liquidGlassLayer is not null
            && string.Equals(liquidGlassLayerSource, nextSource, StringComparison.OrdinalIgnoreCase)
            && dictionaries.Contains(liquidGlassLayer))
        {
            return;
        }

        RemoveLayer(dictionaries, liquidGlassLayer);
        liquidGlassLayer = GetOrCreateLayer(liquidGlassLayer, ref liquidGlassLayerSource, nextSource);
        dictionaries.Add(liquidGlassLayer);
    }

    public static void ApplyPageBackgroundOpacity(
        ResourceDictionary applicationResources,
        int opacityPercent,
        bool liquidGlassEnabled)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);
        var percent = Math.Clamp(opacityPercent, 0, 100);
        var scale = liquidGlassEnabled ? LiquidGlassPageOpacityScale : 1d;
        applicationResources[PageBackgroundOpacityResourceKey] = percent / 100d * scale;
    }

    internal static string GetThemeSource(EffectiveTheme theme) =>
        theme is EffectiveTheme.Light ? LightThemeSource : DarkThemeSource;

    internal static string GetAccentThemeSource(string accentColor) =>
        $"{AccentThemeSourcePrefix}{LauncherAccentColors.Normalize(accentColor)}.xaml";

    internal static string GetLiquidGlassSource(EffectiveTheme theme) =>
        $"{LiquidGlassSourcePrefix}{(theme is EffectiveTheme.Light ? "Light" : "Dark")}.xaml";

    private ResourceDictionary GetAccentLayer(string accentColor)
    {
        if (!LauncherAccentColors.TryNormalizeCustom(accentColor, out var customColor))
            return GetOrCreateLayer(accentLayer, ref accentLayerSource, GetAccentThemeSource(accentColor));

        if (accentLayer is not null && string.Equals(accentLayerSource, customColor, StringComparison.Ordinal))
            return accentLayer;

        accentLayerSource = customColor;
        return CustomAccentResourceFactory.Create(customColor);
    }

    private ResourceDictionary GetOrCreateLayer(
        ResourceDictionary? currentLayer,
        ref string? currentSource,
        string nextSource)
    {
        if (currentLayer is not null
            && string.Equals(currentSource, nextSource, StringComparison.OrdinalIgnoreCase))
            return currentLayer;

        currentSource = nextSource;
        return dictionaryFactory(nextSource);
    }

    private static void RemoveLayer(
        ICollection<ResourceDictionary> dictionaries,
        ResourceDictionary? layer)
    {
        if (layer is null)
            return;

        while (dictionaries.Remove(layer))
        {
        }
    }
}
