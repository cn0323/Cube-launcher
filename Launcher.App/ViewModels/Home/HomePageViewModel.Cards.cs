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

using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Home;

/// <summary>
/// 首页可选小卡片：长按卡片进入编辑态后，可以把联机、下载、资源、设置加到首页，
/// 加进来的卡片同样参与长按拖动排序，并在设置里持久化。
/// </summary>
public sealed partial class HomePageViewModel
{
    public const string OnlineCardKey = "online";
    public const string DownloadCardKey = "download";
    public const string ResourcesCardKey = "resources";
    public const string SettingsCardKey = "settings";

    [ObservableProperty]
    private bool isHomeCardEditing;

    public bool HasOnlineCard => HasExtraCard(OnlineCardKey);

    public bool HasDownloadCard => HasExtraCard(DownloadCardKey);

    public bool HasResourcesCard => HasExtraCard(ResourcesCardKey);

    public bool HasSettingsCard => HasExtraCard(SettingsCardKey);

    public bool CanAddOnlineCard => !HasOnlineCard;

    public bool CanAddDownloadCard => !HasDownloadCard;

    public bool CanAddResourcesCard => !HasResourcesCard;

    public bool CanAddSettingsCard => !HasSettingsCard;

    /// <summary>四张可选卡片都加过之后，编辑态里就没有可点的添加项了。</summary>
    public bool HasAddableCard => CanAddOnlineCard || CanAddDownloadCard || CanAddResourcesCard || CanAddSettingsCard;

    [RelayCommand]
    private void ToggleHomeCardEditing() => IsHomeCardEditing = !IsHomeCardEditing;

    [RelayCommand]
    private void FinishHomeCardEditing() => IsHomeCardEditing = false;

    [RelayCommand]
    private void AddHomeCard(string? key)
    {
        if (!IsKnownCardKey(key) || HasExtraCard(key!))
            return;

        var current = ReadExtraCards();
        current.Add(key!);
        ApplyExtraCards(current);
        AppendCardOrder(key!);
    }

    [RelayCommand]
    private void RemoveHomeCard(string? key)
    {
        if (!IsKnownCardKey(key))
            return;

        var current = ReadExtraCards();
        if (current.Remove(key!))
            ApplyExtraCards(current);

        RemoveFromCardOrder(key!);
    }

    /// <summary>卡片点击后切换到对应页面。</summary>
    [RelayCommand]
    private void OpenHomeCard(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
            return;

        navigateToPage?.Invoke(page);
    }

    internal void NotifyHomeCardState()
    {
        OnPropertyChanged(nameof(HasOnlineCard));
        OnPropertyChanged(nameof(HasDownloadCard));
        OnPropertyChanged(nameof(HasResourcesCard));
        OnPropertyChanged(nameof(HasSettingsCard));
        OnPropertyChanged(nameof(CanAddOnlineCard));
        OnPropertyChanged(nameof(CanAddDownloadCard));
        OnPropertyChanged(nameof(CanAddResourcesCard));
        OnPropertyChanged(nameof(CanAddSettingsCard));
        OnPropertyChanged(nameof(HasAddableCard));
    }

    private static bool IsKnownCardKey(string? key) =>
        key is OnlineCardKey or DownloadCardKey or ResourcesCardKey or SettingsCardKey;

    private bool HasExtraCard(string key) =>
        ReadExtraCards().Any(item => string.Equals(item, key, StringComparison.Ordinal));

    private List<string> ReadExtraCards() =>
        (settings.HomeExtraCards ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private void ApplyExtraCards(List<string> cards)
    {
        var value = string.Join(',', cards);
        settings.HomeExtraCards = value;
        settingsPersistence?.Update(current => current.HomeExtraCards = value);
        NotifyHomeCardState();
    }

    private void AppendCardOrder(string key)
    {
        var keys = NormalizeHomeCardOrder(settings.HomeCardOrder)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (keys.Contains(key, StringComparer.Ordinal))
            return;

        keys.Add(key);
        ApplyCardOrder(keys);
    }

    private void RemoveFromCardOrder(string key)
    {
        var keys = NormalizeHomeCardOrder(settings.HomeCardOrder)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.Equals(item, key, StringComparison.Ordinal))
            .ToList();
        ApplyCardOrder(keys);
    }

    private void ApplyCardOrder(List<string> keys)
    {
        var value = keys.Count == 0 ? LauncherDefaults.DefaultHomeCardOrder : string.Join(',', keys);
        if (string.Equals(settings.HomeCardOrder, value, StringComparison.Ordinal))
            return;

        settings.HomeCardOrder = value;
        settingsPersistence?.Update(current => current.HomeCardOrder = value);
        OnPropertyChanged(nameof(HomeCardOrder));
    }
}
