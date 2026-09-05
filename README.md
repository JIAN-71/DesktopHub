# DesktopHub

Windows 桌面**灵动岛式图标面板 Dock** —— 把桌面上的全部图标收纳进屏幕顶部居中的"胶囊岛"，面板就是你的桌面入口。

技术栈：WPF / .NET 8（C# 13 + CommunityToolkit.Mvvm）

## 功能特性

- **完整桌面扫描**：枚举 Shell 桌面命名空间，与资源管理器所见完全一致（含公共桌面图标与"此电脑 / 回收站 / 控制面板"等系统图标）；桌面上的任何文件都不会被移动或修改。
- **胶囊窗（灵动岛）**：常驻屏幕顶部居中、跨屏跟随，显示当前时间与表情球（呼吸 / 眨眼 / 眼环轮换 / 自旋动画，点击自旋一圈）；悬停展开"最近打开"扩展条（重启后保留），点击展开完整分组面板，双击图标直接启动，鼠标离开自动收起。
- **分类分组**：内置 应用程序 / 游戏 / 文件夹 / 网页快捷方式 / 系统 / 其他 分组，支持自定义关键字与目标路径规则。
- **一键隐藏桌面图标**：调用系统"显示桌面图标"切换，立即生效、随时切回。
- **自动刷新**：监听用户 / 公共桌面文件变化，图标增删后面板自动更新，也可手动刷新。
- **托盘常驻**：重新扫描 / 隐藏桌面图标 / 设置 / 退出，支持开机自启（HKCU Run 键）。
- **外观设置**：主题三态（跟随系统 / 深色 / 浅色）、亚克力开关与不透明度滑杆，全部即时生效并自动保存。

## 环境与构建

需要 Windows 10 / 11 + [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)：

```bash
git clone https://github.com/JIAN-71/DesktopHub.git
cd DesktopHub
dotnet build DesktopHub.sln
dotnet run --project src/DesktopHub.App
```

编译产物：`src/DesktopHub.App/bin/Debug/net8.0-windows/DesktopHub.App.exe`，双击即可运行（单实例，Mutex 防重复启动）。

## 测试

```bash
dotnet test tests/DesktopHub.Core.Tests
dotnet test tests/DesktopHub.App.Tests
```

## 文档

完整的项目定位、架构分层、模块职责、关键技术决策与开发约定见 [DesktopHub.md](DesktopHub.md)。

## 图标来源

表情球图标来自 [aora-bot/emotion-ball](https://github.com/sam70361/aora-bot)（社区许可，出处已在文件头注明）。

## License

暂未设置开源许可证；如需引用请注明出处。
