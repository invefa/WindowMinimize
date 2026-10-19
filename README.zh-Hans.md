# WindowMinimize

[English](README.md) | [简体中文](README.zh-Hans.md)

一个用自定义全局热键最小化当前前台窗口的Windows小工具。程序驻留在托盘，默认热键为`Win+S`，发布为单文件exe，不依赖.NET运行时或任何第三方组件。

从[发布页](https://github.com/invefa/WindowMinimize/releases/latest)下载完整版
（`WindowMinimize-<version>-win-x64.exe`）直接运行，无需安装。同一个页面上的
`WindowMinimize-<version>-win-x64-lite.exe`约240KB，但需要机器安装.NET8 Desktop Runtime。两个版本都可以从源码构建，见第八节。

## 一、使用

双击`start.bat`，或直接运行`dist\WindowMinimize.exe`。启动后程序停留在任务栏通知区域，按一次热键即可最小化当时的前台窗口。

遇到以管理员权限运行的窗口时，用`start-admin.bat`启动程序，或使用托盘菜单中的
`以管理员身份重启`。当前台是这类窗口时，Windows不会把热键传给普通权限程序。要每次登录都以管理员权限静默启动，先开启`开机自动启动`，再勾选`以管理员身份开机自启`。

首次运行会在exe旁创建`hotkey.ini`。保存修改后立即生效，不需要重启。左键单击托盘图标会打开设置界面，可以直接修改热键、方式、语言、通知分类、自启和调试日志，不必编辑文件。

退出：右击托盘图标，选择`退出`。

## 二、热键语法

用`+`连接修饰键，主键放在最后，顺序不影响结果。也可以用`-`作为分隔符。

| 部分 | 取值 |
| --- | --- |
| 修饰键 | `Ctrl`（也可写`Control`）、`Alt`、`Shift`、`Win`（也可写`Windows`） |
| 主键 | `A`到`Z`、`0`到`9`、`F1`到`F24`、`Space`、`Esc`、`Enter`、`Tab`、`Backspace`、`Delete`、`Insert`、`Home`、`End`、`PageUp`、`PageDown`、四个方向键、`Num0`到`Num9`、`Minus`、`Plus`等 |

示例：`Win+Alt+M`、`Ctrl+Shift+Q`、`Ctrl+Alt+F9`、`Alt+Space`、`F11`、`Ctrl+-`。

不带修饰键时只允许功能键，即`F1`到`F24`，避免抢走正常输入。`F12`是系统保留键，无法注册，程序会直接拒绝。
`Alt+Tab`、`Ctrl+Alt+Del`、`Win+L`这类系统保留组合也会被拒绝。

Windows自身占用的组合不会被拒绝，例如用`Win+S`打开搜索，或用`Win+D`显示桌面。
`RegisterHotKey`对这类组合总是失败，程序会改用键盘钩子接管；接管后系统原有功能不再可用。

## 三、配置文件 hotkey.ini

```ini
[general]
Hotkey=Win+S
Mode=minimize
Language=auto
NotifyStartup=true
NotifyErrors=true
NotifyPermissions=true
Debug=false
```

| 设置 | 默认值 | 说明 |
| --- | --- | --- |
| Hotkey | Win+S | 全局热键，语法见上一节 |
| Mode | minimize | `minimize`最小化到任务栏；`hide`直接隐藏窗口 |
| Language | auto | 界面语言：`auto`跟随Windows，`en`或`zh`强制指定；在设置界面修改后立即更新托盘 |
| NotifyStartup | true | 是否显示启动时的热键确认气泡 |
| NotifyErrors | true | 是否显示失败气泡，例如热键未能注册 |
| NotifyPermissions | true | 是否显示权限相关气泡，例如提权目标或计划任务变更 |
| AutoStart | 不写入 | 设为`true`或`false`时，启动时按该值同步自启项；删除整行则不碰注册表，保留手动设置 |
| Debug | false | 是否写入`hotkey.log`调试日志 |

程序生成的默认配置不包含`AutoStart`。需要开机自启时，可以自己加这一行，或在托盘菜单中勾选。

旧配置中的`MinimizeToTray`仍可作为`AutoStart`的别名使用。

配置损坏不会破坏已有行为：解析失败时保留上一次可用的热键，原因会显示在托盘气泡和日志中。

配置文件默认位于exe旁。如果该目录不可写，例如解压到`C:\Program Files`，会退回到`%APPDATA%\WindowMinimize\hotkey.ini`。

## 四、托盘菜单

| 菜单项 | 作用 |
| --- | --- |
| 版本 | 顶部不可点击的标题，显示版本号以及当前是否为管理员权限 |
| 隐藏的窗口 | 子菜单列出每个用`hide`方式隐藏的窗口；选择一项即可恢复，`恢复所有隐藏窗口`可一次全部恢复。列表只保存在内存中，程序退出后丢失 |
| 热键：xxx | 只显示当前生效的热键 |
| 暂停热键 / 恢复热键 | 临时禁用热键，适合游戏或录制时使用；按住Shift点击可暂停5分钟，之后自动恢复 |
| 设置 | 打开设置界面，可修改热键、方式、语言、通知分类、普通或管理员自启和调试日志 |
| 打开配置文件 | 用记事本打开`hotkey.ini` |
| 重新载入配置 | 手动重新读取配置；通常不需要 |
| 开机自动启动 | 自启总开关。勾选后用普通Run项随Windows启动；取消后关闭自启 |
| 以管理员身份开机自启 | 仅在自启开启时可用。切换到管理员登录任务：请求一次管理员权限，注册最高运行级别的计划任务，并移除普通Run项 |
| 以管理员身份重启 | 通过UAC关闭并重新启动程序，以便处理管理员权限窗口；当前已是管理员时不可点击 |
| 退出 | 结束程序 |

左键单击托盘图标会打开设置界面。

## 五、命令行参数

```
WindowMinimize.exe [选项]

--hotkey <组合>     本次运行使用的热键，覆盖配置文件
--mode <方式>       minimize或hide
--config <路径>     使用指定的配置文件
--lang <语言>       界面语言，en或zh，默认跟随系统
--debug             写入调试日志
--no-tray           不显示托盘图标，也可写作--quiet
--help              显示帮助
```

## 六、语言

界面默认跟随系统语言：中文Windows显示中文，其他语言显示英文。`hotkey.ini`中的
`Language=auto`跟随Windows，`Language=en`或`Language=zh`强制指定一种；在设置界面修改该值后，托盘菜单会立即重建。
`--lang en`或`--lang zh`可以在单次运行中覆盖配置文件，例如在英文系统上临时使用中文界面。

只有用户可见的内容会被翻译：托盘菜单、气泡通知、设置界面、用法说明以及热键失败提示。源代码和`hotkey.ini`保持英文。

两种语言都放在`src\Resources`：`Strings.resx`是英文，`Strings.zh-Hans.resx`是中文。
中文资源以`zh-Hans`附属程序集的形式打进单文件，所以exe复制到另一台机器后仍能跟随系统语言。
系统语言从.NET全球化数据读取，Windows10 1903及以后版本自带该数据。

## 七、实现说明

热键注册优先使用系统`RegisterHotKey`。如果组合已被其他程序占用（Win32错误1409），
自动回退到低级键盘钩子`WH_KEYBOARD_LL`抓取按键，保证热键仍然可用；托盘气泡会说明接管方式。

一次`RegisterHotKey`注册按窗口和标识唯一，所以修改热键时先注销旧组合，再注册新组合。新组合不可用时，程序恢复上一次可用的热键，因此配置写错也不会让程序失去可用热键。

Windows自身占用的组合，例如`Win+S`打开搜索或`Win+D`显示桌面，走同一套回退逻辑。这类组合的注册总是失败；气泡会说明组合由系统占用，接管后原有功能不可用。

键盘钩子吞掉按键后，系统在Win或Alt按住期间看不到其他按键，于是把松开Win当成单独按下Win，弹出开始面板。
程序在吞键时额外发送一个掩码键（Ctrl轻点，如果组合已含Ctrl则用Shift），因此无论先松开Win还是先松开S，都不会弹出开始面板。

最小化按窗口类型分三条路径：普通窗口发送`SW_MINIMIZE`；被DWM遮挡的UWP挂起窗口改用
`WM_SYSCOMMAND`/`SC_MINIMIZE`；少数自绘窗口第一次最小化无效，程序在350毫秒后复查，必要时重新发送，或把窗口切到前台后强制最小化。

`ShowWindowAsync(SW_MINIMIZE)`被拒绝时，程序立即改发`WM_SYSCOMMAND`/`SC_MINIMIZE`，而不是等待重试；有些窗口只拒绝第一次调用。
两条路径都失败时，日志记录拒绝信息，并用一次气泡说明目标窗口可能是管理员权限，可通过`以管理员身份重启`处理。

前台是管理员权限窗口是另一项限制：Windows不会把它的按键传给普通权限程序的键盘钩子。因此这类窗口获得焦点时热键没有反应；以管理员权限重启程序即可解除限制。

管理员自启项会注册名为`WindowMinimize`的计划任务，登录时以最高运行级别启动，然后删除普通Run值，保证只启动一份。
取消勾选会注销该任务。两个方向都通过隐藏的提权PowerShell辅助进程完成，并请求一次管理员权限。

获取前台窗口时，程序会跳过系统界面：任务栏、桌面、开始菜单、搜索以及程序自己的窗口。点击托盘菜单时，前台通常是任务栏或菜单本身。这些窗口永远不会被当作最小化目标，因此系统界面不会被收起。

## 八、构建

需要.NET SDK8。运行：

```
pwsh -NoProfile -File build\build.ps1
```

输出为`dist\WindowMinimize.exe`，一个自包含版本，复制到另一台Windows10/11机器即可直接运行。仓库中的`global.json`把SDK固定在8.0.403系列。

加`-Lite`会生成`dist\lite\WindowMinimize-lite.exe`，这是依赖框架的单文件版本，运行机器需要.NET8 Desktop Runtime。
它放在单独目录，因为单文件发布会先清空输出目录。

推送与`src\WindowMinimize.csproj`中`<Version>`一致的标签，例如`v1.2.1`，会触发
`.github\workflows\release.yml`运行单元测试、构建两个exe、提取`CHANGELOG.md`中对应版本的段落，并把exe附加到Release。

单元测试：

```
dotnet test test\unit\WindowMinimize.Tests.csproj
```

目录结构：

```
WindowMinimize
  build        构建脚本和图标生成脚本
  src          源代码、app.ico以及Resources下的字符串表
  test/e2e     端到端验证脚本，需要真实窗口和全局热键
  test/unit    热键解析和字符串表的单元测试
  dist         构建输出，不提交
  start.bat    启动入口
  start-admin.bat  以管理员权限启动，用于处理提权窗口
```

## 九、已知限制

1. 当前台是管理员权限窗口时，Windows禁止普通权限程序捕获热键，也禁止它最小化该窗口。要处理这类窗口，请以管理员权限运行本程序：
使用托盘菜单中的`以管理员身份重启`或`start-admin.bat`。需要每次登录时静默以管理员权限启动，先开启`开机自动启动`，再勾选
`以管理员身份开机自启`；程序会注册最高运行级别的计划任务，并移除普通Run项。

2. `hide`方式会彻底隐藏窗口，任务栏上不留下按钮；需要恢复时使用托盘菜单中的`隐藏的窗口`子菜单。
该列表只保存在内存中，程序重启后丢失，只剩任务管理器可用。日常使用建议保持默认的`minimize`。

3. 低级键盘钩子是全局钩子，少数带有严格反作弊的游戏可能对全局钩子敏感。如果普通注册已经成功、不需要抓取按键，程序不会安装钩子。

4. 键盘钩子接管Windows占用的组合后，例如`Win+S`，系统原有功能不再响应，例如搜索面板不再弹出。
