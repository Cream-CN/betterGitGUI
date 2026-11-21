# Editior

一个用 WinForms 默认控件实现的轻量代码编辑器，作为 BetterGitGUI 的配套工具。
目标不是复刻 VS Code，而是把「打开代码 → 改两行 → 提交」这条链路做顺：
标准 RichTextBox + TreeView + TabControl + StatusStrip，没有自绘控件、没有深色皮肤、没有自建编辑器内核。

## 功能

**编辑**
- 多标签页，支持右键菜单关闭、鼠标中键关闭、Ctrl+W 关闭、关闭其他 / 关闭全部
- 语法高亮（RichTextBox 逐 token 着色，200k 字符以上自动跳过以保证流畅）
- 查找 / 替换（区分大小写、全词匹配、全部替换）
- 转到行（Ctrl+G）
- 自动换行、Ctrl+滚轮缩放、可配置制表符宽度
- 编码识别与转换：UTF-8、UTF-8 with BOM、GBK（自动识别，可另存为其他编码）
- 换行符转换：CRLF / LF，保存时按选择写回
- 未命名文档、最近打开文件、拖放打开文件 / 文件夹

**资源管理器**
- 打开文件夹（懒加载目录树，F2 重命名，右键新建 / 删除等）
- 打开解决方案：`.sln` / `.slnx` 解析后按「解决方案 → 项目 → 文件」展示
- Git 状态着色：未跟踪（绿）、已修改（橙）、已删除 / 冲突（红），F5 刷新
- 双击文件直接打开，外部修改会在窗口激活时检测并提示重载

**鸟瞰图（minimap）**
- 右侧自绘缩略图：按行压缩成短线，注释用绿色区分
- 画出当前可视区域，点击 / 拖动可快速定位，滚动联动重绘
- 可在设置中关闭或调整宽度

**会话与设置**
- 启动时恢复上次打开的标签（可关闭）
- 设置保存在 `%APPDATA%\.ccnedit\settings.ini`：字体、字号、缩进宽度、自动换行、鸟瞰图、会话恢复、Git 状态、BetterGitGUI 路径

**与其他工具的结合**
- `Git` 菜单：刷新状态、在 BetterGitGUI 中打开当前仓库
- `工具` 菜单：GUID 生成器（原 guidgen 的功能已并入本程序，`Editior.exe --guid` 可单独打开）、设置
- BetterGitGUI 的列表右键菜单与「工具」菜单可反向调用本编辑器；BetterGitGUI 也支持用仓库路径作为启动参数

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| Ctrl+N / Ctrl+O | 新建 / 打开文件 |
| Ctrl+Shift+O | 打开文件夹 |
| Ctrl+S / Ctrl+Shift+S | 保存 / 另存为 |
| Ctrl+W | 关闭当前标签 |
| Ctrl+F / Ctrl+H | 查找 / 替换 |
| Ctrl+G | 转到行 |
| Ctrl+B | 显示 / 隐藏资源管理器 |
| Ctrl+, | 设置 |
| Ctrl+Shift+G | GUID 生成器 |
| F5 | 刷新 Git 状态 / GUID 重新生成 |
| F2 | 重命名资源管理器中的节点 |
| Ctrl+滚轮 | 缩放字号 |

## 编译

需要 .NET Framework 4.8.1 与 .NET SDK（用于 `dotnet build`）：

```
cd editior\Editior
dotnet build Editior.csproj
```

产物：`editior\Editior\bin\Debug\Editior.exe`。

三个程序可以一起编译：在 `betterGitGUI` 目录执行 `dotnet build betterGitGUI.slnx`，
会一次构建 BetterGitGUI、Editior 与 guidgen（`betterGitGUI.slnx` 已把三个项目加入同一解决方案）。

> 本项目是旧式（非 SDK 风格）csproj。`dotnet build` 不会为 `PackageReference`
> 自动注入编译引用，因此 csproj 中按 NuGet 还原目录显式列出了所需程序集；
> 首次编译前请确保已执行 `dotnet restore`（`dotnet build` 会自动触发）。

## 第三方开源软件

解析 `.slnx` / `.csproj` 时使用微软官方 NuGet 包：

- **Microsoft.Build**（MIT License）：读取 `.sln` 与 MSBuild 项目文件（`ProjectRootElement`）
- **Microsoft.VisualStudio.SolutionPersistence**（MIT License）：读取 `.slnx`

其余功能只用 .NET Framework 自带的 `System.Windows.Forms` / `System.Drawing`。
程序内「帮助 → 第三方开源软件」也有同样的说明。

## 目录

```
Editior/
  MainForm.cs           主窗体：菜单、工具栏、标签、资源管理器、查找面板、Git、会话
  Document.cs           Document / TextFile（编码与换行符）/ EditorSettings / InputDialog
  SyntaxDefinitions.cs  语言定义与注册表（按扩展名 + 文件名匹配）
  SyntaxTokenizer.cs    行级分词
  SyntaxHighlighter.cs  RichTextBox 着色
  Minimap.cs            鸟瞰图
  GitSupport.cs         git 状态、仓库根、BetterGitGUI 定位
  SolutionParser.cs     .sln / .slnx / 项目文件解析
  SettingsForm.cs       设置窗口
  GuidGenerator.cs      原 guidgen 的 GUID 生成逻辑与窗口
```

## 已知限制

- 大文件（>200k 字符）不做语法高亮，避免 RichTextBox 卡顿
- 解决方案树一次性加载项目源码列表，超大解决方案首次展开会略慢
- Git 状态按文件着色；git 未安装时该功能自动失效，不影响编辑
