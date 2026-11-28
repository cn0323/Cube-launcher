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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Domain.Models;

namespace Launcher.App.Views.Onboarding;

/// <summary>
/// 首次启动向导：语言 → 外观 → 完成。
/// 只负责收集选择，落盘交给 App 启动流程，避免在这里阻塞界面线程。
/// </summary>
public partial class OnboardingWindow : Window
{
    private const int StepCount = 3;

    private static readonly Brush ActiveDot = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xF5));
    private static readonly Brush IdleDot = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEB));

    private int step;

    public OnboardingWindow(string? theme, bool followSystem, string? language)
    {
        InitializeComponent();

        SelectedTheme = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";
        FollowSystem = followSystem;
        SelectedLanguage = LauncherLanguages.Normalize(language);

        if (FollowSystem)
            ThemeSystem.IsChecked = true;
        else if (SelectedTheme == "Light")
            ThemeLight.IsChecked = true;
        else
            ThemeDark.IsChecked = true;

        switch (SelectedLanguage)
        {
            case LauncherLanguages.TraditionalChinese:
                LanguageZhHant.IsChecked = true;
                break;
            case LauncherLanguages.Japanese:
                LanguageJapanese.IsChecked = true;
                break;
            case LauncherLanguages.English:
                LanguageEnglish.IsChecked = true;
                break;
            default:
                LanguageZhHans.IsChecked = true;
                break;
        }

        ShowStep(0);
    }

    /// <summary>用户是否走完（或主动跳过）向导。</summary>
    public bool Completed { get; private set; }

    public string SelectedTheme { get; private set; }

    public bool FollowSystem { get; private set; }

    public string SelectedLanguage { get; private set; }

    private void ShowStep(int index)
    {
        step = index;

        StepLanguage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        StepTheme.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepDone.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

        var active = ActiveDot;
        var idle = IdleDot;
        StepDot0.Fill = active;
        StepDot1.Fill = index >= 1 ? active : idle;
        StepDot2.Fill = index >= 2 ? active : idle;

        BackButton.IsEnabled = index > 0;
        NextButton.Content = index == StepCount - 1 ? "开始使用" : "下一步";
        StepCounter.Text = $"第 {index + 1} 步 · 共 {StepCount} 步";
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (step < StepCount - 1)
        {
            ShowStep(step + 1);
            return;
        }

        Finish();
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (step > 0)
            ShowStep(step - 1);
    }

    private void SkipButton_OnClick(object sender, RoutedEventArgs e) => Finish();

    private void Finish()
    {
        Completed = true;
        Close();
    }

    private void Language_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string language } && !string.IsNullOrWhiteSpace(language))
            SelectedLanguage = LauncherLanguages.Normalize(language);
    }

    private void Theme_OnChecked(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, ThemeLight))
        {
            SelectedTheme = "Light";
            FollowSystem = false;
        }
        else if (ReferenceEquals(sender, ThemeDark))
        {
            SelectedTheme = "Dark";
            FollowSystem = false;
        }
        else if (ReferenceEquals(sender, ThemeSystem))
        {
            FollowSystem = true;
        }
    }

    private void Window_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 只让空白和标题区拖动，按钮上的按下交给按钮自己。
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 拖拽在按钮抬起后才开始时会走到这里，忽略即可。
        }
    }

    private static T? FindAncestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        var current = node;
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
