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

namespace Launcher.Application.Services.Ai;

/// <summary>
/// 未配置在线模型时的本地知识库。这里收录的是启动器自身的操作知识，
/// 用关键词打分匹配，保证断网、没填 API Key 时助手仍然可用。
/// 条目内容属于产品知识而非界面文案，因此以常量形式维护；
/// 界面上的提示语仍走资源文件。
/// </summary>
internal static class OfflineAssistantKnowledge
{
    private sealed record Entry(string[] Keywords, string Answer);

    private const int MinimumScore = 2;

    private static readonly Entry[] Entries =
    [
        new(
            ["java", "jdk", "jre", "运行时", "版本不匹配", "unsupported class"],
            """
            不同游戏版本需要的 Java 版本不一样，装错了会直接闪退或报 UnsupportedClassVersionError：

            • 1.21 及以上 → Java 21
            • 1.20.5 ~ 1.20.6 → Java 21
            • 1.18 ~ 1.20.4 → Java 17
            • 1.17 → Java 16 或 17
            • 1.16.5 及更早 → Java 8

            在「设置 → Java」里可以让启动器自动扫描本机 Java；也可以在「实例设置 → Java 与内存」里给单个实例单独指定。
            启动器内置了 Java 下载，扫描不到合适版本时点一下就能装。
            """),

        new(
            ["内存", "分配", "卡顿", "掉帧", "outofmemory", "内存不足", "heap"],
            """
            内存给太多反而更卡，因为 GC 回收时间会变长。推荐值：

            • 8 GB 物理内存 → 分配 3 ~ 4 GB
            • 16 GB → 分配 6 ~ 8 GB
            • 32 GB 及以上 → 分配 8 ~ 10 GB

            不要超过物理内存的 60%。原版 1.20 分配 2 GB 就够，装了几百个 Mod 再往上加。
            位置：「实例设置 → Java 与内存」，也可以在「设置 → 启动内存」里改默认值。

            如果还是卡，优先装优化 Mod（Sodium / Lithium），比加内存有效得多。
            """),

        new(
            ["mod", "模组", "冲突", "崩溃", "闪退", "报错", "crash", "不兼容", "依赖"],
            """
            排查 Mod 问题的顺序（从快到慢）：

            1. 看崩溃报告：实例设置里可以打开日志目录，latest.log 末尾通常直接写着哪个 Mod 出错。
            2. 二分法定位：把 Mod 分成两半，先只留一半启动，能进游戏说明问题在另一半，反复对半缩小范围。
            3. 缺依赖是最常见的原因，Forge 缺前置会在日志里写 Missing or unsupported mandatory dependencies。
            4. 确认加载器没混装：Forge 的 Mod 放进 Fabric 实例一定崩，两者不能共存。
            5. 检查 Mod 与游戏版本是否对应，1.20.1 的 Mod 丢进 1.21 基本都会崩。

            启动器在启动前会做一次文件校验，缺库文件会自动补，但 Mod 之间的兼容性需要你自己判断。
            """),

        new(
            ["整合包", "modpack", "curseforge", "装包", "整合"],
            """
            安装整合包有两种方式：

            • 在线：左侧「下载」→ 切到「整合包」→ 搜索名字 → 选版本 → 安装，启动器会自动下载依赖和配置。
            • 本地：直接把 .zip 整合包拖进启动器窗口，会弹出导入确认；也可以在「下载」页用「导入本地整合包」。

            安装完成后实例会出现在「游戏设置」列表里，第一次启动前建议先确认 Java 版本是否匹配。
            整合包体积大，如果下载慢，到「设置 → 下载」把下载源切成 BMCLAPI。
            """),

        new(
            ["下载慢", "速度", "镜像", "bmclapi", "下载源", "卡住"],
            """
            下载慢通常是源的问题，不是网络本身：

            • 「设置 → 下载 → 下载源」切换到 BMCLAPI，国内速度一般能快几倍。
            • 并发数调高到 64 左右，线程太多反而会被限速。
            • 如果某一步长时间卡在 0%，多半是单个文件超时，点重试即可，已下载的文件不会重下。
            • 公司/校园网可能拦了部分域名，可以试试换网络。

            下载任务在左侧「下载任务」里能看到每个文件的进度和失败原因。
            """),

        new(
            ["皮肤", "skin", "披风", "cape", "换皮肤"],
            """
            换皮肤：「账户」页选中账户 → 右侧「更换皮肤」→ 上传 64×64 的 PNG，或者直接把图片拖到左侧预览区。
            左侧大图可以按住拖动，上下左右都能看，右下角会同步预览披风。

            注意：正版账户上传皮肤会同步到 Mojang 服务器，进服务器别人也能看到；
            离线账户的皮肤只在本机生效。
            """),

        new(
            ["正版", "登录", "微软", "账号", "账户", "离线登录", "认证"],
            """
            账户页支持两种账户：

            • 微软正版：点「添加账户」→ 微软登录，会打开浏览器授权，登录后能进正版服务器。
            • 离线账户：只填用户名，适合单机和本地联机，不能进正版服。

            如果微软登录一直转圈，多半是网络问题，可以挂代理后重试；令牌过期时启动器会自动刷新，
            刷新失败会提示重新登录，不会影响已安装的游戏。
            """),

        new(
            ["联机", "多人", "服务器", "好友", "terracotta", "局域网"],
            """
            联机有两种：

            • 加入服务器：「多人游戏」里填地址，或者从实例设置里配置默认自动加入的服务器。
            • 创建房间：「多人游戏 → 创建房间」，走 Terracotta 中转，不需要公网 IP，把房间号发给朋友即可。
              第一次使用需要同意一份协议。

            双方的游戏版本和 Mod 必须一致，否则会提示协议不兼容。
            """),

        new(
            ["光影", "shader", "optifine", "iris", "画质"],
            """
            光影需要先装渲染前置：

            • Fabric 实例 → 装 Iris
            • Forge 实例 → 装 OptiFine（或 Oculus）

            前置装好后，把光影包（.zip）放到实例目录的 shaderpacks 文件夹，
            或者在「资源 → 光影」里直接安装。进游戏后在「选项 → 视频设置 → 光影」里选择。

            光影很吃显卡，集显建议先用低配光影试。
            """),

        new(
            ["备份", "存档", "回滚", "恢复", "world"],
            """
            备份在「实例设置 → 备份」里，可以手动备份，也可以在启动前自动备份。

            存档目录默认在实例目录的 saves 文件夹，启动器「资源 → 世界」里可以直接打开。
            误删了存档先别写盘，去备份列表里找最近的还原点。

            切换 Minecraft 目录时启动器不会动原目录里的文件，换回来还在。
            """),

        new(
            ["更新", "升级", "新版本", "检查更新"],
            """
            启动器默认在启动时检查更新，也可以在「设置 → 关于」里手动点「检查更新」。

            更新会替换启动器本体，不会动你的实例、存档和账户。
            如果更新后打不开，可以在设置里回退，或者重新下载完整包覆盖安装。
            """),

        new(
            ["你是", "你是谁", "名字", "介绍一下自己", "cubi", "小库", "助手"],
            """
            我是 Cubi（小库），Cube Launcher 内置的助手。

            现在处于离线模式：没有配置在线模型，我只能回答启动器操作、Java、内存、Mod 排查这类内置知识。

            想让我回答更多问题，去「AI 助手 → 设置」填一个兼容 OpenAI 协议的接口地址、密钥和模型名，
            之后就能随便问——写配置、改 Mod、解释崩溃日志、翻译英文文档都行。
            """),

        new(
            ["你好", "hi", "hello", "在吗", "嗨"],
            """
            在的。有什么要处理的？

            启动器相关的问题可以直接问，比如「1.20.1 该用哪个 Java」「装了几百个 Mod 还是很卡怎么办」「这个崩溃日志什么意思」。
            """)
    ];

    private static readonly string[] Fallback =
    [
        """
        这个问题超出了我的离线知识范围。

        离线模式下我能回答：Java 版本、内存分配、Mod 冲突排查、整合包安装、下载速度、皮肤披风、账户登录、联机、光影、备份和更新。

        去「AI 助手 → 设置」填一个在线模型（兼容 OpenAI 协议），我就能回答任何问题。
        """
    ];

    /// <summary>
    /// 按关键词命中数打分，取得分最高且达到阈值的条目。
    /// </summary>
    public static string Answer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Fallback[0];

        var normalized = question.ToLowerInvariant();
        var bestScore = 0;
        string? bestAnswer = null;

        foreach (var entry in Entries)
        {
            var score = 0;
            foreach (var keyword in entry.Keywords)
            {
                if (normalized.Contains(keyword, StringComparison.Ordinal))
                    score += keyword.Length >= 3 ? 2 : 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestAnswer = entry.Answer;
            }
        }

        return bestScore >= MinimumScore ? bestAnswer! : Fallback[0];
    }

    /// <summary>
    /// 首次进入页面时的欢迎语。它与具体问题无关，因此单独提供。
    /// </summary>
    public static string Greeting =>
        """
        我是 Cubi，Cube Launcher 的助手。

        可以直接问我：Java 该装哪个版本、内存怎么分、Mod 冲突怎么排查、整合包怎么装、启动崩了怎么看日志。

        想聊别的也行——去「设置」填一个在线模型，我就能回答任何问题。
        """;

    /// <summary>
    /// 建议问题，供空状态下的快捷按钮使用。
    /// </summary>
    public static IReadOnlyList<string> Suggestions { get; } =
    [
        "1.20.1 该用哪个 Java 版本？",
        "装了很多 Mod 很卡，内存怎么分？",
        "游戏一启动就闪退，怎么排查？",
        "整合包怎么安装？"
    ];
}
