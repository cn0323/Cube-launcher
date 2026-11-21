using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.IO;
using Launcher.Application;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.Infrastructure.Updates;

public sealed class RemoteManifestLauncherUpdateService : ILauncherUpdateService
{
    private const int MaximumManifestBytes = 1024 * 1024;
    // Release 列表包含 release notes，可能比清单大，单独给一个上限。
    private const int MaximumReleaseListBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // 宽松提取版本号：兼容 v0.10.3、CubeLauncher-0.10.3、release_0.10.3 等常见 tag 命名。
    private static readonly Regex VersionPattern = new(
        @"(?<![\d.])(?<major>\d{1,3})\.(?<minor>\d{1,3})(?:\.(?<patch>\d{1,3}))?",
        RegexOptions.CultureInvariant);
    private readonly HttpClient httpClient;
    private readonly ILogger<RemoteManifestLauncherUpdateService>? logger;
    private readonly IReadOnlyList<LauncherUpdateManifestSource> manifestSources;

    public RemoteManifestLauncherUpdateService(
        HttpClient? httpClient = null,
        ILogger<RemoteManifestLauncherUpdateService>? logger = null)
        : this(httpClient, logger, LauncherUpdateManifestSource.DefaultSources)
    {
    }

    internal RemoteManifestLauncherUpdateService(
        HttpClient? httpClient,
        ILogger<RemoteManifestLauncherUpdateService>? logger,
        IReadOnlyList<LauncherUpdateManifestSource> manifestSources)
    {
        this.httpClient = httpClient ?? OfficialUpdateHttp.CreateClient(DefaultRequestTimeout);
        this.logger = logger;
        this.manifestSources = manifestSources.OrderBy(source => source.Priority).ToArray();
        EnsureDefaultHeaders(this.httpClient);
    }

    public async Task<LauncherUpdateCheckResult> CheckForUpdatesAsync(
        string currentVersion,
        LauncherUpdateChannel channel,
        CancellationToken cancellationToken = default)
    {
        if (!TryCalculateVersionCode(currentVersion, out var currentVersionCode))
            return LauncherUpdateCheckResult.Failed(currentVersion, "The current launcher version is invalid.");

        var channelText = channel is LauncherUpdateChannel.Beta ? "beta" : "release";

        // 优先用仓库的 Releases：发布者只要打 tag 并上传 exe，检查更新就能直接工作，
        // 不需要额外维护一份清单文件。返回 null 表示 Releases 不可用，继续回退到清单。
        var releaseResult = await TryCheckGitHubReleasesAsync(
            currentVersion,
            currentVersionCode,
            channel,
            cancellationToken).ConfigureAwait(false);
        if (releaseResult is not null)
            return releaseResult;

        foreach (var source in manifestSources)
        {
            var result = await LoadSourceAsync(source, channelText, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ManifestSourceStatus.Valid)
                continue;
            var manifest = result.Manifest!;
            logger?.LogInformation(
                "Validated launcher update manifest. Source={Source} Channel={Channel} VersionCode={VersionCode}",
                result.Source.Name,
                channelText,
                manifest.VersionCode);
            return CreateResult(currentVersion, currentVersionCode, manifest);
        }

        logger?.LogWarning("All launcher update manifest sources failed or were unavailable. Channel={Channel}", channelText);
        return LauncherUpdateCheckResult.Failed(currentVersion, "All update manifest sources failed or were unavailable.");
    }

    /// <summary>
    /// 读取仓库的 GitHub Releases，把它当成一份动态更新清单。
    /// 返回 null 表示这次判定不可用（网络或接口异常），调用方继续回退远端清单；
    /// 返回 Latest 表示仓库确实没有更新的版本可提供。
    /// </summary>
    private async Task<LauncherUpdateCheckResult?> TryCheckGitHubReleasesAsync(
        string currentVersion,
        int currentVersionCode,
        LauncherUpdateChannel channel,
        CancellationToken cancellationToken)
    {
        List<GitHubReleaseDto> releases;
        try
        {
            if (!Uri.TryCreate(
                    LauncherProjectLinks.GitHubReleasesApiUrl + "?per_page=30",
                    UriKind.Absolute,
                    out var releasesUri))
                return null;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DefaultRequestTimeout);
            using var response = await OfficialUpdateHttp.SendAsync(
                    httpClient,
                    releasesUri,
                    OfficialUpdateUriKind.Manifest,
                    timeout.Token)
                .ConfigureAwait(false);
            var payload = await OfficialUpdateHttp
                .ReadLimitedBytesAsync(response, MaximumReleaseListBytes, timeout.Token)
                .ConfigureAwait(false);
            releases = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(payload, JsonOptions) ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is UpdateSourceUnavailableException
                                              or UpdateSecurityException
                                              or JsonException
                                              or InvalidDataException)
        {
            logger?.LogWarning(exception, "GitHub releases could not be read; falling back to update manifests.");
            return null;
        }

        var candidate = SelectNewerRelease(releases, currentVersionCode, channel);
        if (candidate is null)
        {
            // 只打 tag、没发 Release 时 releases 是空的，再查一次标签避免白白判定为最新。
            if (releases.Count == 0)
            {
                var tagResult = await TryCheckGitHubTagsAsync(
                        currentVersion,
                        currentVersionCode,
                        channel,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (tagResult is not null)
                    return tagResult;

                logger?.LogInformation(
                    "GitHub repository has no published release or tag. CurrentVersionCode={CurrentVersionCode} Channel={Channel}",
                    currentVersionCode,
                    channel);
                return LauncherUpdateCheckResult.NoPublishedVersion(currentVersion);
            }

            logger?.LogInformation(
                "GitHub releases contain no newer build. CurrentVersionCode={CurrentVersionCode} Channel={Channel}",
                currentVersionCode,
                channel);
            return LauncherUpdateCheckResult.Latest(currentVersion);
        }

        var version = NormalizeReleaseVersion(candidate.Release.TagName, candidate.Release.Name);
        var releasePageUrl = string.IsNullOrWhiteSpace(candidate.Release.HtmlUrl)
            ? LauncherProjectLinks.GitHubReleasesUrl
            : candidate.Release.HtmlUrl!;
        var releaseNotes = string.IsNullOrWhiteSpace(candidate.Release.Body) ? null : candidate.Release.Body;

        var asset = PickReleaseAsset(candidate.Release);
        if (asset is null
            || asset.Size <= 0
            || !TryGetAssetSha256(asset, out var sha256)
            || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri))
        {
            // 有新版但资产不能自动安装（没有 digest 或没上传 exe）：仍然提示，界面会引导到发布页手动下载。
            logger?.LogWarning(
                "GitHub release {Version} has no installable executable asset; offering the release page instead.",
                version);
            return LauncherUpdateCheckResult.Available(currentVersion, new LauncherUpdateInfo(
                version,
                version,
                releasePageUrl,
                releasePageUrl,
                releaseNotes,
                null,
                LauncherUpdateAssetKind.None,
                0,
                string.Empty,
                VersionCode: candidate.VersionCode,
                PublishedAt: candidate.Release.PublishedAt));
        }

        logger?.LogInformation(
            "GitHub release resolved as launcher update. Version={Version} VersionCode={VersionCode} Asset={Asset}",
            version,
            candidate.VersionCode,
            asset.Name);

        return LauncherUpdateCheckResult.Available(currentVersion, new LauncherUpdateInfo(
            version,
            version,
            releasePageUrl,
            downloadUri.ToString(),
            releaseNotes,
            asset.Name,
            LauncherUpdateAssetKind.WindowsX64Executable,
            asset.Size,
            sha256,
            VersionCode: candidate.VersionCode,
            PublishedAt: candidate.Release.PublishedAt,
            DownloadUrls: [new LauncherUpdateDownloadUrl("github", downloadUri.ToString(), 1)]));
    }

    /// <summary>
    /// Releases 为空时再看仓库标签：有些维护者只打了 tag 而没有建 Release，
    /// 这时仍应提示存在更新，但不提供自动安装，交由发布页手动下载。
    /// </summary>
    private async Task<LauncherUpdateCheckResult?> TryCheckGitHubTagsAsync(
        string currentVersion,
        int currentVersionCode,
        LauncherUpdateChannel channel,
        CancellationToken cancellationToken)
    {
        List<GitHubTagDto> tags;
        try
        {
            if (!Uri.TryCreate(
                    LauncherProjectLinks.GitHubTagsApiUrl + "?per_page=30",
                    UriKind.Absolute,
                    out var tagsUri))
                return null;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DefaultRequestTimeout);
            using var response = await OfficialUpdateHttp.SendAsync(
                    httpClient,
                    tagsUri,
                    OfficialUpdateUriKind.Manifest,
                    timeout.Token)
                .ConfigureAwait(false);
            var payload = await OfficialUpdateHttp
                .ReadLimitedBytesAsync(response, MaximumReleaseListBytes, timeout.Token)
                .ConfigureAwait(false);
            tags = JsonSerializer.Deserialize<List<GitHubTagDto>>(payload, JsonOptions) ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is UpdateSourceUnavailableException
                                              or UpdateSecurityException
                                              or JsonException
                                              or InvalidDataException)
        {
            logger?.LogInformation(exception, "GitHub tags could not be read.");
            return null;
        }

        GitHubTagDto? bestTag = null;
        var bestVersionCode = 0;
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Name))
                continue;
            if (channel is not LauncherUpdateChannel.Beta && LooksLikePrerelease(tag.Name))
                continue;
            if (!TryCalculateVersionCode(tag.Name, out var versionCode)
                || versionCode <= currentVersionCode)
                continue;
            if (versionCode <= bestVersionCode)
                continue;
            bestTag = tag;
            bestVersionCode = versionCode;
        }

        if (bestTag is null)
            return null;

        var version = NormalizeReleaseVersion(bestTag.Name, null);
        logger?.LogInformation(
            "GitHub tag {Tag} is newer than the current build but has no release assets. VersionCode={VersionCode}",
            bestTag.Name,
            bestVersionCode);
        return LauncherUpdateCheckResult.Available(currentVersion, new LauncherUpdateInfo(
            version,
            version,
            LauncherProjectLinks.GitHubReleasesUrl,
            LauncherProjectLinks.GitHubReleasesUrl,
            null,
            null,
            LauncherUpdateAssetKind.None,
            0,
            string.Empty,
            VersionCode: bestVersionCode,
            PublishedAt: null));
    }

    private static ReleaseCandidate? SelectNewerRelease(
        IReadOnlyList<GitHubReleaseDto> releases,
        int currentVersionCode,
        LauncherUpdateChannel channel)
    {
        ReleaseCandidate? best = null;
        foreach (var release in releases)
        {
            if (release.Draft)
                continue;

            // release 渠道只接受正式版；beta 渠道同时接受预发布版本。
            var isPrerelease = release.Prerelease || LooksLikePrerelease(release.TagName);
            if (channel is not LauncherUpdateChannel.Beta && isPrerelease)
                continue;

            if (!TryCalculateVersionCode(release.TagName, out var versionCode)
                || versionCode <= currentVersionCode)
                continue;

            if (best is null || versionCode > best.VersionCode)
                best = new ReleaseCandidate(release, versionCode);
        }

        return best;
    }

    private static bool LooksLikePrerelease(string? tagName)
    {
        // 过去“含 - 就算预发布”会把 CubeLauncher-0.10.3 这类命名误杀，改为只看约定的预发布后缀。
        if (string.IsNullOrWhiteSpace(tagName)) return false;
        var text = tagName.Trim();
        return text.Contains("-beta", StringComparison.OrdinalIgnoreCase)
            || text.Contains("-alpha", StringComparison.OrdinalIgnoreCase)
            || text.Contains("-rc", StringComparison.OrdinalIgnoreCase)
            || text.Contains("-preview", StringComparison.OrdinalIgnoreCase)
            || text.Contains("-dev", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeReleaseVersion(string? tagName, string? name)
    {
        var text = tagName?.Trim();
        if (string.IsNullOrEmpty(text))
            text = name?.Trim();
        if (string.IsNullOrEmpty(text))
            return "0.0.0";

        // 界面只需要标准版本串：把 CubeLauncher-、release_ 之类前缀去掉，beta 后缀保留。
        var match = VersionPattern.Match(text);
        if (!match.Success)
            return text;

        var version = match.Value;
        var suffix = text[(match.Index + match.Length)..];
        var spaceIndex = suffix.IndexOf(' ');
        if (spaceIndex >= 0)
            suffix = suffix[..spaceIndex];
        if (suffix.StartsWith("-beta.", StringComparison.OrdinalIgnoreCase))
            version += suffix;
        return version;
    }

    private static GitHubReleaseAssetDto? PickReleaseAsset(GitHubReleaseDto release)
    {
        var executables = release.Assets
            .Where(asset => asset.Size > 0
                            && !string.IsNullOrWhiteSpace(asset.Name)
                            && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (executables.Length == 0)
            return null;

        return executables.FirstOrDefault(asset =>
                   asset.Name.Contains("CubeLauncher_x64", StringComparison.OrdinalIgnoreCase))
               ?? executables.FirstOrDefault(asset =>
                   asset.Name.Contains("CubeLauncher", StringComparison.OrdinalIgnoreCase))
               ?? executables[0];
    }

    private static bool TryGetAssetSha256(GitHubReleaseAssetDto asset, out string sha256)
    {
        // GitHub 会为上传的资产计算 digest，形如 sha256:xxxx；自更新必须拿到它才能校验。
        sha256 = string.Empty;
        var digest = asset.Digest?.Trim();
        const string prefix = "sha256:";
        if (string.IsNullOrEmpty(digest)
            || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var value = digest[prefix.Length..].Trim();
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            return false;

        sha256 = value.ToLowerInvariant();
        return true;
    }

    private async Task<ManifestSourceResult> LoadSourceAsync(
        LauncherUpdateManifestSource source,
        string expectedChannel,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(source.CreateManifestUrl(expectedChannel), UriKind.Absolute, out var manifestUri))
                throw new UpdateSecurityException("The update manifest URL is invalid.");
            var manifestBytes = await DownloadBytesAsync(manifestUri, MaximumManifestBytes, cancellationToken)
                .ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<RemoteUpdateManifestDto>(manifestBytes, JsonOptions)
                ?? throw new UpdateSecurityException("The update manifest JSON is empty.");
            ValidateManifest(manifest, expectedChannel);
            return ManifestSourceResult.Valid(source, manifest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UpdateSourceUnavailableException exception)
        {
            logger?.LogWarning(exception, "Launcher update manifest source unavailable. Source={Source}", source.Name);
            return ManifestSourceResult.Unavailable(source, exception.Message);
        }
        catch (Exception exception) when (exception is UpdateSecurityException or JsonException or InvalidDataException)
        {
            logger?.LogError(exception, "Launcher update manifest source failed security validation. Source={Source}", source.Name);
            return ManifestSourceResult.Invalid(source, exception.Message);
        }
    }

    private async Task<byte[]> DownloadBytesAsync(Uri uri, int maximumBytes, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DefaultRequestTimeout);
        try
        {
            using var response = await OfficialUpdateHttp.SendAsync(
                httpClient, uri, OfficialUpdateUriKind.Manifest, timeout.Token).ConfigureAwait(false);
            return await OfficialUpdateHttp.ReadLimitedBytesAsync(response, maximumBytes, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateSourceUnavailableException("The update manifest source timed out.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new UpdateSourceUnavailableException("The update manifest source could not be read.", exception);
        }
    }

    private static LauncherUpdateCheckResult CreateResult(
        string currentVersion,
        int currentVersionCode,
        RemoteUpdateManifestDto manifest)
    {
        if (manifest.VersionCode <= currentVersionCode)
            return LauncherUpdateCheckResult.Latest(currentVersion);
        var asset = manifest.Assets.Single();
        var urls = asset.Urls.OrderBy(url => url.Priority)
            .Select(url => new LauncherUpdateDownloadUrl(url.Name.Trim(), url.Url.Trim(), url.Priority))
            .ToArray();
        var versionName = manifest.VersionName.Trim();
        return LauncherUpdateCheckResult.Available(currentVersion, new LauncherUpdateInfo(
            versionName,
            versionName,
            LauncherProjectLinks.GitHubReleasesUrl,
            urls[0].Url,
            string.IsNullOrWhiteSpace(manifest.ReleaseNotes) ? null : manifest.ReleaseNotes,
            asset.FileName.Trim(),
            LauncherUpdateAssetKind.WindowsX64Executable,
            asset.Size,
            asset.Sha256.Trim().ToLowerInvariant(),
            VersionCode: manifest.VersionCode,
            IsMandatory: manifest.Mandatory || currentVersionCode < manifest.MinSupportedVersionCode,
            MinSupportedVersionCode: manifest.MinSupportedVersionCode,
            PublishedAt: manifest.PublishedAt,
            DownloadUrls: urls));
    }

    private static void ValidateManifest(RemoteUpdateManifestDto manifest, string expectedChannel)
    {
        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.AppId?.Trim(), "CubeLauncher", StringComparison.Ordinal)
            || !string.Equals(manifest.Channel?.Trim(), expectedChannel, StringComparison.OrdinalIgnoreCase)
            || manifest.VersionCode < 0
            || string.IsNullOrWhiteSpace(manifest.VersionName)
            || manifest.Assets.Count != 1)
        {
            throw new UpdateSecurityException("The update manifest metadata is invalid.");
        }

        var asset = manifest.Assets[0];
        if (!string.Equals(asset.Platform?.Trim(), "windows", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(asset.Arch?.Trim(), "x64", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(asset.PackageType?.Trim(), "exe", StringComparison.OrdinalIgnoreCase)
            || asset.Size <= 0
            || !IsHex64(asset.Sha256)
            || string.IsNullOrWhiteSpace(asset.FileName)
            || !string.Equals(Path.GetFileName(asset.FileName), asset.FileName, StringComparison.Ordinal)
            || !asset.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || asset.Urls.Count == 0)
        {
            throw new UpdateSecurityException("The update executable metadata is invalid.");
        }

        foreach (var url in asset.Urls)
        {
            if (string.IsNullOrWhiteSpace(url.Name)
                || !Uri.TryCreate(url.Url, UriKind.Absolute, out var uri))
                throw new UpdateSecurityException("The update download URL is invalid.");
            OfficialUpdateHttp.ValidateInitialUri(uri, OfficialUpdateUriKind.Executable);
        }
    }

    private static bool IsHex64(string? value) => value?.Trim() is { Length: 64 } text && text.All(Uri.IsHexDigit);

    private static void EnsureDefaultHeaders(HttpClient client)
    {
        LauncherHttpUserAgent.Apply(client);
        if (!client.DefaultRequestHeaders.Accept.Any(header => header.MediaType == "application/json"))
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public static bool TryCalculateVersionCode(string? value, out int versionCode)
    {
        versionCode = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        var metadataIndex = text.IndexOf('+');
        if (metadataIndex >= 0) text = text[..metadataIndex];

        // 不要求 tag 以 v 开头：直接找第一段 x.y[.z]，前面带 CubeLauncher-、release_ 之类也能解析。
        var match = VersionPattern.Match(text);
        if (!match.Success) return false;

        var betaRevision = 99;
        var suffix = text[(match.Index + match.Length)..];
        if (suffix.StartsWith("-beta.", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(suffix["-beta.".Length..], out betaRevision)
                || betaRevision is <= 0 or > 98) return false;
        }
        else if (suffix.StartsWith('-'))
        {
            // -rc / -alpha / -preview 等其它预发布后缀不参与正式版比较。
            return false;
        }

        if (!int.TryParse(match.Groups["major"].Value, out var major)
            || !int.TryParse(match.Groups["minor"].Value, out var minor))
            return false;

        var patch = 0;
        if (match.Groups["patch"].Success
            && !int.TryParse(match.Groups["patch"].Value, out patch)) return false;

        if (major is < 0 or > 99 || minor is < 0 or > 99 || patch is < 0 or > 99) return false;
        versionCode = major * 1_000_000 + minor * 10_000 + patch * 100 + betaRevision;
        return true;
    }

    internal sealed class RemoteUpdateManifestDto
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
        [JsonPropertyName("appId")] public string? AppId { get; init; }
        [JsonPropertyName("channel")] public string? Channel { get; init; }
        [JsonPropertyName("versionName")] public string VersionName { get; init; } = string.Empty;
        [JsonPropertyName("versionCode")] public int VersionCode { get; init; }
        [JsonPropertyName("publishedAt")] public DateTimeOffset? PublishedAt { get; init; }
        [JsonPropertyName("mandatory")] public bool Mandatory { get; init; }
        [JsonPropertyName("minSupportedVersionCode")] public int MinSupportedVersionCode { get; init; }
        [JsonPropertyName("releaseNotes")] public string? ReleaseNotes { get; init; }
        [JsonPropertyName("assets")] public List<RemoteUpdateAssetDto> Assets { get; init; } = [];
    }

    internal sealed class RemoteUpdateAssetDto
    {
        [JsonPropertyName("platform")] public string? Platform { get; init; }
        [JsonPropertyName("arch")] public string? Arch { get; init; }
        [JsonPropertyName("packageType")] public string? PackageType { get; init; }
        [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
        [JsonPropertyName("size")] public long Size { get; init; }
        [JsonPropertyName("sha256")] public string Sha256 { get; init; } = string.Empty;
        [JsonPropertyName("urls")] public List<RemoteUpdateUrlDto> Urls { get; init; } = [];
    }

    internal sealed class RemoteUpdateUrlDto
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("url")] public string Url { get; init; } = string.Empty;
        [JsonPropertyName("priority")] public int Priority { get; init; }
    }

    /// <summary>
    /// GitHub Releases API 的返回结构（只声明用到的字段）。
    /// </summary>
    internal sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("body")] public string? Body { get; init; }
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; init; }
        [JsonPropertyName("assets")] public List<GitHubReleaseAssetDto> Assets { get; init; } = [];
    }

    internal sealed class GitHubReleaseAssetDto
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("size")] public long Size { get; init; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; init; }
        [JsonPropertyName("digest")] public string? Digest { get; init; }
    }

    /// <summary>
    /// GitHub Tags API 的返回结构（只声明用到的字段）。
    /// </summary>
    internal sealed class GitHubTagDto
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
    }

    private sealed record ReleaseCandidate(GitHubReleaseDto Release, int VersionCode);

    private enum ManifestSourceStatus { Valid, Unavailable, Invalid }
    private sealed record ManifestSourceResult(
        LauncherUpdateManifestSource Source,
        ManifestSourceStatus Status,
        RemoteUpdateManifestDto? Manifest,
        string? FailureReason)
    {
        public static ManifestSourceResult Valid(LauncherUpdateManifestSource source, RemoteUpdateManifestDto manifest) =>
            new(source, ManifestSourceStatus.Valid, manifest, null);
        public static ManifestSourceResult Unavailable(LauncherUpdateManifestSource source, string reason) =>
            new(source, ManifestSourceStatus.Unavailable, null, reason);
        public static ManifestSourceResult Invalid(LauncherUpdateManifestSource source, string reason) =>
            new(source, ManifestSourceStatus.Invalid, null, reason);
    }
}

public sealed record LauncherUpdateManifestSource(string Name, string UrlTemplate, int Priority)
{
    public static IReadOnlyList<LauncherUpdateManifestSource> DefaultSources { get; } =
    [
        new("gitee", LauncherProjectLinks.GiteeUpdateManifestUrlTemplate, 1),
        new("github", LauncherProjectLinks.GitHubUpdateManifestUrlTemplate, 2)
    ];

    public string CreateManifestUrl(string channel) => string.Format(
        System.Globalization.CultureInfo.InvariantCulture, UrlTemplate, channel);
}
