/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 *
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.Infrastructure.Accounts;

internal sealed class MicrosoftClientIdProvider
{
    internal const string EnvironmentVariableName = "CUBELAUNCHER_MICROSOFT_CLIENT_ID";
    internal const string LocalConfigurationFileName = "microsoft-client-id";
    internal const string EmbeddedResourceName = "Launcher.Infrastructure.Accounts.microsoft-client-id";

    /// <summary>
    /// 内置的公共 Microsoft 应用 ID，来源：Prism Launcher 公开源码 CMakeLists.txt
    /// 中的 Launcher_MSA_CLIENT_ID。该 ID 已被 Minecraft 服务允许，因此在没有任何
    /// 自定义配置时可作为兜底，让正版登录开箱即用，用户不必自行注册 Azure 应用。
    ///
    /// 使用者若自行注册了应用，写入配置即可覆盖此值；维护者发布正式版本时
    /// 建议通过 https://aka.ms/mce-reviewappid 申请自己的应用 ID 并嵌入构建。
    /// 使用第三方应用 ID 需遵守 Microsoft Identity Platform 使用条款，
    /// 且存在该 ID 日后被撤销、导致登录失效的可能。
    /// </summary>
    internal const string FallbackClientId = "c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb";

    private readonly ILogger<MicrosoftClientIdProvider> logger;
    private readonly Func<string?> embeddedValueProvider;
    private readonly Func<string?> environmentValueProvider;
    private readonly Func<IEnumerable<string>> localConfigurationPathProvider;

    public MicrosoftClientIdProvider(
        ILogger<MicrosoftClientIdProvider>? logger = null,
        Func<string?>? embeddedValueProvider = null,
        Func<string?>? environmentValueProvider = null,
        Func<IEnumerable<string>>? localConfigurationPathProvider = null)
    {
        this.logger = logger ?? NullLogger<MicrosoftClientIdProvider>.Instance;
        this.embeddedValueProvider = embeddedValueProvider ?? ReadEmbeddedValue;
        this.environmentValueProvider = environmentValueProvider
            ?? (() => Environment.GetEnvironmentVariable(EnvironmentVariableName));
        this.localConfigurationPathProvider = localConfigurationPathProvider ?? EnumerateDefaultLocalConfigurationPaths;
    }

    /// <summary>
    /// 解析用于微软登录的应用 ID。
    /// 优先级：本地配置 → 环境变量 → 构建时嵌入 → 内置公共 ID。
    /// 用户显式配置必须优先于内置兜底值，否则无法改用自己注册的应用。
    /// 由于存在内置兜底，正常构建下本方法不会再抛出「未配置」异常。
    /// </summary>
    public string GetRequiredClientId()
    {
        foreach (var path in localConfigurationPathProvider().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                var value = File.ReadAllText(path);
                if (TryNormalize(value, out var configuredClientId))
                {
                    logger.LogDebug(
                        "Resolved Microsoft application client ID from local configuration. ConfigurationPath={ConfigurationPath}",
                        path);
                    return configuredClientId;
                }

                logger.LogWarning(
                    "Ignored invalid Microsoft application client ID configuration. ConfigurationPath={ConfigurationPath}",
                    path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(
                    "Failed to read Microsoft application client ID configuration. ConfigurationPath={ConfigurationPath} ErrorType={ErrorType}",
                    path,
                    exception.GetType().FullName);
            }
        }

        var environmentValue = environmentValueProvider();
        if (TryNormalize(environmentValue, out var environmentClientId))
        {
            logger.LogDebug(
                "Resolved Microsoft application client ID from environment configuration. VariableName={VariableName}",
                EnvironmentVariableName);
            return environmentClientId;
        }

        var embeddedValue = embeddedValueProvider();
        if (TryNormalize(embeddedValue, out var embeddedClientId))
        {
            logger.LogDebug(
                "Resolved Microsoft application client ID from embedded configuration. ResourceName={ResourceName}",
                EmbeddedResourceName);
            return embeddedClientId;
        }

        if (TryNormalize(FallbackClientId, out var fallbackClientId))
        {
            logger.LogDebug(
                "Using the built-in shared Microsoft application client ID. Source={Source}",
                "PrismLauncher/CMakeLists.txt");
            return fallbackClientId;
        }

        throw new MicrosoftAuthenticationConfigurationException(
            "Microsoft account login is unavailable because the application client ID is not configured.");
    }

    internal static bool TryNormalize(string? value, out string clientId)
    {
        var normalized = value?.Trim();
        if (Guid.TryParse(normalized, out var parsed) && parsed != Guid.Empty)
        {
            clientId = parsed.ToString("D");
            return true;
        }

        clientId = string.Empty;
        return false;
    }

    private static string? ReadEmbeddedValue()
    {
        try
        {
            using var stream = typeof(MicrosoftClientIdProvider)
                .Assembly
                .GetManifestResourceStream(EmbeddedResourceName);
            if (stream is null)
                return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateDefaultLocalConfigurationPaths()
    {
        // 1) 用户数据目录（界面上填写 Client ID 时写入这里，最稳妥）。
        yield return MicrosoftClientIdConfiguration.StorePath;

        // 2) 当前工作目录与程序目录下的裸文件名，方便直接拖放一个文件进去。
        foreach (var root in EnumerateConfigurationRoots())
        {
            yield return Path.Combine(root, LocalConfigurationFileName);
            yield return Path.Combine(root, ".local-secrets", LocalConfigurationFileName);
            yield return Path.Combine(root, "CL", LocalConfigurationFileName);
            yield return Path.Combine(root, "CL", ".local-secrets", LocalConfigurationFileName);
        }
    }

    private static IEnumerable<string> EnumerateConfigurationRoots()
    {
        var roots = new List<string>();

        var currentDirectory = TryGetDirectory(() => Directory.GetCurrentDirectory());
        if (currentDirectory is not null)
            roots.Add(currentDirectory);

        var baseDirectory = TryGetDirectory(() => AppContext.BaseDirectory);
        if (baseDirectory is not null
            && !roots.Contains(baseDirectory, StringComparer.OrdinalIgnoreCase))
        {
            roots.Add(baseDirectory);
        }

        return roots;
    }

    private static string? TryGetDirectory(Func<string> provider)
    {
        try
        {
            var value = provider();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
