/* ==========================================================================
   Cube Launcher — 官网交互
   ========================================================================== */

(() => {
  "use strict";

  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

  /* ---------------------------- 滚动揭示 ---------------------------- */
  const revealNodes = document.querySelectorAll(".reveal");

  if (reduceMotion || !("IntersectionObserver" in window)) {
    revealNodes.forEach((n) => n.classList.add("in"));
  } else {
    const io = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          entry.target.classList.add("in");
          io.unobserve(entry.target);
        });
      },
      { rootMargin: "0px 0px -12% 0px", threshold: 0.12 }
    );
    revealNodes.forEach((n) => io.observe(n));
  }

  /* ---------------------------- 滚动进度与导航 ---------------------------- */
  const nav = document.getElementById("nav");
  const progress = document.getElementById("progress");
  let ticking = false;

  const onScroll = () => {
    const y = window.scrollY || 0;
    const max = document.documentElement.scrollHeight - window.innerHeight;
    const p = max > 0 ? Math.min(1, y / max) : 0;

    if (progress) progress.style.setProperty("--p", p.toFixed(4));
    if (nav) nav.classList.toggle("scrolled", y > 18);
    ticking = false;
  };

  window.addEventListener(
    "scroll",
    () => {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(onScroll);
    },
    { passive: true }
  );
  onScroll();

  /* ---------------------------- 深色 / 浅色主题 ---------------------------- */
  const root = document.documentElement;
  const themeToggle = document.getElementById("themeToggle");
  const themeColorMeta = document.getElementById("themeColorMeta");

  const applyTheme = (theme, persist) => {
    const next = theme === "dark" ? "dark" : "light";
    root.setAttribute("data-theme", next);
    if (themeColorMeta) {
      themeColorMeta.setAttribute("content", next === "dark" ? "#070a10" : "#f7f8fb");
    }
    if (themeToggle) {
      themeToggle.setAttribute("aria-pressed", String(next === "dark"));
      themeToggle.title = next === "dark" ? "切换到浅色主题" : "切换到深色主题";
    }
    if (persist) {
      try { localStorage.setItem("cube-theme", next); } catch (e) { /* 无痕模式忽略 */ }
    }
  };

  applyTheme(root.getAttribute("data-theme"), false);

  if (themeToggle) {
    themeToggle.addEventListener("click", () => {
      // 过渡只在切换瞬间加，平时不加，免得滚动时掉帧
      root.classList.add("theme-anim");
      window.setTimeout(() => root.classList.remove("theme-anim"), 420);
      applyTheme(root.getAttribute("data-theme") === "dark" ? "light" : "dark", true);
    });
  }

  // 用户没手动选过的话，跟着系统走
  const schemeQuery = window.matchMedia("(prefers-color-scheme: dark)");
  schemeQuery.addEventListener?.("change", (e) => {
    let saved = null;
    try { saved = localStorage.getItem("cube-theme"); } catch (err) { /* ignore */ }
    if (!saved) applyTheme(e.matches ? "dark" : "light", false);
  });

  /* ---------------------------- 跟随鼠标的光 ---------------------------- */
  if (finePointer && !reduceMotion) {
    document.body.classList.add("has-pointer");

    const spot = document.getElementById("spotlight");
    // 再慢一点：光斑是"荡"过去的，不是黏在指针上
    const ease = 0.085;
    let raf = 0;
    let targetX = 0;
    let targetY = 0;
    let currentX = 0;
    let currentY = 0;
    let primed = false;

    const paint = () => {
      raf = 0;
      if (!primed) {
        currentX = targetX;
        currentY = targetY;
        primed = true;
      } else {
        currentX += (targetX - currentX) * ease;
        currentY += (targetY - currentY) * ease;
      }

      if (spot) {
        const rect = spot.getBoundingClientRect();
        spot.style.setProperty("--mx", `${currentX - rect.left}px`);
        spot.style.setProperty("--my", `${currentY - rect.top}px`);
      }

      // 还差得远就继续追，追上了就停下来，别空转 rAF
      if (Math.abs(targetX - currentX) > 0.25 || Math.abs(targetY - currentY) > 0.25)
        raf = requestAnimationFrame(paint);
    };

    window.addEventListener(
      "pointermove",
      (e) => {
        targetX = e.clientX;
        targetY = e.clientY;
        if (!raf) raf = requestAnimationFrame(paint);
      },
      { passive: true }
    );
  }

  /* ---------------------------- 卡片冷光跟随 ---------------------------- */
  document.querySelectorAll("[data-sheen]").forEach((card) => {
    if (!finePointer) return;
    card.addEventListener(
      "pointermove",
      (e) => {
        const r = card.getBoundingClientRect();
        card.style.setProperty("--x", `${((e.clientX - r.left) / r.width) * 100}%`);
        card.style.setProperty("--y", `${((e.clientY - r.top) / r.height) * 100}%`);
      },
      { passive: true }
    );
  });

  /* ---------------------------- 界面示意图的倾斜 ---------------------------- */
  const tilt = document.getElementById("tilt");

  if (tilt && finePointer && !reduceMotion) {
    const MAX = 7;

    const reset = () => {
      tilt.style.setProperty("--rx", "0deg");
      tilt.style.setProperty("--ry", "0deg");
    };

    tilt.addEventListener(
      "pointermove",
      (e) => {
        const r = tilt.getBoundingClientRect();
        const px = (e.clientX - r.left) / r.width - 0.5;
        const py = (e.clientY - r.top) / r.height - 0.5;
        tilt.style.setProperty("--ry", `${(px * MAX * 2).toFixed(2)}deg`);
        tilt.style.setProperty("--rx", `${(-py * MAX * 1.4).toFixed(2)}deg`);
      },
      { passive: true }
    );

    tilt.addEventListener("pointerleave", reset);
  }

  /* ---------------------------- 数字滚动 ---------------------------- */
  const counters = document.querySelectorAll("[data-count]");

  const animateCount = (el) => {
    const target = parseFloat(el.dataset.count);
    if (!Number.isFinite(target)) return;
    const decimals = (el.dataset.count.split(".")[1] || "").length;
    const duration = 1100;
    const start = performance.now();

    const step = (now) => {
      const t = Math.min(1, (now - start) / duration);
      const eased = 1 - Math.pow(1 - t, 3);
      el.textContent = (target * eased).toFixed(decimals);
      if (t < 1) requestAnimationFrame(step);
      else el.textContent = target.toFixed(decimals);
    };

    requestAnimationFrame(step);
  };

  if (counters.length) {
    if (reduceMotion || !("IntersectionObserver" in window)) {
      counters.forEach((el) => (el.textContent = el.dataset.count));
    } else {
      const co = new IntersectionObserver(
        (entries) => {
          entries.forEach((entry) => {
            if (!entry.isIntersecting) return;
            animateCount(entry.target);
            co.unobserve(entry.target);
          });
        },
        { threshold: 0.6 }
      );
      counters.forEach((el) => co.observe(el));
    }
  }

  /* ---------------------------- 界面截图切换 ---------------------------- */
  const stageName = document.getElementById("stageName");
  const stageIndex = document.getElementById("stageIndex");
  const shotTabs = Array.from(document.querySelectorAll(".shot-tab"));
  const stageImages = Array.from(document.querySelectorAll(".stage-img"));

  if (shotTabs.length && stageImages.length >= 2) {
    let active = 0;
    let token = 0;

    const showShot = (tab, order) => {
      const src = tab.getAttribute("data-src");
      const label = tab.getAttribute("data-label") || "";
      const alt = tab.querySelector(".tab-name")?.textContent?.trim() || label;
      const incoming = stageImages[1 - active];
      const outgoing = stageImages[active];
      const mine = ++token;

      const swap = () => {
        if (mine !== token) return;
        incoming.classList.add("on");
        outgoing.classList.remove("on");
        active = 1 - active;
      };

      if (incoming.getAttribute("src") !== src) {
        incoming.setAttribute("src", src);
        incoming.setAttribute("alt", alt);
        if (incoming.complete && incoming.naturalWidth) {
          swap();
        } else {
          incoming.addEventListener("load", swap, { once: true });
        }
      } else {
        swap();
      }

      shotTabs.forEach((t) => {
        const on = t === tab;
        t.classList.toggle("active", on);
        t.setAttribute("aria-selected", String(on));
      });

      if (stageName) stageName.textContent = label;
      if (stageIndex) stageIndex.textContent = String(order + 1);
    };

    shotTabs.forEach((tab, i) => {
      tab.addEventListener("click", () => showShot(tab, i));
      tab.addEventListener("keydown", (e) => {
        if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
        e.preventDefault();
        const next = (i + (e.key === "ArrowRight" ? 1 : -1) + shotTabs.length) % shotTabs.length;
        shotTabs[next].focus();
        showShot(shotTabs[next], next);
      });
    });
  }

  /* ---------------------------- 深浅主题对比滑杆 ---------------------------- */
  const ba = document.querySelector(".ba");

  if (ba) {
    const range = ba.querySelector(".ba-range");
    const setPos = (v) => ba.style.setProperty("--pos", `${v}%`);

    if (range) {
      setPos(range.value);
      range.addEventListener("input", () => setPos(range.value));
    }
  }

  /* ---------------------------- 更新日志（读 Releases） ---------------------------- */
  const logList = document.getElementById("logList");

  const FALLBACK = [
    {
      tag: "v1.3.0",
      name: "Cube Launcher 1.3.0",
      date: "2026-11-28",
      body: "## 侧边导航换成液态玻璃\n\n参考图里那种「胶囊 + 一颗放大镜」的做法：\n\n- 胶囊底色改成通透的磨砂白（深色主题是烟熏玻璃），加了顶部亮带、右缘折射带和 1.2px 的白色描边，玻璃有了厚度感。\n- 选中项不再是一块贴在胶囊里的灰片，而是一颗 **96px 的玻璃珠**：探出胶囊两侧，里面是放大 1.5 倍的图标和标题，边缘一圈淡青紫的虹彩，顶部一道弧光。\n- 选中项本体在胶囊里隐藏，只由玻璃珠显示，所以不会出现「一个图标两份」的重影。\n\n## 侧边栏动画\n\n| 时机 | 效果 |\n|---|---|\n| 进入 | 胶囊从左滑入并淡入，收尾带一点回弹 |\n| 悬停 | 图标放大到 1.09，`BackEase` 轻轻弹一下 |\n| 切页 | 玻璃珠滑过去，520ms，尾段回弹，像珠子停住 |\n\n## 首次启动向导\n\n全新安装第一次打开会先走三步，走完才进主界面：\n\n1. **语言** — 简体中文 / 繁體中文 / 日本語 / English\n2. **外观** — 浅色 / 深色 / 跟随系统，每张卡片带预览\n3. **完成** — 提示接下来该做什么（登录账号、选 Minecraft 目录）\n\n右上角可以跳过，跳过的选择也会记下来。老用户升级不会看到这个界面——只在配置文件是新建的时候弹。\n\n## 官网\n\n- **光效变淡**：光斑透明度压到原来的一半左右，从三层加到四层，最远一层铺到 1500px，边缘彻底化开。\n- **过渡更顺**：跟随系数从 0.15 降到 0.085，光斑是「荡」过去的，不再贴着指针；淡入时长 0.7s → 1.05s。\n- **只显示当前版本**：更新日志只渲染最新一条，历史版本留在 GitHub。\n\n## 其它\n\n- 版本号 1.3.0（版本码 `1030099`）\n- 侧边栏仍然固定，不可拖动\n"
    },
    {
      tag: "v1.2.8",
      name: "Cube Launcher 1.2.8",
      date: "2026-11-21",
      body: "## 修复\n\n- **深色主题左侧出现一条白条**：窗口本身没有铺底色，壁纸和液态玻璃都没开时这块空白会露出系统窗口的白色，右侧页面却是深色，看起来像被劈成两半。现在底色落到窗口自己身上（`Brush.Page.Background`），并给根层加了 8px 圆角裁剪，四角不会露出直角。\n- **圆片和胶囊糊成一片**：导航透镜之前借用的是全局玻璃画刷 `Brush.LiquidGlass.Tint`，改一处会连带整个窗口的玻璃底色一起变。现在拆成独立画刷 `Brush.Dock.Lens`，两套主题各管各的。\n\n## 调整\n\n| | 之前 | 现在 |\n|---|---|---|\n| 白色主题 · 胶囊 | `#D6FDFEFF` | `#E6FDFEFF`（更白一点，圆片压得住） |\n| 白色主题 · 选中圆片 | `#F0E8E6E2` 近白 | `#F2CCC8C1` 中性暖灰，明显比胶囊深 |\n| 深色主题 · 选中圆片 | `#ED3A3E45` | `#ED454A52`，比胶囊亮一档 |\n| 圆片内部高光 | `#4DFFFFFF` | `#33FFFFFF`，去掉那股发白的浮光 |\n| 全局玻璃底色 | 被改成实心灰 | 恢复通透（白 `#1FFFFFFF` / 深 `#2612C2FF`） |\n\n白色主题下选中项现在是白胶囊里一块明确的灰片，不再是一团看不清的浅影；深色主题下也还是\"亮一档\"的关系，没有变成白斑。\n\n## 其它\n\n- 版本号 1.2.8（版本码 `1020899`）\n- 侧边栏仍然固定，不可拖动\n"
    },
    {
      tag: "v1.2.7",
      name: "Cube Launcher 1.2.7",
      date: "2026-11-21",
      body: "# Cube Launcher 1.2.7\n\n这一版把左侧导航的交互收回来，也把选中那块圆片调成了参考图的样子；官网的光效重新做了渐变。\n\n## 改进\n\n- **侧边栏固定在左边**：长按不再进入拖动，胶囊不会再被拖到窗口中间；之前拖过的话，启动后自动回到左侧默认位置。\n- **选中圆片加重**：圆片从「几乎透明的一层玻璃」改成一块实心的中性灰（浅色暖灰、深色烟灰），图标和文字压在上面更清楚。\n- **去掉蓝色描边**：原来圆片边缘有一圈青紫折射，现在换成很淡的中性白边，只保留上缘那道白色弧光，和参考图一致。\n- **官网光效更顺**：跟随鼠标的光斑改成三层渐变（光核、中层晕、远处尾光），衰减接近高斯，不再出现一圈硬边；鼠标移动改为缓动追随，不会一格一格地跳。\n- **图片不再串色**：光斑的层级压到内容下面，截图、封面这类不透明图片会整块盖住它，白色主题下 multiply 不会再给图片染上一层蓝灰。\n\n## 下载说明\n\n- `CubeLauncher_x64.exe`：单文件，**框架依赖**，需要先装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。\n- `CubeLauncher-win64.zip`：exe + 使用说明 + 正版登录说明，解压即用。\n- `CubeLauncher-1.2.7-source.zip`：完整源码（GPL-3.0），同时已同步到仓库 `main`。\n\n> 1.2.6 及更早的构建，在「设置 → 关于 → 检查更新」里能看到 1.2.7 并一键升级。\n"
    },
    {
      tag: "v1.2.6",
      name: "Cube Launcher 1.2.6",
      date: "2026-11-20",
      body: "# Cube Launcher 1.2.6\n\n这一版只做一件事：把软件内所有「官网 / 更新 / 下载」出口统一到新官网 `https://cn0323.github.io/Cube-launcher/`，不再把用户送到 GitHub 页面。\n\n## 改进\n\n- **「打开官网更新」直达下载区**：设置 → 关于，以及更新弹窗里的「去官网更新」，点击后打开新官网的下载区（`#download`），版本号、系统要求和三个安装包在一屏内就能看到。\n- **「打开更新日志」改看官网**：更新弹窗里的按钮现在打开新官网的更新记录区（`#changelog`），和官网首屏的版本号保持一致，不再跳到 GitHub Release。\n- **无法自动安装时也走官网**：当远端版本缺少可校验的安装包时，点确认不再落到 GitHub 资产直链，而是打开官网下载区，让用户自己挑 exe 或压缩包。\n\n## 修复\n\n- 单元测试里的自更新服务桩缺少进度参数，导致 `Launcher.sln` 整体编译失败；补齐签名后解决方案恢复可编译。\n\n## 下载说明\n\n- `CubeLauncher_x64.exe`：单文件，**框架依赖**，需要先装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。\n- `CubeLauncher-win64.zip`：exe + 使用说明 + 正版登录说明，解压即用。\n- `CubeLauncher-1.2.6-source.zip`：完整源码（GPL-3.0）。\n\n> 1.2.5 及更早的构建，在「设置 → 关于 → 检查更新」里能看到 1.2.6 并一键升级。\n"
    },
    {
      tag: "v1.2.5",
      name: "Cube Launcher 1.2.5",
      date: "2026-11-20",
      body: "# Cube Launcher 1.2.5\n\n这一版动了启动器的左侧导航，官网也一起换了皮。\n\n## 启动器\n\n- **侧边栏改成悬浮胶囊**：左侧不再是一整条贴边的面板，而是一条浮在壁纸上的竖向玻璃胶囊，图标和文字都在里面。\n- **液态玻璃透镜**：选中项上压着一块圆形玻璃片，切换页面时会顺着胶囊滑过去，带一点回弹。\n- **长按整条可以拖**：按住胶囊约 0.4 秒进入拖动，能把它挪到窗口任意位置，松手不会误触导航。\n\n## 官网\n\n- **深浅主题可切换**：右上角多了一个太阳 / 月亮按钮，选择会记住；没手动选过的时候跟随系统。\n- **跟随鼠标的光效加深**：原来那层几乎看不见，现在换成内外两圈，浅色下压深、深色下发亮，文字依旧清楚。\n- **网址换成 https://cn0323.github.io/Cube-launcher/**：和启动器仓库统一，旧地址仍然能打开。\n\n## 下载说明\n\n- `CubeLauncher_x64.exe`：单文件，**框架依赖**，需要先装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。\n- `CubeLauncher-win64.zip`：exe + 使用说明 + 正版登录说明，解压即用。\n- `CubeLauncher-1.2.5-source.zip`：完整源码（GPL-3.0）。\n\n> 1.2.4 及更早的构建，在「设置 → 关于 → 检查更新」里能直接看到 1.2.5 并在启动器内一键升级。"
    },
    {
      tag: "v1.2.4",
      name: "Cube Launcher 1.2.4",
      date: "2026-11-18",
      body: "# Cube Launcher 1.2.4\n\n这一版只改了译文，没有动功能。\n\n## 文案\n\n- **英文**：「打开官网更新」的按钮改成 `Update from website`；模型设置的说明不再用 `unlock unlimited questions` 这种广告腔，改成 `Point it at any OpenAI-compatible endpoint and you can ask anything.`；离线模式副标题也去掉 `unlock everything`。\n- **英文细节**：等待上一则回答的提示补成完整句子，`Fill in endpoint, key and model first` 补上冠词，`Partial UI` 改成 `UI preview`。\n- **日文**：「公式サイトの更新を開く」改成「公式サイトから更新」，「一部 UI」改成「UI プレビュー」，引用项目的致谢改成「公開してくださった作者に感謝します」。\n- **繁体中文**：「開啟官網更新」改成「前往官網更新」，和弹窗统一。\n\n## 其它\n\n- 版本号 1.2.4，版本码 1020499，界面和 1.2.3 完全一样。不切英文或日文的话，用起来没有区别。"
    },
    {
      tag: "v1.2.3",
      name: "Cube Launcher 1.2.3",
      date: "2026-11-10",
      body: "# Cube Launcher 1.2.3\n\n这一版只动了两个地方：侧边栏的选中样式，和更新入口。\n\n## 界面\n\n- **去掉任务栏选项的纯色底板**：左侧导航选中时不再铺一整块灰色底，改成图标左侧一根蓝色细指示条，选中项更轻，也不再压住液态玻璃。\n\n## 更新\n\n- **新增「打开官网更新」按钮**：设置 → 关于，版本号和「检查更新」旁边多了一个按钮，点一下直接用浏览器打开官网的下载区，不用先关掉启动器。\n- **更新弹窗里的入口更好找**：弹窗底部的按钮原来挤在一起，宽度由 560 调到 648，两个出口都能完整显示。\n\n## 下载说明\n\n- `CubeLauncher_x64.exe`：单文件，**框架依赖**，需要先装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。\n- `CubeLauncher-win64.zip`：exe + 使用说明 + 正版登录说明，解压即用。\n- `CubeLauncher-1.2.3-source.zip`：完整源码（GPL-3.0）。\n\n> 1.2.2 及更早的构建，在「设置 → 关于 → 检查更新」里能直接看到 1.2.3 并在启动器内一键升级。"
    },
    {
      tag: "v1.2.2",
      name: "Cube Launcher 1.2.2",
      date: "2026-11-10",
      body: "# Cube Launcher 1.2.2\n\n这一版基本都在修上一版的问题，顺手把更新流程做顺了。\n\n## 修复\n\n- **AI 助手打不了字**：设置页的通用标题层没给 AI 页让位，盖在聊天页上吃掉了鼠标点击。现在 AI 页独占输入，进页面自动聚焦输入框。\n- **模型设置关不掉**：右上角加了关闭按钮，底部加「取消」，按 Esc 或点空白处都能退出。\n- **服务商列表叠在一起**：下拉弹层换成可换行的胶囊按钮，缩放和不同 DPI 下也不会错位。\n\n## 更新\n\n- **在启动器里下载**：点更新后弹窗内直接显示百分比进度，不再跳浏览器。\n- **忽略此版本**：忽略后启动时的静默检查不再提醒，手动检查仍会显示。\n- **去官网更新**：自动更新失败或资产不可用时，一键打开官网下载页。\n- **自动覆盖旧版本**：下载校验 sha256 后替换当前 exe 并重启，旧文件自动备份。\n- **更新通道只保留正式版**：预发布版本不再参与自动更新。\n\n## 界面\n\n- **液态玻璃更接近苹果**：背景单独做全局动态模糊（半径缓慢呼吸），叠上色散描边和指针柔光。\n- **侧边栏去掉焦点虚线框**：点完左边图标不再留一圈蓝框。\n- **引用项目加 BlockHelm**：设置 → 关于的引用项目里加了 BlockHelmLauncher 的部分界面示意，可直接打开项目主页。\n- **移除添加卡片**：主页不再有长按添加卡片和一排按钮，卡片保持固定排列（仍可长按拖动换位）。\n- **AI 助手名字统一为 Cubi**：侧边栏、标题、提示语全部对齐。\n\n## 下载说明\n\n- `CubeLauncher_x64.exe`：单文件，**框架依赖**，需要先装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。\n- `CubeLauncher-win64.zip`：exe + 使用说明 + 正版登录说明，解压即用。\n- `CubeLauncher-1.2.2-source.zip`：完整源码（GPL-3.0）。\n\n> 1.2.1 及更早的构建，在「设置 → 关于 → 检查更新」里能直接看到 1.2.2 并一键升级。"
    },
    {
      tag: "v1.2.1",
      name: "Cube Launcher 1.2.1",
      date: "2026-11-09",
      body: "# Cube Launcher 1.2.1\n\n1.2.1 的核心是内置 AI 助手 **Cubi**。同时把开发中完成的运行态面板、主页卡片和账号页皮肤区一起并进 1.2 系列，版本号回到 1.2。\n\n## 新增\n\n- **AI 助手 Cubi**：侧边栏新增入口，打开就是聊天界面。可以直接问 Java 版本、内存分配、Mod 冲突、整合包安装、崩溃排查，也能问别的。\n  - 没配模型时走**内置离线知识库**，断网也能答；\n  - 在「模型设置」里填一个兼容 OpenAI 协议的接口（DeepSeek、通义千问、Kimi、智谱、硅基流动、Ollama 等），就能问任何问题；\n  - 对话会自动带上启动器版本、Minecraft 目录和默认内存，回答贴着实际配置走；\n  - 密钥只写在本地 settings.json，支持「测试连接」和一键停止生成。\n- **游戏运行态**：启动成功后，首页启动卡片切换为「已运行时长 + 实时网速 + 停止游戏」。上行下行分开显示，每秒刷新一次；点「停止游戏」会结束整个进程树，玩家自己关掉游戏时面板也会自动复原。\n- **主页小卡片**：长按任意首页卡片约 0.4 秒进入编辑态，可把联机、下载、资源、设置加进首页，同样支持拖动排序。\n- **账号页大图皮肤**：皮肤预览移到左侧并放大，按住可以上下左右拖动查看，披风同框显示在右下角；原来的按钮收敛成一个「更换皮肤」入口。\n\n## 改进\n\n- **液态玻璃**：面板更透，边缘加了青紫色散的透镜描边，覆盖层新增跟随鼠标的柔光反射。\n- **应用图标加入字母 C**：立方体正面上叠加白色 C，重新导出全部尺寸与 ico。\n- **侧边栏图标**：选中项加圆角强调色底板，图标尺寸统一收小。\n\n## 修复\n\n- **拖动开始游戏按钮闪退**：拖拽结束时不再把 RenderTransform 写成单位矩阵，改为清除本地值；坐标换算在元素被移出可视树时返回无效值并跳过当前帧；所有拖拽事件都加了异常边界，界面线程另有一层兜底。\n\n## 下载说明\n\n- `CubeLauncher_x64.exe`：单文件，**框架依赖**，需要先装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。\n- `CubeLauncher-win64.zip`：exe + 使用说明 + 正版登录说明，解压即用。\n- `CubeLauncher-1.2.1-source.zip`：完整源码（GPL-3.0）。\n\n> 1.2.0 及更早的构建，在「设置 → 关于 → 检查更新」里能直接看到 1.2.1 并一键升级（下载后先校验 sha256 再替换程序并重启）。"
    },
    {
      tag: "v1.2.0",
      name: "Cube Launcher 1.2.0",
      date: "2026-11-07",
      body: "版本号进入 1.2 系列。重点是启动之后的体验：游戏跑起来以后首页变成实时状态面板。\n\n### 新增\n\n- **游戏运行态**：启动成功后首页切换为「已运行时长 + 实时网速 + 停止游戏」，停止时结束整个进程树。\n- **主页小卡片**：长按任意卡片进入编辑态，可把联机、下载、资源、设置加进首页，同样支持拖动排序。\n- **账号页大图皮肤**：左侧大预览支持上下左右拖动，披风同框显示。\n\n### 改进\n\n- **液态玻璃更像苹果**：边缘色散描边、顶面斜光，覆盖层新增跟随鼠标的柔光反射。\n- **图标加入字母 C**，侧边栏选中项加圆角强调色底板。\n\n### 修复\n\n- **拖动开始游戏按钮闪退**：修正 RenderTransform 恢复方式，坐标换算加入无效值保护，拖拽事件全部加异常边界。\n\n> 1.2.0 及更早的构建，在「设置 → 关于 → 检查更新」里能直接看到 1.2.1 并一键升级。"
    }
  ];

  const escapeHtml = (s) =>
    s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

  const inline = (s) =>
    escapeHtml(s)
      .replace(/\*\*([^*]+)\*\*/g, "<b>$1</b>")
      .replace(/`([^`]+)`/g, "<code>$1</code>")
      .replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');

  const stripDuplicateTitle = (raw, tag) => {
    const lines = String(raw || "").split(/\r?\n/);
    const idx = lines.findIndex((l) => l.trim());
    if (idx === -1) return raw;
    const first = lines[idx].trim();
    if (!/^#{1,6}\s+/.test(first)) return raw;
    const text = first.replace(/^#{1,6}\s+/, "").toLowerCase();
    const version = String(tag || "").replace(/^v/i, "").toLowerCase();
    if (text.includes("cube launcher") || (version && text.includes(version))) {
      lines.splice(idx, 1);
      return lines.join("\n");
    }
    return raw;
  };

  const renderBody = (raw) => {
    const out = [];
    const lines = String(raw || "").split(/\r?\n/);
    let inList = false;
    let inQuote = false;

    const closeList = () => {
      if (inList) {
        out.push("</ul>");
        inList = false;
      }
      if (inQuote) {
        out.push("</blockquote>");
        inQuote = false;
      }
    };

    for (const line of lines) {
      const t = line.trim();
      if (!t) {
        closeList();
        continue;
      }
      if (/^#{1,6}\s+/.test(t)) {
        closeList();
        out.push(`<h4>${inline(t.replace(/^#{1,6}\s+/, ""))}</h4>`);
        continue;
      }
      if (/^[-*+]\s+/.test(t)) {
        if (!inList) {
          out.push("<ul>");
          inList = true;
        }
        out.push(`<li>${inline(t.replace(/^[-*+]\s+/, ""))}</li>`);
        continue;
      }
      if (/^>\s?/.test(t)) {
        closeList();
        if (!inQuote) {
          out.push("<blockquote>");
          inQuote = true;
        }
        out.push(`<p>${inline(t.replace(/^>\s?/, ""))}</p>`);
        continue;
      }
      if (inQuote) {
        out.push("</blockquote>");
        inQuote = false;
      }
      closeList();
      out.push(`<p>${inline(t)}</p>`);
    }
    closeList();
    if (inQuote) out.push("</blockquote>");
    return out.join("");
  };

  const formatDate = (value) => {
    if (!value) return "";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return value;
    const pad = (n) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  };

  const plainPeek = (raw) => {
    const text = String(raw || "")
      .split(/\r?\n/)
      .map((l) => l.replace(/^[>#\-*+\s]+/, "").replace(/\*\*/g, "").replace(/`/g, ""))
      .join(" ")
      .replace(/\s+/g, " ")
      .trim();
    return text.length > 96 ? `${text.slice(0, 96)}…` : text;
  };

  const renderLogs = (items) => {
    if (!logList) return;

    logList.innerHTML = items
      .map((it, i) => {
        const open = i === 0;
        const body = renderBody(stripDuplicateTitle(it.body, it.tag));
        return `
      <article class="log-item reveal in${open ? " is-open" : ""}">
        <button class="log-head" type="button" aria-expanded="${open}">
          <span class="log-tag">${escapeHtml(it.tag)}</span>
          <span class="log-name">${escapeHtml(it.name || "")}</span>
          ${open ? '<span class="log-new">最新</span>' : ""}
          <span class="log-date">${escapeHtml(it.date || "")}</span>
          <span class="log-chev" aria-hidden="true">
            <svg viewBox="0 0 24 24" width="14" height="14"><path fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="m6 9 6 6 6-6"/></svg>
          </span>
        </button>
        <div class="log-peek">${escapeHtml(plainPeek(it.body))}</div>
        <div class="log-clip">
          <div class="log-inner">
            <div class="log-body">${body}</div>
          </div>
        </div>
      </article>`;
      })
      .join("");

    logList.querySelectorAll(".log-item").forEach((item) => {
      const head = item.querySelector(".log-head");
      if (!head) return;
      head.addEventListener("click", () => {
        const willOpen = !item.classList.contains("is-open");
        // 手风琴：展开一条时收起其它，免得整页都是日志
        logList.querySelectorAll(".log-item").forEach((other) => {
          if (other === item) return;
          other.classList.remove("is-open");
          other.querySelector(".log-head")?.setAttribute("aria-expanded", "false");
        });
        item.classList.toggle("is-open", willOpen);
        head.setAttribute("aria-expanded", String(willOpen));
      });
    });
  };

  const loadLogs = async () => {
    if (!logList) return;
    try {
      const res = await fetch(
        "https://api.github.com/repos/cn0323/Cube-launcher/releases?per_page=30",
        { headers: { Accept: "application/vnd.github+json" } }
      );
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      if (!Array.isArray(data) || data.length === 0) throw new Error("empty");

      const items = data
        .filter((r) => !r.draft)
        .map((r) => ({
          tag: r.tag_name || "",
          name: r.name || "",
          date: formatDate(r.published_at),
          body: r.body || ""
        }));

      if (items.length === 0) throw new Error("empty");

      // 首页只放当前版本这一条，历史改动交给 GitHub
      renderLogs(items.slice(0, 1));

      const dateEl = document.getElementById("releaseDate");
      if (dateEl && items[0].date) dateEl.textContent = items[0].date;
    } catch (err) {
      renderLogs(FALLBACK.slice(0, 1));
    }
  };

  /* ---------------------------- 宣传片 ---------------------------- */
  const filmModal = document.getElementById("filmModal");
  const filmVideo = document.getElementById("filmVideo");
  const filmPlay = document.getElementById("filmPlay");
  let filmLoaded = false;

  const openFilm = () => {
    if (!filmModal || !filmVideo) return;
    if (!filmLoaded) {
      filmVideo.src = filmVideo.dataset.src;
      filmLoaded = true;
    }
    filmModal.classList.add("open");
    filmModal.setAttribute("aria-hidden", "false");
    document.body.classList.add("film-open");
    const playing = filmVideo.play();
    if (playing && playing.catch) playing.catch(() => {});
  };

  const closeFilm = () => {
    if (!filmModal || !filmVideo) return;
    filmModal.classList.remove("open");
    filmModal.setAttribute("aria-hidden", "true");
    document.body.classList.remove("film-open");
    filmVideo.pause();
    filmVideo.currentTime = 0;
  };

  if (filmPlay) filmPlay.addEventListener("click", openFilm);
  if (filmModal) {
    filmModal.querySelectorAll("[data-close]").forEach((el) => el.addEventListener("click", closeFilm));
  }
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && filmModal && filmModal.classList.contains("open")) closeFilm();
  });

  loadLogs();
})();
