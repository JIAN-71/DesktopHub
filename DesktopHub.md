# DesktopHub 项目主文档（整合版）

> **本文档整合自原 `README.md`、`overview.md`、`ARCHITECTURE.md`、`HANDOFF_ZCODE.md` 四份文档（2026-09-05），去重重组为一册。**
> **内容以 `src/` 源码为准**——整合时已核对代码，凡与代码冲突的旧文档描述均标注为"历史方案"，勿据此改代码。
> 原四份文档已整合入本文，并已于 2026-09-05 删除；本文件是项目唯一文档入口，建议读完本文再动手改码。

---

## 目录

1. 项目定位
2. 技术栈与开发环境
3. 功能特性
4. 构建 / 运行 / 测试
5. 架构分层与依赖规则（硬性）
6. 模块职责速查
7. 关键技术决策
8. 胶囊窗（Pill）设计与实现
9. 表情球（EmotionBall）
10. 背景模糊 / 亚克力演进史（★以代码为准）
11. 近期改动与修复记录（时间线）
12. 测试现状
13. 代码核对发现与技术债清理（2026-08-31 复审）
14. 已知限制与待办 / 后续方向
15. 开发约定（改代码前必看）
16. 注意事项与维护约定

---

## 1. 项目定位

**DesktopHub** —— Windows 桌面图标面板（灵动岛式 Dock）：把桌面上的**全部图标**（用户桌面、公共桌面、以及"此电脑 / 回收站 / 控制面板"等系统图标，与资源管理器所见完全一致）收纳进屏幕顶部居中的"胶囊岛"，按分类以小图标网格归纳到展开面板中。

- **桌面上的任何文件都不会被移动或修改**；桌面图标可以隐藏，面板就是你的桌面入口。
- 胶囊窗常驻屏幕顶部居中，显示**当前时间**与**表情球**；跨屏跟随（鼠标移到哪块屏幕，胶囊即平移到该屏顶部居中）。

## 2. 技术栈与开发环境

| 项 | 值 |
|---|---|
| 平台 | Windows（实测 Win10 19045 / Win11 22H2 兼容分支） |
| 框架 | WPF / .NET 8（`net8.0-windows`） |
| MVVM | CommunityToolkit.Mvvm（`[ObservableProperty]` / `RelayCommand`） |
| 语言风格 | C# 13（file-scoped namespace、required、`(_, _)` 丢弃） |
| 本机 SDK | `C:\Users\yezi\.dotnet\dotnet.exe`（**PATH 里的 dotnet 只是运行时，构建必须用全路径**，SDK 8.0.424） |
| 显示缩放 | 开发机 150% DPI（注意物理像素换算） |
| 图标来源 | [aora-bot/emotion-ball](https://github.com/sam70361/aora-bot)（社区许可，出处已在文件头注明） |

## 3. 功能特性

- **完整扫描**：枚举 Shell 桌面命名空间，与资源管理器看到的桌面完全一致——含公共桌面图标与系统图标（此电脑、回收站、控制面板等），一个不漏。
- **分类分组**：内置 应用程序 / 游戏 / 文件夹 / 网页快捷方式 / 系统 / 其他 分组；支持自定义关键字与目标路径规则；分组只影响面板显示。
- **胶囊窗（灵动岛）**：小胶囊常驻屏幕顶部居中，显示当前时间与表情球；表情球播放待机动画（呼吸、眨眼、眼环池轮换、自旋），**点击自旋一圈**；**悬停/触碰时向右展开"最近打开"扩展条**（最近经胶囊启动的 3 个软件，**重启后保留**，单击即可再次启动）；**点击胶囊才展开完整分组面板**；双击图标直接启动（系统图标经 `explorer shell:::` 打开）；鼠标离开自动收起。
- **一键隐藏桌面图标**：胶囊面板与托盘菜单都有开关，调用系统"显示桌面图标"切换（等效桌面右键 → 查看），立即生效、随时切回。
- **自动刷新**：监听用户/公共桌面文件变化，图标增删后面板自动更新；也可手动刷新。
- **托盘常驻**：重新扫描 / 隐藏桌面图标 / 设置 / 退出；开机自启（HKCU Run 键）。
- **音乐前置区**：悬停胶囊展开 [前置区 | 最近打开条]——存在媒体会话时前置区(表情球+时间的位置)切换为 **圆形专辑封面**(44) + 歌名/歌手 + 播放⇄暂停按钮(右侧)；无会话保持表情球+时间；鼠标离开收回小胶囊。
- **外观设置（设置页即时生效）**：主题三态（跟随系统 / 深色 / 浅色，启动时按配置解析）、亚克力开关、不透明度滑杆（0~100%，拖动实时生效、停止 500ms 后落盘）；三项即时生效并自动保存，退出时兜底再固化一次。设置窗口自身也随主题换色（DynamicResource）。
- **主题跟随**：`ThemeManager` 跟随系统深浅色（注册表 `AppsUseLightTheme`），亚克力着色同步切换；`AcrylicEnabled` / `AcrylicOpacity` 由设置状态驱动、改动即广播重设（详见 §10）。

## 4. 构建 / 运行 / 测试

需要 Windows + .NET 8 SDK（本机 `C:\Users\yezi\.dotnet`，未加入 PATH 时先 `set PATH=%USERPROFILE%\.dotnet;%PATH%`；**推荐直接用全路径**）：

```bash
cd /d/Project/DesktopHub
"C:/Users/yezi/.dotnet/dotnet.exe" build DesktopHub.sln
"C:/Users/yezi/.dotnet/dotnet.exe" run --project src/DesktopHub.App
```

- 编译产物：`src/DesktopHub.App/bin/Debug/net8.0-windows/DesktopHub.App.exe`，双击即可运行。
- 单实例：Mutex `Local\DesktopHub.SingleInstance`；已在运行时再次启动会弹"已经运行"。
- 自测钩子已全部移除（`DH_ANIM_SELFTEST` 相关代码不存在了，勿找）。

运行全部测试（两个工程）：

```bash
"C:/Users/yezi/.dotnet/dotnet.exe" test tests/DesktopHub.Core.Tests
"C:/Users/yezi/.dotnet/dotnet.exe" test tests/DesktopHub.App.Tests
```

## 5. 架构分层与依赖规则（硬性）

```
┌─────────────────────────────────────────────────────────────┐
│ DesktopHub.App（WPF 前端，薄 UI 层）                          │
│  Views · ViewModels · UI 服务(Tray/AutoStart/ScreenInterop)  │
├─────────────────────────────────────────────────────────────┤
│ DesktopHub.Core（业务逻辑，无 WPF 依赖）                       │
│  扫描编排 · 分类 · 分组排序 · 变化监听 · 配置读写              │
├─────────────────────────────────────────────────────────────┤
│ DesktopHub.Shell（Win32/COM 互操作，零外部依赖）              │
│  Shell 枚举 · 图标提取 · 显隐切换 · 快捷方式解析               │
└─────────────────────────────────────────────────────────────┘
```

- 允许方向：`App → Core → Shell`。**禁止反向引用**（Shell 不依赖 Core/App，Core 不依赖 App）。
- `App` 允许直接引用 `Shell`，但**仅限纯 Win32 适配**（如显隐切换）；业务编排必须经 `Core`。
- **`Core` 不得出现 WPF 类型**（`ImageSource`、`Dispatcher`、`Application` 等），保证 Core 可脱离 UI 独立测试。
- **`Core` 不得泄漏 Win32 句柄**（`IntPtr HICON` 等）；句柄生命周期只属于 Shell 提取者与 App 适配者。
- 句柄契约：`IconExtractor.Extract` 返回的 HICON 必须成对 `Free`（`IconService.ExtractIcon` 的 `finally` 已落实）——**新增取图逻辑必须遵守，防 GDI 句柄泄漏。**

## 6. 模块职责速查

### 6.1 DesktopHub.Shell（基础设施）

| 类型 | 职责 |
|---|---|
| `DesktopShell` | 用 `Shell.Application` COM（IDispatch 路线）枚举桌面命名空间，返回纯数据 `DesktopShellItem`（含 Kind 判定） |
| `IconExtractor` | `SHGetFileInfo(SHGFI_SYSICONINDEX)` 拿图标索引 + `SHGetImageList(SHIL_EXTRALARGE)` 取 **48×48** HICON；系统项 `::{CLSID}` 先 `SHParseDisplayName` 解析 PIDL。`Extract` / `Free` 成对使用 |
| `DesktopIcons` | 向桌面 `SHELLDLL_DefView` 发送 `WM_COMMAND 0x7402` 切换图标显隐（Progman 下找不到时回退遍历 WorkerW） |
| `LinkResolver` | `WScript.Shell` 晚绑定解析 `.lnk` 目标；`.url` 直接读 `URL=` 行 |
| `DesktopPaths` | 用户桌面 / 公共桌面路径 |

> 历史：`BackdropBlur.cs`（2026-09-03 早期 blurbehind 方案）已删除，见 §10。

### 6.2 DesktopHub.Core（业务逻辑，无 WPF）

| 类型 | 职责 |
|---|---|
| `DesktopIconScanner` | 扫描编排：枚举 → 解析 .lnk → 分类，产出纯数据 `DesktopItem` |
| `ClassificationEngine` | 分类：自定义规则优先（关键字/路径片段），按类型回退（Url/Folder/Application/System），兜底「其他」 |
| `IconGrouper` | 分组与排序：**以 `AppConfig.Categories` 顺序为单一事实来源**，未知分类排最后 |
| `DesktopChangeWatcher` | `FileSystemWatcher` 监听用户/公共桌面，2s 去抖后发出变更信号（仅信号，不碰文件） |
| `ConfigStore` | `%APPDATA%\DesktopHub\config.json` 读写；加载时自动补齐缺失的内置分类（兼容旧配置）；`AppConfig.SchemaVersion`（当前 1） |
| `RecentStore` | `%APPDATA%\DesktopHub\recent.json` 读写（"最近打开"的 Shell 解析名列表，最新在前）；文件缺失/损坏回退空态 |

### 6.3 DesktopHub.App（WPF UI）

| 类型 | 职责 |
|---|---|
| `App.xaml.cs` | 单实例（Mutex）、未处理异常兜底、组合根：组装 Controller/Tray/PillWindow；启动时 `ThemeManager.Initialize(this)`（先于窗口创建） |
| `PillWindow` + `PillViewModel` | 胶囊窗：三态状态机（Small/Recent/Expanded）、三段式形变动画、跨屏跟随、时钟刷新；悬停展开后存在媒体会话时前置区切换为音乐信息(封面/歌名/歌手/播放暂停)，后部最近打开条不变（详见 §8.5） |
| `MediaSessionService` | 系统媒体检测：包装 SMTC（`GlobalSystemMediaTransportControlsSessionManager`，WinRT，Win10 1903+），聚合会话优先"播放中"，发布 歌名/歌手/播放态/专辑封面（事件经 IUiDispatcher 回 UI 线程），`TogglePlayPause` 切换播放（详见 §8.5） |
| `DesktopController` | App 层门面：组合 `IconService`（扫描/缓存/刷新）与 `ConfigService`（配置/自启），持有桌面变化监听，广播 `StatusChanged`；负责"最近打开"的恢复（首次扫描后）与落盘（启动时）；UI 层只依赖本门面 |
| `IconService` | 扫描编排 + 图标缓存：`GetGroups` 命中缓存快速路径，`RefreshAsync` 后台 STA 重扫后回 UI 广播，`Invalidate` 置脏清缓存；句柄在 `finally` 中 `Free` |
| `IconCache` | 按 ParsingName 缓存冻结 `ImageSource`（`ConcurrentDictionary`，忽略大小写） |
| `ConfigService` | 配置加载/保存（`ConfigStore`）+ 开机自启注册表同步（`AutoStartManager`，HKCU Run 免管理员），Apply 失败兜底 |
| `RecentAppsService` | "最近打开"记录：追踪经胶囊启动的软件，最新在前、按 Key 去重、**最多 3 个**；经 `RecentStore`（Core）持久化，启动后首次扫描成功时按 Key 恢复（桌面图标已删除的条目自然失效） |
| `StaThreadRunner` | 专用 STA 后台线程（`BlockingCollection` 队列串行），Shell COM 交互集中于此 |
| `UiDispatcher`（WpfUiDispatcher） | `IUiDispatcher` 实现，业务层不再直接依赖 `Application.Dispatcher` |
| `ScreenInterop` | 鼠标所在显示器工作区（DIP 换算），用于胶囊窗定位 |
| `SettingsWindow` + `SettingsViewModel` | **侧边栏分区设置页**：个性化（主题三态/亚克力开关/不透明度，改动即生效即落盘）、通用（自启/隐藏图标）、分组规则（DataGrid）；侧边栏为 ListBox 分区导航（Segoe MDL2 图标 + 选中高亮），内容面板经 `IndexVisibilityConverter` 切换；保存应用与校验警告/成功提示经事件桥接弹窗。窗口配色随主题经 DynamicResource 切换 |
| `TrayIconProvider` | 系统托盘图标与右键菜单（Hardcodet.NotifyIcon.Wpf） |
| `DesktopIconVM` | 面板图标 VM（名称/分类/图标/启动动作） |
| `Helpers/AcrylicHelper` | 亚克力背景 + 圆角裁剪 + 去系统边框（当前方案，详见 §10） |
| `Helpers/ThemeManager` | 深浅色主题管理 + 亚克力开关/不透明度状态（详见 §10） |
| `Themes/Capsule.Dark.xaml` / `Capsule.Light.xaml` | 主题画刷资源字典 |
| `EmotionBall/*` | 表情球引擎 + WPF 渲染控件 + 自动生成数据（详见 §9） |

## 7. 关键技术决策

- **桌面枚举走 `Shell.Application` 自动化对象（IDispatch）而非 IShellFolder 裸接口**：实测后者在某些环境下 vtable 调用不可靠（测试宿主 AccessViolation），IDispatch 路线稳定。
- **枚举与图标提取分离**：`DesktopShell` 只产数据，`IconExtractor` 在展示时按需取图标——避免一次全量扫描同时做昂贵的图标提取，也把句柄生命周期限制在 UI 适配层。
- **图标 48×48（高 DPI 不模糊）**：`SHGetFileInfo(SHGFI_SYSICONINDEX)` 拿系统图标索引 → `SHGetImageList(SHIL_EXTRALARGE)` + `IImageList::GetIcon` 取 HICON。踩坑记录：
  - `IID_IImageList` 是 `{46EB5926-582E-4017-9FDF-E8998DAA0950}`（网上常见错抄 `…D80B5F5`，QI 会返回 E_NOINTERFACE）。
  - 系统图标列表指针会**跨线程使用**（UI 线程 GetGroups / STA 后台 RefreshAsync），**不能缓存 RCW**（跨 apartment 抛 InvalidComObjectException），也不能手工 vtable 调用（测试宿主 AccessViolation）→ 方案：缓存裸指针永久持有一个引用，每次调用在当前线程重新 `GetObjectForIUnknown` QI 出 RCW 调 `GetIcon` 后立即 `FinalReleaseComObject`。
  - `SHGetFileInfo` 失败时 `iIcon` 残留 0，会取到系统列表第 0 项 → **必须检查返回值**。
- **图标显隐走 Explorer 内部命令**：向 `SHELLDLL_DefView` 发 `WM_COMMAND 0x7402`，等效桌面右键 → 查看 → 显示桌面图标，即时生效；找不到时回退遍历 WorkerW。
- **句柄契约**：见 §5（`Extract`/`Free` 成对、`finally` 释放）。
- **数据自动生成、勿手改**：表情球几何数据由 `tools/gen-emotion-ball-data.mjs` 从原始 JS 转译，生成文件带 `<auto-generated>` 头（详见 §9）。
- **GDI 截屏方案已废弃**：曾用 `BackdropCapture`（`CopyFromScreen` + `BlurEffect`）模拟 backdrop blur，且 `Graphics.CopyFromScreen` 会把 layered 窗口自身像素截进去（抓到的只是胶囊自身）。该方案与 `BackdropBlur.cs`（blurbehind 方案）均已删除，详见 §10 演进史。

## 8. 胶囊窗（Pill）设计与实现

### 8.1 交互模型（三态状态机）

- **Small**：240×58 小胶囊，屏幕顶部水平居中（跟随鼠标所在屏幕），显示**当前时间**（每秒刷新，`_clockTimer`）。
- **Recent**：悬停/触碰 → 向右展开为 412×58 扩展条，展示**最近打开 3 个软件图标**（单击即启动，`RecentTile_MouseDown` 中 `e.Handled=true` 防冒泡触发展开），不打开面板。
- **Expanded**：**点击胶囊** → 展开为 640×580 完整分组面板；图标**双击**启动；鼠标离开 1.5s（`_collapseTimer`）自动收起。

### 8.2 三段式形变动画

常量：`SmallWidth=240/58`、`RecentWidth=412/58`、`ExpandedWidth=640/580`；
时长：`HeightStageMs=200`、`WidthStageMs=220`、`SettleStageMs=140`（`AnimationMs=280` 为 Recent 单段/兼容用）。

- **收起**（Expanded → Small）：
  1. `CollapseStageHeight`：640×580 → 640×58，顶边锚定向上压缩，面板淡出并 Hidden，`SetCorner(29)` 全程圆角一致；
  2. `CollapseStageWidth`：640×58 → 240×58，**Left 同步动画保持中心固定**，两侧同时向中间收缩；
  3. `CollapseStageSettle`：时间文本 140ms 淡入收尾。
- **展开**（镜像两段）：`Expand` 先宽度段（两侧向外展开为 640×58 长条，时间/最近条淡出）→ `Completed` 后高度段（向下展开 640×580，`SetCorner(18)`，面板淡入，`RefreshStatus`）。
- **Recent 直接收起**走 `CollapseToSmall`（左缘锚定单段）。
- 圆角规律：Small/Recent=29，Expanded=18；收起全程 29。

### 8.3 关键实现细节（改代码前必读）

1. **动画代次守卫 `_animSeq`**：每次状态切换 `_animSeq++`，所有 `Completed`/定时器回调先比对 `seq != _animSeq` 即作废——保证收起中途点击可立即转向展开、旧回调不覆盖新目标。
2. **`AnimateSizePos` 的可选参数**：`(toWidth, toHeight, toLeft=null, durationMs=AnimationMs, easingMode=EaseOut, onCompleted=null)`，链式阶段靠 `onCompleted` 串起，共享同一代 seq。
3. **`SetCorner(radius)` 直接赋值 + `UpdateClip()`**，禁止用 `DiscreteObjectKeyFrame` 动画圆角（会造成收起后轮廓闪烁）。
4. **`UpdateClip` 复用单个 `RectangleGeometry` 实例**（改 Rect/RadiusX/RadiusY），避免逐帧分配；`PillBorder.Clip` 在构造函数赋一次。
5. **ExpandedPanel 固定尺寸 600×552**（Margin 20,16,20,12，左上对齐）——动画期间内容层不重排、WrapPanel 不逐帧 re-measure，流畅度关键。
6. **滚动条 `GlassScrollBar`**（2026-09-02 重写，替代旧 `ThinScrollBar/ThinScrollViewer`）：8px 宽覆盖式贴右侧，默认 `Opacity=0` 自动隐藏——悬停面板 150ms 淡入、离开 350ms 淡出（DataTrigger + Storyboard，绑定 `AncestorType=ScrollViewer` 的 `IsMouseOver`）；Thumb `MinHeight=32` 圆角 4，常态 `#26FFFFFF`、悬停 `#66FFFFFF`、拖拽 `#99FFFFFF`；`GlassScrollViewer` 设 `CanContentScroll=False` 实现像素级平滑滚轮，模板命名 `PART_ScrollContentPresenter` + `PART_VerticalScrollBar`。
   ⚠️ 自写 ScrollViewer 模板时 `PART_VerticalScrollBar` 必须显式绑定 `Maximum={TemplateBinding ScrollableHeight}`、`Value={Binding VerticalOffset, RelativeSource={RelativeSource TemplatedParent}}`、`ViewportSize={TemplateBinding ViewportHeight}`（WPF 默认模板就是这三条）——缺了滑块会停在顶部不跟随滚动，Track 与 ScrollBar 之间的自动同步只解决"滑块↔ScrollBar.Value"，不覆盖"ScrollBar.Value↔ScrollViewer 偏移"。
7. **SmallPanel 是整列 Grid + `Background="Transparent"`** 扩大可点击区（早期坑：只点文本可点，其余死区）。
8. **跨屏跟随**：`PillWindow.FollowCursorScreen`（挂时钟定时器每秒），非 Expanded 态下鼠标所在屏工作区与 `_workArea` 不同则 `AnimateMove` 平移到该屏顶部居中；`_workArea` 随之更新（`CenterLeft` 后续动画用新屏坐标）。`Completed` 同样先订阅后 `BeginAnimation`。
9. **时钟刷新**：`_clockTimer` 每秒刷新时间文本（Small/Recent 态显示）。

### 8.5 音乐前置区与滚轮切换（2026-09-05 深夜,改代码前必读）

**交互**:**滚轮上滑**进入音乐前置区(240 宽不变)——存在媒体会话时显示 **圆形专辑封面(40) + 歌名/歌手 + 播放⇄暂停**(紧跟歌名右侧);无会话显示 音符+「暂无音乐」占位。**滚轮下滑**回到表情球+时间。**检测到音乐开始播放(暂停→播放 边沿)自动切入**音乐前置区。悬停展开的 [前置区 | 最近打开条] 中前置区跟随当前模式,后部最近打开条不受影响。**歌词功能已按用户要求移除**(LyricsService.cs 删除;LRCLIB 方案验证可用,恢复方式见本日工作日志)。

改这里的代码前必须知道:

1. **前置面板切换**:`_musicMode` 字段 + `SetFrontMode(bool)` 统一切换;`ApplyFrontMode` 按模式互斥可见性。所有收起路径(`CollapseToSmall`/`CollapseStageSettle`)与展开动画都必须走 `ApplyFrontMode`,不得硬编码 SmallPanel 可见性。**滚轮必须走 `HwndSource.AddHook`(`PillWndProcHook`)`**——WPF 丢弃非活动窗口的 `WM_MOUSEWHEEL`,悬停小组件从未被激活,MouseWheel 事件收不到;展开态在钩子里放行给图标列表。**重构时勿删 SourceInitialized 里的 AddHook 注册**(实测:钩子方法在而注册被删=滚轮静默失效)。
2. **自动切入**:`OnNowPlayingForAutoSwitch` 只在 `IsPlaying` 的 false→true **边沿**触发(避免用户滚回时间后被周期事件反复拉回);展开态不打扰。
3. **SMTC 线程模型**:WinRT 事件在线程池触发,`MediaSessionService` 全部经 `IUiDispatcher.BeginInvoke` 回 UI;会话属性读取存在竞态,整轮 try-catch 兜底。`StartAsync` 放后台(`StartMediaSessionsAsync`),失败静默空态。
4. **TFM**:`DesktopHub.App` 与 `DesktopHub.App.Tests` 均为 `net8.0-windows10.0.19041.0`(WinRT 投影由 SDK 自带,离线可用);引用 App 的项目(含 tools/SettingsPreview)必须同 TFM,否则 NU1201。**启动/验证必须使用最新输出目录的 exe**(TFM 变更会换输出目录,旧目录残留过期 exe——本日"滚轮无效"假象的根源)。
5. **封面**:`TryGetArtworkAsync` 从 `props.Thumbnail` 读流 → DataReader 取字节 → 冻结 BitmapImage,按 (Title,Artist) 缓存。**渲染用 `Image + EllipseGeometry.Clip`,不要用 `Ellipse.Fill=ImageBrush``**——Freezable 上的绑定在属性由 null 变非 null 时不更新(实测封面不出现)。
6. **播放/暂停**:`TogglePlayPauseCommand` → `session.TryTogglePlayPauseAsync()`(SMTC 官方切换);图标按 `IsMusicPlaying` 切换(暂停三角/播放中双竖条)。
7. **端到端验证工具**:tools/MediaProbe(SMTC 探针:注册真实会话模拟播放器——元数据/文件封面/时间轴推进/响应播放暂停,不发声,可验证自动切入/封面/歌名切换)+ tools/_hwnd.ps1 / capture-hwnd.ps1(PrintWindow 后台抓图)+ PostMessage 直投 WM_MOUSEWHEEL。
8. **前置区轮换动画与按钮定位(2026-09-09)**:滚轮上/下滑切换不再是硬切——旧内容沿滚动方向滑出淡出、新内容自反向滑入(`AnimateFrontSwap`,260ms CubicEase);出/入场一律从当前值续接,中途反向滚动平滑折返,收尾由定时器固化终值并复位位移。**`MusicFrontPanel` 必须留在 `CapsuleBar` 第 0 列(固定 240 宽)**——放外层 Grid 会横跨整窗宽,播放/暂停按钮随悬停展开右移(用户截图返工);`SmallPanel`/`MusicFrontPanel` 各挂 `TranslateTransform`(`SmallShift`/`MusicShift`)供轮换动画使用。

### 8.4 亚克力背景接线（当前，详见 §10）
`PillWindow` 在 `SourceInitialized` 中获取 HWND 后调用 `AcrylicHelper`（去系统边框 → 挂背景 → 圆角裁剪），并订阅 `ThemeManager.ThemeChanged` / `AcrylicChanged` 同步重设；`PillBorder.SizeChanged` 触发 region 重裁。详见 §10 的"当前实现（代码为准）"。

## 9. 表情球（EmotionBall）

移植自 [aora-bot/emotion-ball](https://github.com/sam70361/aora-bot)（社区许可，出处已在文件头注明）。**32 套表情 + 25 组眼环轮廓**，44px 胶囊内展示。

### 9.0 运行时行为（行为规格）

- **开场序列**：启动播放「唤醒」序列后自动落到「待机放空」。
- **待机表现**：随机眨眼、眼环池轮换形变、呼吸起伏；约 **9~18s 触发一次**自旋/弹跳小动作。
- **交互**：点击胶囊触发自旋一圈彩蛋；表情暂未接悬停/点击切换情绪（见 §14）。

### 9.1 架构与移植方式

| 原版（JS） | 本项目 |
|---|---|
| `rings.js`（25 组眼环几何）+ `emotions.js`（32 表情配置） | 经 `tools/gen-emotion-ball-data.mjs` 转成 `EmotionBallData.cs` / `EmotionSeed.cs`（**纯数据、自动生成、只读勿手改**） |
| `engine.js` 的 rAF 状态机 | 手工移植为 `EmotionEngine.cs`（`CompositionTarget.Rendering` 驱动，**零分配渲染**） |
| `ball.js` 的 SVG 渲染 | 移植为 `EmotionBallControl.cs`（**单 Visual 自绘** + 静态几何缓存） |
| 彩带/撒花/线稿/zzz 等粒子特效 | **按设计跳过**——44px 胶囊下不可读，渲染成本不成比例（数据字段已保留，见 §14） |

### 9.2 关键实现细节（改代码前必读）

- **每帧合成顺序**（`EmotionEngine`，与原版 engine.js 一致）：base → sequence → 呼吸 → anim 原语 → 池轮换 → 眨眼 → 弹簧整步 → 弹跳 → 注视漂移 → 过渡插值；对外 `SetEmotion` / `Spin` / `Bounce`。
- **零分配渲染约束**：`OnRender` 逐帧只改 Transform 属性值与 Brush 引用；眼环几何仅在 `RingDirty` 时重建（形变帧才逐点插值，静止帧直接复用缓存的 `StreamGeometry`）。
- **坐标换算**：viewBox `-15 -15 259 259`；`px = (viewCoord + 15) * scale + offset`（Pad=15 补正 viewBox 负边距）；`UpdateFit` 按 `min(ActualWidth, ActualHeight)/259` 缩放世界变换并居中。
- ⚠️ **`_world` TransformGroup 的 Children 顺序即应用顺序（先排先应用），必须是 [Translate(Pad) → Scale → Offset]**——2026-09-02 曾因顺序写成 [Offset → Scale → Translate] 导致 15px 边距补正作用在缩放后坐标上，球体整体偏右下 15px（`PillVisualSnapshotTests` 可复现与回归）。
- 布局验证：`tools/verify-emotion-ball-layout.mjs` 输出 44px 容器内身体/眼睛的像素边界——身体占 88%、四周留白 ~2.5px、眼睛放大后约 7.8×9.6px。对齐回归：`PillLayoutAlignmentTests` 断言球容器在小胶囊内垂直居中、时间相对整窗水平居中（240 宽窗内居中于 120）。
- 弹跳时顶部瞬时超出控件 5.6px 属**设计内行为**（胶囊 58px 高 + 圆角裁剪保护）。

### 9.3 双眼水平正面化 `FaceFrontX`（2026-09-03，相对原版唯一有意偏离）

- **问题**：原版眼环数据把注视方向烘焙在位置里（待机环 0 双眼对中点偏右上 +46.9px、环 8 偏左下 -34.1px，并带"远眼薄近眼厚"的透视形状差），网页大尺寸下读作"左看右看 / 张望"，**44px 胶囊里被读成"侧脸/背对"**（用户反馈"总像面向后方"）。
- **排查结论**：经与原版 ball.js 逐行核对 + 静态对照页验证，移植代码与原版渲染完全一致，**非移植 bug**。
- **修复**：`EmotionBallControl` 投影层计算双眼质心中点相对 `HeadC` 的水平偏移 `pairDx`，`UpdateEyeTransform` 经度换算前先减去（`ox = FaceX + (bx - HeadC - pairDx) * FaceSx + …`）——**只动整对眼睛的位置**，眼环形状/俯仰/大小差异、形变/眨眼/自旋/张望动画全保留（自旋 yaw 在经度 `total = theta + yaw` 叠加，不受影响）。`FaceFrontX=1` 为完全回正，置 0 可恢复原版烘焙注视位。余弦压缩从 0.41 回到 0.95（眼睛不再被压成细条）。
- **回归**：`EmotionBallFacingTests` 断言环 0/环 8 投影后双眼对中点偏离轮廓中线 < 14px。

### 9.4 测试与离屏渲染注意事项

- 引擎冒烟：注册表完整性（32 表情全可切换）、未知 ID 回退待机、几何数据尺寸/范围、连续 600 帧合成管线稳定性、多表情快速切换、自旋/弹跳 API、自旋偏航收敛（真实时间）、切换眨眼开合度。
- **离屏渲染诊断坑**：(1) `OnRendering` 有 `IsVisible` 门槛，测试控件不在可视树会完全不推进，须直接反射调 `engine.Tick()` + `RefreshEyeGeometries()`；(2) 漏 `InvalidateVisual()` 时 `RenderTargetBitmap.Render` 只回放首帧缓存画面，看到的不是当前状态。
- 时间驱动断言用真实时间等待 + 宽松边界，避免 flaky。

## 10. 背景模糊 / 亚克力演进史（★以代码为准）

> 这一块是文档历史上最混乱的区域（四份文档各写了一段且互相矛盾）。整合时已核对 `src/` 代码，演进按时间列全，**当前实现以最末段"当前实现（代码为准）"为准**。此前文档中凡称"BackdropImage 抓屏 / BlurEffect / BackdropCapture / BackdropBlur / 普通窗口 + blurbehind"为当前方案的段落，**均已过时**。

### v0 历史背景

胶囊窗需要"半透明底色 + 适度 backdrop blur"的灵动岛观感。WPF 默认 `AllowsTransparency=True` 是 layered 窗口，系统背景模糊（`SetWindowCompositionAttribute` blurbehind）对其无效——这是整个演进反复的根因。

### v1（已废弃）普通窗口 + blurbehind

去掉 `AllowsTransparency` 改普通窗口 + `ACCENT_ENABLE_BLURBEHIND` + `SetWindowRgn` 圆角裁剪，`PillBorder` 自绘 `#B0202027`（α≈0.69）。
**实测两个 bug**：WPF 内容在非 layered 窗口上被 DWM 视作不透明，blurbehind 被覆盖、背景变死黑；且去掉 `AllowsTransparency` 后窗口被加 `WS_BORDER`，胶囊周围出现一圈白边。→ 推翻。对应 `Shell/BackdropBlur.cs`（已删除）。

### v2（已废弃）layered + 抓屏伪模糊

恢复 `AllowsTransparency=True`（无 WS_BORDER、像素级透明圆角），用 `App/Services/BackdropCapture.cs` 把窗口区域屏幕抓成 `BitmapSource` 赋给 `BackdropImage`（挂 `BlurEffect Radius=22`），`TintOverlay` 叠 `#96202027`（α≈59%）形成"暗玻璃"。
**已知简化**：`Graphics.CopyFromScreen` 在当前 DWM 合成下会把 layered 窗口像素也截到——抓到的只是胶囊自身当前帧而非"实景"桌面（壁纸均匀时无差别，花壁纸下看到的是胶囊自身的模糊）。→ 已废弃，`BackdropCapture.cs` 已删除。

### v3（overview.md 交付，真亚克力雏形）

新增 `Helpers/AcrylicHelper.cs` + `Helpers/ThemeManager.cs` + `Themes/Capsule.Dark/Light.xaml`：`SetWindowCompositionAttribute` 挂 `ACCENT_ENABLE_ACRYLICBLURBEHIND`（Acrylic 模糊，Win10 1803+ 生效），GradientColor（ABGR）深色 `0xCC202020` / 浅色 `0xBFFFFFFF`；Win11 兼容尝试 `DWMWA_SYSTEMBACKDROP_TYPE(38) = DWMSBT_TRANSIENTWINDOW`；`SetWindowRgn` 物理像素圆角；内容层阴影 `DropShadowEffect`（BlurRadius=20, Opacity=0.2, ShadowDepth=2）；内容画刷 Alpha ≤ 0x40 不画不透明底；删除抓屏伪模糊（BackdropImage + BlurEffect）。

### v4（已废弃，2026-09-05 上午~下午）SWCA state=3 中间方案

`ACCENT_ENABLE_ACRYLICBLURBEHIND(4)`（WinUI AcrylicBrush 私有模拟）矩形材质面不被 `SetWindowRgn` 裁剪 + 自带 ~1px 描边 → 明显矩形框；据此切到 `ACCENT_ENABLE_BLURBEHIND(3)` + WPF `TintOverlay` 自绘着色。当时验证只查了胶囊四边中段（与圆角帽重合处）便下结论"模糊跟随圆角"——**实际 state=3 的模糊面同样覆盖全窗口矩形、无视 SetWindowRgn**，四个圆角角落露出被抹平的模糊角（std 13.5 vs 壁纸 39.4），即用户看到的"隐约矩形"。

### v5 = 当前实现（代码为准，2026-09-05 下午定稿）

- **`AllowsTransparency=True`（WS_EX_LAYERED）仍是硬性前提**：只有 layered 窗口上 DWM 才真正合成模糊层。
- **Win11**：维持 `ACCENT_ENABLE_ACRYLICBLURBEHIND` + `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE=38, DWMSBT_TRANSIENTWINDOW=2)`，由 DWM 提供圆角模糊与着色（不变）。
- **Win10**：SWCA accent 全部弃用（`ACCENT_DISABLED`），改用公开 API **`DwmEnableBlurBehindWindow` + `DWM_BB_BLURREGION` 显式圆角模糊区域**（`AcrylicHelper.UpdateBlurRegion`，与 `SetWindowRgn` 同参同坐标系）；着色由 **WPF `TintOverlay` 自绘**（`CreateTintBrush`，`UseWpfTintOnWin10 = !IsWindows11OrLater`）。关闭/透明度 0 时 `fEnable=false` 撤掉模糊面。**像素验收**：胶囊内部 std 22.9~24.6 vs 壁纸 58.1~61.3（真模糊）；四角落 maxGrad 94~96 ≈ 基线 104~133（无矩形模糊面，旧配方仅 19）。`hRgnBlur` 句柄按"调用方持有"处理，调用后立即 `DeleteObject`。
- **`StripSystemBorder`**：清掉 `WS_BORDER`（0x00800000）等边框位——DWM 沿窗口外侧画的 1px 矩形描边会盖在 `SetWindowRgn` 圆角裁剪之上，必须清。
- **`SetWindowRgn` + 模糊区域跟随 DPI**：`UpdateRoundedRegion` / `UpdateBlurRegion` 均按物理像素（`CornerRadius × DPI.PixelsPerDip`）创建圆角区域；`PillWindow.UpdateWindowRegion` 在 `SizeChanged` / `SetCorner` 时**同时刷新两者**（动画期间也严格贴合）。
- **主题与开关状态**（`ThemeManager`）：`Mode`（System/Dark/Light）+ `CurrentTheme`；`ThemeChanged` / `AcrylicChanged` 事件广播；`AcrylicEnabled`、`AcrylicOpacity` 为设置状态，`PillWindow.RefreshBackdrop` 每次以 `opacity = AcrylicEnabled ? AcrylicOpacity : 0.0` 重设并把当前圆角传给 `ApplyBackdrop`。启动经 `ThemeManager.ApplyStartup(AppConfig)` 装配（先于窗口创建）。
- **圆角外形裁剪两层**：`SetWindowRgn` 裁 HWND + `PillBorder.Clip` 裁 WPF 内容层；模糊区域由 `DWM_BB_BLURREGION` 单独约束（Win10）。

### 验证方法（如何确认真模糊）

- **构建/测试**：`dotnet build` 0 警告 0 错误；`DesktopHub.App.Tests` 全绿。
- **目视**：胶囊内壁纸应被 DWM 柔化（柔化的白色调 + 被磨平的物体轮廓），与胶囊外（锐利轮廓）对比清晰。历史证据截图见 `.workbuddy/capsule-evidence.png`（v3 快照）。
- **像素统计（可复用指纹）**：对截图采样"胶囊内"与"同位置原始壁纸"区域，比较平均亮度与**亮度方差 L_std**——高斯模糊会显著压低局部对比度，胶囊内方差应明显低于外部锐利壁纸。v3 快照实测：胶囊内 43.77 vs 胶囊外下方 50.75 / 左侧 66.82（约降 13.4%）。**数值随配方改动仅供参考，改完以实测为准**。
- **矩形板材回归检查（2026-09-05 新增，tools/ 可复用）**：① `tools/capture-zone.ps1`（DPI-aware GDI 抓屏，可加 `-ProcId` 顺带打印胶囊窗口物理矩形）；② `tools/verify-no-rect.mjs`（胶囊圆角外采样点应为锐利壁纸、窗口外缘 diff≈0、胶囊内 std 应显著低于壁纸）；③ `tools/diff-shape.mjs`（应用 vs 基线 A/B 差异足迹）；④ `tools/corner-analysis.mjs`（**四角落差集区**结构统计——窗口矩形减去圆角帽的区域是矩形 surface 唯一露出处，运行态角落 maxGrad 应 ≈ 基线，std/mean 残差用 `wall-*` 对照区扣除动态壁纸运动噪声）；⑤ `tools/interior-check.mjs`（胶囊内部模糊指纹）。⚠️ 本机壁纸是**动态壁纸**（两次抓屏间树枝会移动），逐像素 diff 有全局噪声——判定一律以**局部方差/梯度**等结构指标与目视对比为准，勿用单点 diff 下结论。证据存档：`.workbuddy/diagnostics/rect-fix/`。

### 系统前置条件

| 项 | 要求 | 说明 |
|---|---|---|
| Windows 版本 | Win10 1803+（本机实测 Win10 19045；Win11 走 DWM 分支） | `SetWindowCompositionAttribute` 亚克力生效起点 |
| 系统"透明效果" | **必须开启** | `HKCU\...\Personalize\EnableTransparency=1`，否则 DWM 不画模糊层，Accent 完全失效 |
| 系统主题 | 任意 | 跟随系统深浅色（`AppsUseLightTheme`），GradientColor/tint 同步切换 |

### 使用接线示例

```csharp
// 窗口 SourceInitialized 后一次性应用（已在 PillWindow 中调用）
AcrylicHelper.StripSystemBorder(this);
AcrylicHelper.ApplyBackdrop(window, ThemeManager.CurrentTheme, opacity);
AcrylicHelper.UpdateRoundedRegion(window, cornerRadiusDip);

// 主题 / 开关 / 透明度变化时同步刷新
ThemeManager.ThemeChanged  += _ => RefreshBackdrop();
ThemeManager.AcrylicChanged += () => RefreshBackdrop();
```

## 11. 近期改动与修复记录（时间线）

### Phase：三轮需求（2026-08-31）
1. 时间替换图标数量 + 移除绿点 + 水平居中修复（`PositionPill` 按 SmallWidth 居中）；悬停展开 Recent 条、点击才开面板；滚动条圆角细条化。
2. 修复收起后左侧圆角轮廓闪烁（删 `AnimateCorner`，改 `SetCorner` 直接赋值）；展开停留 2500ms→1500ms；修复"有最近图标时点不开展开面板"（SmallPanel 改整列可点）。

### Phase：三段式动画（2026-09-01 白天）
收起三段式 + 镜像展开两段式；`AnimateSizePos`/`AnimateOpacity` 支持自定义时长/缓动/链式回调。

### Phase：关键 Bug 修复（2026-09-01 晚）⭐ 最重要的经验
- **症状**：点击胶囊后停在"长胶囊"（640×58），面板不展开；再点无效。
- **根因**：`AnimateSizePos` 在 `BeginAnimation` **之后**才订阅 `widthAnim.Completed`。WPF 的 `Timeline.Completed` 由时钟派发，**订阅必须发生在 `BeginAnimation` 之前**；之后订阅的处理器静默丢失（不报错）。单段动画不依赖 Completed（HoldEnd 保持终值）所以此前未暴露，三段式链式首次依赖 Completed 触发下一阶段即暴露。
- **修复**：订阅移到 `BeginAnimation` 前，并留注释。
- **定位方法（可复用）**：环境变量开关 + 文件日志 + 内存计数器侧信道 + 每次只变一个变量的对照实验，用 DispatcherTimer 自动驱动流程，排除 GC/共享 Freezable/多动画并发等假设。
- ⚠️ 若未来给 `AnimateOpacity` 等动画加 Completed 链，**同样必须先订阅后 BeginAnimation**。

### Phase：待办三项落地（2026-09-01 深夜）
1. **图标升级 48px**（详见 §7 踩坑记录：IID、RCW 跨线程、iIcon 残留 0）。
2. **跨屏跟随**：`PillWindow.FollowCursorScreen` 每秒检测，非 Expanded 态跨屏 `AnimateMove` 平移。
3. **最近打开持久化**：Core 新增 `RecentStore`（`%APPDATA%\DesktopHub\recent.json`）；`DesktopController` 启动时 Load、首次扫描成功后 `TryRestoreRecent`、`RecordRecentLaunch` 落盘；`RecentAppsService` 增加 `Restore`。

### Phase：表情球朝向正面化（2026-09-03）
详见 §9.3。已渲染验证静止/池轮换/自旋多帧：静止正面、自旋一圈后回正。

### Phase：胶囊岛背景模糊（2026-09-03）
v1 普通窗口 + blurbehind（死黑 + 白边）→ v2 layered + 抓屏伪模糊 → v3 真亚克力雏形 → 后续演进至 v4 当前实现。完整演进与代码现状见 §10。

### Phase：胶囊岛外"多余矩形"结案（2026-09-05）⭐
- **历经 5 轮修复未果的根因**：00:28 已写好的 Win10 blurbehind 配方（AccentState=3）**从未构建成功**（当时沙箱 restore 无网络 + NETSDK1060），用户跑的一直是 00:07 旧构建 = state=4 亚克力配方，其矩形材质 surface 不被 `SetWindowRgn` 裁剪 → 矩形板材 + 顶部 accent 描边始终在。**教训：改完配方必须构建 + 实测闭环，"源码已改"≠"问题已修"。**
- **取证方法（可复用，见 §10 验证方法）**：DPI-aware GDI 抓屏 A/B 对比（应用运行 vs 结束进程）+ 自写 Node PNG 解码做像素分析；枚举顶层窗口排除"其他窗口"嫌疑；实拍确认 state=4 有矩形板材、state=3 为标准圆角胶囊。
- **附带发现**：本机壁纸是动态壁纸，逐像素 A/B diff 有全局噪声，验证需用局部方差/梯度指标。
- **顺手修**：`PillWindow.RefreshBackdrop` 把当前圆角传给 `ApplyBackdrop`（此前硬编码 29，展开态收到主题/透明度广播会把面板圆角重置回 29）。

### Phase：四角"隐约矩形"二次结案（2026-09-05 下午）⭐
- **根因**：SWCA `ACCENT_ENABLE_BLURBEHIND(3)` 的模糊面同样覆盖全窗口矩形、无视 `SetWindowRgn`——四边中段与圆角帽重合看不出来，**四个角落**露出模糊角（std 13.5 vs 壁纸 39.4），即"隐约矩形"。上午的验证只查了边缘中段，教训：**矩形窗口 ⊖ 圆角形状的差集区才是矩形 surface 唯一露出处，必须专查四角**（`tools/corner-analysis.mjs`）。
- **修复（§10 v5）**：Win10 弃用全部 SWCA accent，改公开 API `DwmEnableBlurBehindWindow` + `DWM_BB_BLURREGION` 显式圆角模糊区域（`UpdateBlurRegion`，与 SetWindowRgn 同参，SizeChanged/SetCorner 双刷新）；关闭时 `fEnable=false`；着色仍由 WPF TintOverlay 自绘。
- **验收**：两轮 A/B——胶囊内部 std 22.9~24.6 vs 壁纸 58.1~61.3（真模糊仍在）；四角 maxGrad 94~96 ≈ 基线 104~133（无矩形面，旧配方 19）。

### Phase：设置页外观功能（2026-09-05 下午）
- **需求**：设置 UI 优化 + 透明度调整 + 深浅色切换；`ThemeManager` 接口顺势优化。
- **接口演进**：新增 `ThemeMode` 枚举（System/Dark/Light）与 `SetThemeMode`；MVVM 友好重载 `SetTheme(AppTheme)`（VM 不再需要 `Application.Current`）；`ApplyStartup(AppConfig)` 组合根装配；`ParseMode/ModeKey` 与 `AppConfig.ThemeMode` 字符串互映射；主题字典改用**程序集限定 pack URI** 加载（相对 URI 按入口程序集解析，换宿主会挂）。
- **持久化**：`AppConfig` 新增 `ThemeMode`（字符串，向前兼容）/`AcrylicEnabled`/`AcrylicOpacity`；`ConfigService.SaveCurrent` + `DesktopController.SaveAppearance` 就地落盘（不动自启注册表）；退出时 `ExitApp` 兜底固化。
- **UI**：设置窗口**侧边栏分区布局**（172px 导航列 + 内容区）：个性化 / 通用 / 分组规则三个分区（ListBox 导航，Segoe MDL2 图标 + 选中高亮，内容面板经 `IndexVisibilityConverter` 切换），拨动开关 + 细轨滑杆（IsMoveToPointEnabled + 1% 步进）+ 主题三选一；窗口配色全部 DynamicResource 随主题切换；`Themes/Controls.xaml` 收纳 PillButton/PillListStyle 供多处合并。⚠️ WPF 资源字典内 `StaticResource` 不支持前向引用（NavItem 须先于 NavList 声明）；PowerShell 5.1 读无 BOM 的 UTF-8 脚本中文会乱码（自动化脚本要么加 BOM 要么避开中文字面量）。
- **实测**（tools/SettingsPreview 独立预览器 + UIA/坐标交互）：主题切换即时换肤 ✓、滑杆 40%→61% 落盘 ✓、亚克力开关切换胶囊窗口模糊实时增减 ✓、配置恢复 ✓。
- **开发工具**：`tools/SettingsPreview`——独立宿主复用真实 SettingsWindow/ThemeManager/DesktopController（无托盘/单实例），预览与调试设置 UI 用，不进 .sln。

### Phase：音乐模式 + 滚轮切换（2026-09-05 深夜）
- **需求**：悬停胶囊滚轮上滑进入音乐模式（时间上滚动画），检测到音乐播放显示歌名/歌手 + 播放/暂停按钮，滚轮下滑切回时间。
- **TFM**：`net8.0-windows10.0.19041.0`（App + App.Tests + 预览器），WinRT SMTC 投影 SDK 自带、离线可用。
- **服务**：`MediaSessionService` 包装 SMTC——会话优先"播放中"否则保持上一个；事件线程池触发经 `IUiDispatcher` 回 UI；`TryTogglePlayPauseAsync` 切换播放。
- **滚轮交付的关键坑**：WPF 丢弃非活动窗口的 `WM_MOUSEWHEEL`，悬停小组件永远"非活动"——`MouseWheel` 事件方案必然失效（PostMessage/SendInput/SendMessage 直投 hwnd 也不行，消息在 WPF 输入管线被过滤）。**必须用 `HwndSource.AddHook` 公开钩子**（先于内置输入过滤器执行），见 §8.5。
- **验证**：PostMessage 滚轮上滑 ×3 → 胶囊 240→412 DIP 音乐条（占位态）✓；下滑 ×3 → 回到 240 ✓；构建 0 警 0 错；测试 41 全绿（新增 SMTC 冒烟）。
- **排障教训**：预览器 TFM 升级后输出目录变为 `net8.0-windows10.0.19041.0\`，启动脚本仍指向旧目录 `net8.0-windows\` 的**过期 exe**，导致整轮"滚轮无效"假象——**启动路径必须与最新输出目录一致，旧输出目录已删**。

### Phase：前置区轮换动画 + 播放按钮固定（2026-09-09）
- **需求**：滚轮上/下滑切换前置区要有滚动动画；播放/暂停按钮不随悬停展开移动（固定位置）。
- **修复**：`MusicFrontPanel` 从外层 Grid 移回 `CapsuleBar` 第 0 列——原先 `Grid.Column=0` 落在无列定义的外层 Grid 上横跨整窗，按钮被钉在窗口右缘随展开右移（用户截图现象）。
- **动画**：`AnimateFrontSwap` 双面板轮换——旧内容沿滚动方向滑出淡出、新内容自反向滑入（位移 24px / 260ms CubicEase）;从当前值续接实现中断折返;`_frontSeq` 代数作废旧轮换;收尾定时器固化终值。展开态接管时只复位位移,显隐交给收起流程。
- **验证**：PostMessage 上/下滚轮 + PrintWindow 连续抓帧,两个方向中间帧均可见推挤过渡 ✓;小胶囊/展开态按钮 x 坐标一致(固定) ✓;构建 0 警 0 错;App 测试 16/16 ✓。

### 文档同步状态
- 原 HANDOFF_ZCODE.md 最后一次同步声明"README/ARCHITECTURE/HANDOFF 已同步到 v2 抓屏方案"——**该状态已被 v3/v4 取代**，勿再据此改码；本文档（DesktopHub.md）为当前唯一入口。

## 12. 测试现状

| 工程 | 内容 | 数量 |
|---|---|---|
| `tests/DesktopHub.Core.Tests`（xUnit） | 分类引擎、配置读写（含外观字段 ThemeMode/AcrylicEnabled/AcrylicOpacity 往返 + 旧配置向后兼容）、分组排序（`IconGrouper`）、.lnk 解析、48px 图标提取冒烟、recent.json 读写、真实桌面枚举冒烟（Shell COM 须 STA：`ShellAndConfigTests.RunInSta`）、TempAppData 隔离 | 25 |
| `tests/DesktopHub.App.Tests`（xUnit） | SMTC 系统媒体检测冒烟 + 表情球引擎冒烟（注册表完整性、未知 ID 回退、几何数据、600 帧管线稳定性、快速切换、自旋/弹跳、偏航收敛、眨眼开合度）+ `EmotionBallFacingTests`（双眼回正 ×2）+ `PillLayoutAlignmentTests`（球贴左 + 时间居中）+ `PillVisualSnapshotTests`（变换顺序回归）；引用 WPF 工程但只测无 WPF 依赖的纯逻辑与投影计算 | 16 |

合计 **41**（截至 2026-09-05 文档记录）。构建基线：0 警告 0 错误。

## 13. 代码核对发现与技术债清理（2026-08-31 复审）

复审与代码逐一核对，实现与基线一致；以下为**已清理/待处理**：

### 已修复（Phase 3 + Phase 4）

| 问题 | 处理 |
|---|---|
| 每次展开/刷新全量扫描 + 提取全部图标，UI 线程同步执行 | ✅ `IconCache`（按 ParsingName 缓存冻结 ImageSource）+ `RefreshAsync` 后台 STA 重扫回 UI 广播 `StatusChanged` |
| `DesktopController` 直接耦合 `Application.Current.Dispatcher` | ✅ 注入 `IUiDispatcher`（`UiDispatcher` 默认实现） |
| `ConfigStore.Save` 无异常兜底 | ✅ 与 Load 一致，加 try-catch 回退 |
| `TrayIconProvider.CloneConfig` 死代码 | ✅ 删除 |
| 配置无版本号 | ✅ `AppConfig.SchemaVersion`（当前 1），Load 保留字段供迁移 |
| `DesktopController` 上帝对象 | ✅ 拆 `IconService`（扫描+缓存）+ `ConfigService`（配置/自启）；Controller 只做门面编排，UI 层零改动 |
| `PillWindow`/`SettingsWindow` 交互逻辑全在 code-behind | ✅ 抽 `PillViewModel`/`SettingsViewModel`（MVVM）；code-behind 只留动画、窗口定位与弹窗桥接 |

### 待处理（按优先级）

| 级别 | 问题 | 位置 | 建议 |
|---|---|---|---|
| 低 | 系统项（回收站等）图标提取走 `SHParseDisplayName` 每个都新建 PIDL，频率高时开销大 | `Shell/IconExtractor.cs` | 结合 IconCache 后已缓解，可进一步缓存 PIDL |

**正面结论**：分层依赖（`App → Core → Shell`）与文档一致，无反向引用；Core 无 WPF 类型、无句柄泄漏；句柄契约 `Extract/Free` 成对落实在 `IconService.ExtractIcon` 的 `finally`；分组顺序单一事实来源（`AppConfig.Categories`）已收敛；窗口级 ViewModel 化后 code-behind 仅剩纯 UI 行为。

## 14. 已知限制与待办 / 后续方向

**功能/体验限制：**
- 面板不显示桌面图标的右键菜单（目前只支持双击启动）→ 可扩展 `IContextMenu`。
- 未做安装包，当前以直接运行 exe 方式使用 → 可接 Windows 应用打包（MSIX / Inno Setup）。
- 表情球暂未接入交互（悬停情绪变化/点击切换表情），当前只有点击自旋彩蛋 → 可把 `EmotionBallControl.SetEmotion(id)` 接到胶囊状态（如展开面板时切换到"专注/思考"）。
- 表情球粒子特效未移植（彩带/撒花/线稿/zzz/鼠标注视），44px 下不可读；若胶囊放大（如展开态），可补 `Engine.Burst`/`Ribbon` 并接入 `EmotionEngine` 的 `Ribbons/Confetti/Zzz/Orbit` 字段（数据已保留）。
- 混合 DPI 多屏下 WPF 窗口坐标换算仍是按目标屏 scale 的近似（`ScreenInterop`），若出现错位再深化。
- 展开动画链目前完全依赖 `Timeline.Completed`（订阅时机已修复）；若未来动画异常难查，可考虑整体改用 `DispatcherTimer` 驱动的阶段调度器（确定性更强）。

**主题相关（非本任务遗留）：**
- ~~主题切换入口~~ ✅ 已接入设置页（2026-09-05）：`ThemeManager.SetThemeMode(ThemeMode)` 三态（跟随系统/深色/浅色），启动经 `ApplyStartup(AppConfig)` 按配置解析；配置字段持久化在 `AppConfig.ThemeMode/AcrylicEnabled/AcrylicOpacity`。
- 浅色主题下 `PillButton` 等 `Themes/Controls.xaml` 样式仍是深色配色（只在胶囊面板内使用，深色底上正确）；设置窗口自身配色已随主题切换。

## 15. 开发约定（改代码前必看）

1. **分层依赖**：App → Core → Shell，禁止反向；Core 无 WPF、无句柄（见 §5）。
2. **分类顺序**：面板分组顺序永远来自 `AppConfig.Categories`（`IconGrouper` 已收敛）；**不要在任何 UI 层硬编码分类顺序或名称**。
3. **MVVM**：交互逻辑优先进 ViewModel（`[ObservableProperty]` / `RelayCommand`）；code-behind 只保留纯 UI 行为（动画、窗口属性、弹窗桥接）。
4. **错误兜底**：Shell/扫描类操作一律 try-catch 兜底，失败返回空态而非抛到 UI；COM 对象用 `Marshal.FinalReleaseComObject`。
5. **新增 Win32 互操作**：一律放 `DesktopHub.Shell`（纯 UI 需要的窗口级 P/Invoke 如 AcrylicHelper 可放 App/Helpers）；先写冒烟测试再接入。
6. **句柄契约**：`IconExtractor.Extract` 返回的 HICON 必须成对 `Free`，在 `finally` 中释放——新增取图逻辑必须遵守，防 GDI 句柄泄漏。
7. **动画订阅时机**：`Timeline.Completed` 订阅**必须在 `BeginAnimation` 之前**（详见 §11），链式动画同理。
8. **自动生成文件勿手改**：`EmotionBallData.cs` / `EmotionSeed.cs`（`<auto-generated>` 头）；改数据只动 `tools/gen-emotion-ball-data.mjs`。
9. **视觉基调**：深色半透明玻璃拟态 + 亚克力。主题画刷见 `Themes/Capsule.Dark.xaml` / `Capsule.Light.xaml`：1px 高光描边 `#40FFFFFF`、微弱衬底（Alpha ≤ 0x40）、主文字 `#EAEAF0` / 浅色主题对应替换、次级 `#9A9AA8`、半透明白 `#55FFFFFF`，圆角 Small/Recent=29、Expanded=18；亚克力着色深色 `0xCC202020` / 浅色 `0xBFFFFFFF`（ABGR）。**注意：本项目不是用户 uni-app 项目的蓝白 iOS 风格，勿混用。**
10. **构建命令**：必须用全路径 SDK（见 §4）；提交前保证 0 警告 0 错误、两组测试全绿。

## 16. 注意事项与维护约定

- `.workbuddy/` 目录存项目记忆与工作日志（`memory/` 下按日追加），**不要删除**；每日工作可继续追加 `memory/YYYY-MM-DD.md`。
- 测试中 Shell COM 交互需在 STA 线程（见 `ShellAndConfigTests.RunInSta`）。
- 构建/运行需 Windows + .NET 8 SDK；无安装包，直接跑 exe。
- **文档维护**：本文件（`DesktopHub.md`）自 2026-09-05 起为项目文档唯一入口。原四份文档——`README.md`（对外快速介绍）、`overview.md`（亚克力交付说明，已被 §10 v3/v4 取代）、`ARCHITECTURE.md`（旧架构基线，已被本文 §5-§13 取代）、`HANDOFF_ZCODE.md`（旧交接文，已被本文取代）——已全部并入本文并于 2026-09-05 删除。内容冲突处以 `src/` 代码为准。后续改码请直接更新本文对应章节，勿再产生平行文档。
