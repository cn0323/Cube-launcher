# Cube Launcher

[中文](#中文) | [English](#english)

## 中文

### 简介

`Cube Launcher` 是一款面向 Minecraft Java 版玩家的现代化启动器，提供从游戏下载、实例管理、账户登录到资源安装、联机和故障诊断的一体化体验。界面简洁直观，支持丰富的个性化设置，让日常启动和游戏内容管理更加轻松。

注意：`Cube Launcher` 是独立开发的第三方开源启动器，并非 Minecraft 官方产品，也未经 Microsoft、Mojang Studios 或其他第三方平台赞助、认可或背书。

### 主要功能

#### 游戏安装与启动

* 安装原版 Minecraft，以及 `Fabric`、`Forge`、`NeoForge` 和 `Quilt` 加载器。
* 管理下载任务，支持下载源、并发线程和速度限制设置。
* 自动检查并补全缺失或损坏的游戏文件。
* 展示完整的启动进度与状态，并在启动失败时提供原因分析和诊断报告。

#### 实例管理

* 创建、重命名、删除和启动多个独立游戏实例。
* 为每个实例单独配置 Java、内存、窗口、启动参数和前后置命令。
* 备份与恢复实例，并可自定义备份目录。
* 导入本地游戏和整合包，也可将实例导出为整合包。

#### 账户与角色

* 支持 Microsoft 正版账户、离线账户和第三方认证账户。
* 管理多个账户并快速切换当前角色。
* 支持账户重命名、头像显示、皮肤与披风管理。
* 首页头像即账户页入口，点击直达；账户页的皮肤预览可按住左右拖动旋转。
* 提供多种离线账户 UUID 生成方式。
* 如果您尚未拥有正版 Minecraft，请始终优先考虑购入正版  Minecraft。

#### 资源中心

* 浏览和搜索 Mod、资源包、光影包、世界与整合包。
* 支持 Modrinth 和 CurseForge，并可按游戏版本、加载器、来源及分类筛选。
* 查看项目详情、可用版本和依赖关系，将兼容资源直接安装到指定实例。
* 自动检查缺失的前置 Mod，并辅助完成依赖安装。
* 管理本地 Mod、存档、资源包和光影包，支持拖放导入、搜索、批量启用、禁用与删除。

#### 整合包与服务器

* 安装和导入 Modrinth、CurseForge 整合包。
* 导出实例内容，便于分享和迁移游戏配置。
* 将支持的整合包直接部署为独立 Minecraft 服务端。
* 对受分发限制的文件提供清晰的手动补全指引。

#### 联机

联机页分为 **陶瓦联机** 与 **红石联机** 两个分区：

* 陶瓦联机（Terracotta）：自动发现本机已开放到局域网的世界，创建房间并生成房间代码，或通过房间代码加入其他玩家，可查看房间状态与成员。
* 红石联机：面向第三方中继服务的说明与官网入口，不装模组也能把本机端口映射成公网地址。

#### 外观与设置

* 支持深色、浅色和跟随系统主题，并可在运行时切换。
* 提供多种强调色、亚克力背景、自定义图片背景和循环播放的视频背景（可调音量）。
* 主界面标题栏可一键切换深色 / 浅色主题；正版登录已内置可用的 Microsoft 应用 ID，开箱即用，无需自行注册 Azure 应用，并提供内置说明文档。
* 支持简体中文、繁體中文、English 和日本語。
* 自动发现、下载和管理 Java，也可手动导入本地 Java。
* 支持全局及实例独立内存设置、Minecraft 目录管理和启动器更新。

### 系统要求

* Windows 10 / 11
* Minecraft Java Edition
* .NET 8 Desktop Runtime

### 发布更新（维护者）

启动器的「检查更新」直接读取本仓库的 GitHub Releases，不需要额外维护清单文件：

1. 修改 `Launcher.App/Launcher.App.csproj` 里的 `<Version>`（例如 `1.2.0`）
2. 用发布配置构建单文件 exe（`BuildSingleRelease.bat` 或 `dotnet publish`）
3. 在 GitHub 创建 Release，tag 必须能解析为 `v主.次.修订`，例如 `v1.2.0`
4. 上传构建出的 `CubeLauncher_x64.exe`（文件名含 `CubeLauncher_x64` 的资产优先被选中）
5. 发布说明会原样显示在更新对话框里，建议按时写更新日志
6. 不要勾选「Draft」；预发布版本请勾选「Pre-release」，它只对 beta 渠道可见

GitHub 会为上传的资产自动计算 sha256，启动器用它校验下载结果，校验不通过不会替换程序。
如果某个 Release 没有可安装的 exe 或缺少 sha256，更新对话框会退化为「前往下载页」。

\---

## English

### Overview

`Cube Launcher` is a modern launcher for Minecraft: Java Edition. It brings game installation, instance management, account sign-in, resource installation, multiplayer, and troubleshooting together in one streamlined experience. Its clean interface and flexible personalization options make everyday launching and content management simple.

Note: `Cube Launcher` is an independently developed, third-party open-source launcher. It is not an official Minecraft product and is not sponsored, approved, or endorsed by Microsoft, Mojang Studios, or any other third-party platform.

### Features

#### Game Installation and Launching

* Install vanilla Minecraft with `Fabric`, `Forge`, `NeoForge`, or `Quilt`.
* Manage download tasks and configure download sources, concurrency, and speed limits.
* Automatically detect and restore missing or damaged game files.
* Follow detailed launch progress and receive clear analysis and diagnostic reports when a launch fails.

#### Instance Management

* Create, rename, delete, and launch multiple independent game instances.
* Configure Java, memory, window options, launch arguments, and pre-launch or post-exit commands per instance.
* Back up and restore instances with a configurable backup location.
* Import local games and modpacks, or export an instance as a modpack.

#### Accounts and Profiles

* Use Microsoft, offline, and third-party authentication accounts.
* Manage multiple accounts and quickly switch the active profile.
* Rename accounts and manage avatars, skins, and capes.
* Choose from multiple offline UUID generation methods.
* If you do not yet own a legitimate copy of Minecraft, please prioritize purchasing one.

#### Resource Center

* Browse and search mods, resource packs, shader packs, worlds, and modpacks.
* Access Modrinth and CurseForge content with filters for game version, loader, source, and category.
* View project details, available versions, and dependencies, then install compatible content directly into an instance.
* Detect missing mod dependencies and assist with installing them.
* Manage local mods, saves, resource packs, and shader packs with drag-and-drop import, search, and batch actions.

#### Modpacks and Servers

* Install and import Modrinth and CurseForge modpacks.
* Export instance content for sharing or migration.
* Deploy supported modpacks as standalone Minecraft servers.
* Receive clear instructions when distribution-restricted files must be supplied manually.

#### Multiplayer

* Automatically discover Minecraft worlds opened to LAN on the local computer.
* Create a multiplayer room and share its code, or join another player with a room code.
* View room status and members for quick cross-network play.

#### Appearance and Settings

* Use dark, light, or system themes with runtime switching.
* Choose from multiple accent colors, acrylic backgrounds, custom image backgrounds, and looping video backgrounds with adjustable volume.
* Switch between dark and light themes right from the title bar; genuine Microsoft sign-in works out of the box with a built-in app ID, with an in-app guide for using your own.
* Available in Simplified Chinese, Traditional Chinese, English, and Japanese.
* Automatically discover, download, and manage Java, or import a local Java installation manually.
* Configure global and per-instance memory, manage the Minecraft directory, and receive launcher updates.

### System Requirements

* Windows 10 / 11
* Minecraft: Java Edition
* .NET 8 Desktop Runtime

### Publishing an update (maintainers)

The in-app update check reads this repository's GitHub Releases directly, so no separate manifest file is needed:

1. Bump `<Version>` in `Launcher.App/Launcher.App.csproj` (for example `1.2.0`)
2. Build the single-file executable with the release profile
3. Create a GitHub Release with a tag that parses as `vMAJOR.MINOR.PATCH` (for example `v1.2.0`)
4. Upload `CubeLauncher_x64.exe`; assets whose names contain `CubeLauncher_x64` are preferred
5. The release body is shown verbatim in the update dialog as release notes
6. Leave "Draft" unchecked; flag pre-releases so they appear only on the beta channel

GitHub computes a sha256 digest for every uploaded asset, and the launcher verifies the download before replacing itself.
