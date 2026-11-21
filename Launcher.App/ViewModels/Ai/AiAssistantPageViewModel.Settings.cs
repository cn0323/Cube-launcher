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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Models.Ai;
using Launcher.App.Resources;
using Launcher.App.Services.Ai;
using Launcher.Application.Services.Ai;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Ai;

/// <summary>
/// AI 助手的接口配置部分。配置只决定“用哪个模型”，保存后立即生效，不需要重启。
/// </summary>
public sealed partial class AiAssistantPageViewModel
{
    [ObservableProperty]
    private string endpointInput = string.Empty;

    [ObservableProperty]
    private string apiKeyInput = string.Empty;

    [ObservableProperty]
    private string modelInput = string.Empty;

    [ObservableProperty]
    private AiProviderPreset? selectedPreset;

    [ObservableProperty]
    private bool isTesting;

    [ObservableProperty]
    private bool hasTestResult;

    [ObservableProperty]
    private bool isTestSucceeded;

    [ObservableProperty]
    private string testResultText = string.Empty;

    [ObservableProperty]
    private bool isSaving;

    public IReadOnlyList<AiProviderPreset> Presets => AiProviderPreset.All;

    /// <summary>
    /// 把已保存的配置回填到输入框，并尽量匹配到一个预设。
    /// </summary>
    private void LoadSettingsIntoEditor()
    {
        EndpointInput = currentSettings.AiAssistantEndpoint;
        ApiKeyInput = currentSettings.AiAssistantApiKey;
        ModelInput = currentSettings.AiAssistantModel;

        SelectedPreset = Presets.FirstOrDefault(p =>
            !p.IsCustom
            && string.Equals(p.Endpoint, EndpointInput, StringComparison.OrdinalIgnoreCase))
            ?? Presets.FirstOrDefault(p => p.IsCustom);
    }

    partial void OnSelectedPresetChanged(AiProviderPreset? value)
    {
        if (value is null || value.IsCustom)
            return;

        EndpointInput = value.Endpoint;
        ModelInput = value.Model;
        HasTestResult = false;
    }

    partial void OnEndpointInputChanged(string value) => HasTestResult = false;

    partial void OnModelInputChanged(string value) => HasTestResult = false;

    private static bool IsHttpEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (IsSaving)
            return;

        var endpoint = EndpointInput?.Trim() ?? string.Empty;
        if (endpoint.Length > 0 && !IsHttpEndpoint(endpoint))
        {
            statusService.Report(Strings.Ai_SettingsEndpointInvalid);
            return;
        }

        IsSaving = true;
        try
        {
            var apiKey = ApiKeyInput?.Trim() ?? string.Empty;
            var model = ModelInput?.Trim() ?? string.Empty;

            currentSettings = await settingsService.UpdateAsync(settings =>
            {
                settings.AiAssistantEndpoint = endpoint;
                settings.AiAssistantApiKey = apiKey;
                settings.AiAssistantModel = model;
            }).ConfigureAwait(true);

            UpdateModeIndicators();
            HasTestResult = false;
            statusService.Report(Strings.Ai_SettingsSaved);
            IsSettingsOpen = false;

            logger.LogInformation(
                "AI assistant configuration saved. Online={Online} Model={Model} Endpoint={Endpoint}",
                IsOnlineMode,
                string.IsNullOrEmpty(model) ? "<none>" : model,
                string.IsNullOrEmpty(endpoint) ? "<none>" : endpoint);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save the AI assistant configuration.");
            statusService.Report(Strings.Ai_SettingsSaveFailed);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsTesting)
            return;

        var options = new AiChatOptions(
            EndpointInput?.Trim() ?? string.Empty,
            ApiKeyInput?.Trim() ?? string.Empty,
            ModelInput?.Trim() ?? string.Empty,
            AiAssistantSystemPrompt.Build(currentSettings));

        if (!options.IsConfigured)
        {
            HasTestResult = true;
            IsTestSucceeded = false;
            TestResultText = Strings.Ai_TestIncomplete;
            return;
        }

        IsTesting = true;
        TestResultText = Strings.Ai_Testing;
        HasTestResult = true;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var reply = await assistantService.ProbeAsync(options, timeout.Token).ConfigureAwait(true);

            IsTestSucceeded = reply.IsSuccess;
            TestResultText = reply.IsSuccess ? Strings.Ai_TestSuccess : DescribeFailure(reply.FailureCode);
            logger.LogInformation(
                "AI assistant probe finished. Success={Success} Code={Code}",
                reply.IsSuccess,
                reply.FailureCode ?? "<none>");
        }
        catch (OperationCanceledException)
        {
            IsTestSucceeded = false;
            TestResultText = Strings.Ai_TestTimeout;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI assistant probe failed unexpectedly.");
            IsTestSucceeded = false;
            TestResultText = Strings.Ai_TestFailed;
        }
        finally
        {
            IsTesting = false;
        }
    }
}
