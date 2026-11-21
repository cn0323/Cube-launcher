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

namespace Launcher.Application;

public static class LauncherProjectLinks
{
    public const string GitHubOwner = "cn0323";
    public const string GitHubRepositoryName = "Cube-launcher";
    public const string GitHubRepositoryUrl = "https://github.com/" + GitHubOwner + "/" + GitHubRepositoryName;
    // 使用 HEAD 引用，默认分支无论是 master 还是 main 都能正确解析。
    public const string GitHubLicenseUrl = GitHubRepositoryUrl + "/blob/HEAD/LICENSE";

    /// <summary>
    /// 版权声明正文：仓库 main 分支根目录的「Cube launcher版权声明.txt」。
    /// 中文文件名在链接里必须保留百分号编码，否则 GitHub 会 404。
    /// </summary>
    public const string GitHubCopyrightNoticeUrl =
        GitHubRepositoryUrl + "/blob/main/Cube%20launcher%E7%89%88%E6%9D%83%E5%A3%B0%E6%98%8E.txt";

    /// <summary>
    /// 开源协议与第三方组件声明：仓库 main 分支根目录的「Cube Launcher 软件声明.txt」。
    /// </summary>
    public const string GitHubSoftwareStatementUrl =
        GitHubRepositoryUrl + "/blob/main/Cube%20Launcher%20%E8%BD%AF%E4%BB%B6%E5%A3%B0%E6%98%8E.txt";
    /// <summary>
    /// 红石联机（第三方联机中继服务）官网。
    /// </summary>
    public const string RedstoneMultiplayerUrl = "https://hongshi.site/";
    public const string MinecraftPurchaseUrl = "https://www.minecraft.net/store/minecraft-java-bedrock-edition-pc";
    public const string GitHubFeatureSuggestionsUrl = GitHubRepositoryUrl + "/issues/new?labels=enhancement";
    public const string GitHubIssuesUrl = GitHubRepositoryUrl + "/issues/new?labels=bug";
    public const string GitHubReleasesUrl = GitHubRepositoryUrl + "/releases";

    /// <summary>官网地址：更新失败或用户想手动下载时跳这里。</summary>
    public const string OfficialWebsiteUrl = "https://cn0323.github.io/Cube-launcher/";

    /// <summary>带下载锚点的官网地址：点「打开官网更新」直接落到下载区。</summary>
    public const string OfficialWebsiteDownloadUrl = OfficialWebsiteUrl + "#download";

    /// <summary>带更新日志锚点的官网地址：弹窗里的「打开更新日志」落到官网更新记录。</summary>
    public const string OfficialWebsiteChangelogUrl = OfficialWebsiteUrl + "#changelog";

    /// <summary>引用项目：BlockHelmLauncher，部分界面布局参考了它。</summary>
    public const string BlockHelmProjectUrl = "https://github.com/zqq-699/BlockHelm-Launcher";
    public const string GitHubReleasesApiUrl = "https://api.github.com/repos/" + GitHubOwner + "/" + GitHubRepositoryName + "/releases";
    // Releases 为空时用它兜底，覆盖“只打 tag 没发 Release”的情况。
    public const string GitHubTagsApiUrl = "https://api.github.com/repos/" + GitHubOwner + "/" + GitHubRepositoryName + "/tags";
    public const string GitHubUserAgent = "CubeLauncher";
    public const string GiteeOwner = "cn0323";
    public const string GiteeRepositoryName = "Cube-launcher";
    public const string GiteeRepositoryUrl = "https://gitee.com/" + GiteeOwner + "/" + GiteeRepositoryName;
    public const string GiteeUpdateManifestUrlTemplate = GiteeRepositoryUrl + "/raw/update-manifests/update/{0}/latest.json";
    public const string GitHubUpdateManifestUrlTemplate = "https://raw.githubusercontent.com/" + GitHubOwner + "/" + GitHubRepositoryName + "/update-manifests/update/{0}/latest.json";
}
