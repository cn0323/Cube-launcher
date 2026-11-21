# 正版登录与应用 ID 说明

## 一句话：现在不用注册 Azure 了

启动器已经内置一个可用的 Microsoft 应用 ID。直接打开 **账户 → 正版登录**，用你的微软账号登录即可，不需要去 Azure 门户注册应用，也不需要填任何东西。

应用 ID 只是告诉微软「是哪个程序在请求登录」，它不是账号密码，也不会影响你的 Minecraft 正版授权。

> 如果登录时提示「没有购买 Minecraft」或「无正版授权」，说明这个微软账号本身没有购买游戏，与应用 ID 无关。

## 内置的应用 ID 是哪来的

内置值 `c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb` 来自开源启动器 **Prism Launcher** 公开源码中的 `Launcher_MSA_CLIENT_ID`。它已被 Microsoft / Minecraft 服务允许，所以能直接完成正版登录。

需要如实说明三点：

- 这是**别人注册的应用 ID**，使用它意味着所有登录请求都会记在对方名下，且需遵守 [Microsoft Identity Platform 使用条款](https://docs.microsoft.com/en-us/legal/microsoft-identity-platform/terms-of-use)。
- 该 ID 有可能被撤销或限制，届时登录会失效。出现这种情况时，按下文换成自己的应用 ID 即可。
- 如果你是本启动器的维护者并打算正式发布，建议申请一个属于自己的应用 ID，不要长期依赖公共值。

## 登录时会发生什么

点「正版登录」后，启动器用系统浏览器打开微软登录页，输入账号密码并同意授权即可。启动器不会看到你的密码。

登录过程中会向微软申请两个权限：

- `XboxLive.signin`：证明账号拥有 Xbox Live 身份，显示皮肤、加入服务器都需要它
- `offline_access`：下次打开启动器时免密刷新登录状态

## 可选：换成你自己的应用 ID

只有当你想让登录请求归属到自己的应用（例如正式发行）时才需要做这一步。普通玩家跳过了也不影响使用。

### 两个必须先知道的坑

**坑一：个人微软账号默认进不了 Azure 门户。**
Outlook / Hotmail / Xbox 这类个人账号不隶属任何 Entra 目录，直接登录 `portal.azure.com` 常报 `AADSTS16000`、`AADSTS160021` 或「所选用户账户在租户 Microsoft Services 中不存在」，根本到不了「应用注册」。解决办法是先免费建一个目录：

1. 打开 https://entra.microsoft.com ，用同一个微软账号登录
2. 进入 **Microsoft Entra ID → 管理租户 → 创建**，选择「Microsoft Entra ID」，填好名称创建（免费，不需要 Azure 订阅或信用卡）
3. 右上角头像 → **切换目录**，切到刚建的租户
4. 再进入 **Microsoft Entra ID → 应用注册 → 新注册**

**坑二：新注册的应用 ID 会被 Minecraft 服务拒绝。**
Mojang 对 Java 版游戏服务 API 启用了白名单。新 Client ID 在最后一步 `login_with_xbox` 会返回 `403 Invalid app registration`，这不是 Azure 配置错了，改重定向 URI 或权限都解决不了。必须提交审核：

- 申请表：https://aka.ms/mce-reviewappid
- 官方说明：https://help.minecraft.net/hc/en-us/articles/16254801392141
- 需要填：你的应用 ID、应用名称、项目地址、用途说明
- 审核通常数天到数周，通过后最多再等 24 小时生效

所以「自己注册一个就能用」是行不通的，这也是本启动器改为内置公共 ID 的原因。

### 注册与配置步骤

1. 按上面「坑一」建好租户并切换目录
2. **新注册**：名称随意；受支持的账户类型选包含「个人 Microsoft 账户」的那一项；重定向 URI 平台选「公共客户端/本机(移动和桌面)」，地址填 `http://localhost`（不要带端口、路径或结尾斜杠）
3. 左侧 **身份验证** → 把「允许公共客户端流」设为 **是** → 保存
4. **概述** 页复制「应用程序(客户端) ID」，形如 `12345678-1234-1234-1234-1234567890ab`
5. 提交 https://aka.ms/mce-reviewappid 审核，等通过
6. 把 ID 交给启动器，二选一：
   - 双击发行包里的 `启用正版登录.bat`，粘贴 ID 回车
   - 或手工新建文件 `%APPDATA%\CL\microsoft-client-id`，内容只有那一行 ID，不要引号和空格（注意去掉 `.txt` 后缀）

写好后重启启动器即可生效；删除这个文件就会回到内置公共 ID。

其他受支持的位置：`CubeLauncher_x64.exe` 同目录下的 `microsoft-client-id`、`CL\microsoft-client-id`、`.local-secrets\microsoft-client-id`，或环境变量 `CUBELAUNCHER_MICROSOFT_CLIENT_ID`。

## 常见报错对照

- **打开 Azure 门户报 `AADSTS16000` / `AADSTS160021` / 账户不在 Microsoft Services 租户**：个人账号没有 Entra 目录，按「坑一」先建租户并切换目录。
- **`403 Invalid app registration` / 应用未授权**：新应用 ID 未过 Mojang 白名单，按「坑二」提交审核；或删掉自定义配置改用内置 ID。
- **`AADSTS50011` 重定向地址不匹配**：平台没选「公共客户端/本机」，或地址不是 `http://localhost`。
- **`AADSTS7000218` 缺少客户端密钥**：没开启「允许公共客户端流」，改为「是」并保存。
- **提示未配置应用 ID**：自定义文件内容不是合法 GUID；或内置值被破坏，重装启动器。
- **登录成功但进游戏提示没有正版授权**：该账号本身没买 Minecraft，应用 ID 不解决游戏授权。
- **内置公共 ID 突然登不上**：该 ID 可能被撤销，按上文换成自己的应用 ID。

## 安全说明

- 启动器只在本地把你填的 ID 用于向微软发起登录请求，不会上传到第三方服务器。
- 应用 ID 是公开标识符，不是密钥；真正的凭据由微软在你的设备上保存，启动器接触不到密码。
- 撤销授权：访问 https://account.live.com/consent/Manage 移除对应应用。
