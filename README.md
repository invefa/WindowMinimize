# WindowMinimize

按自定义全局热键最小化当前前台窗口的 Windows 小工具。常驻托盘，默认热键 `Win+S`，单文件 exe，不依赖 .NET 运行时与任何第三方组件。

## 一、使用

双击 `start.bat` 或直接运行 `dist\WindowMinimize.exe`。程序启动后驻留在任务栏通知区域，按一次热键即可把当时的前台窗口最小化。

首次运行会在 exe 同目录生成 `hotkey.ini`，改完保存立即生效，不必重启。

退出方式：右键托盘图标，选“退出”。

## 二、热键写法

修饰键用 `+` 连接，主键放最后即可，顺序不影响结果；分隔符用 `+` 或 `-` 都行。

| 部分 | 可取值 |
| --- | --- |
| 修饰键 | `Ctrl`（也可写 `Control`）、`Alt`、`Shift`、`Win`（也可写 `Windows`） |
| 主键 | `A` 到 `Z`、`0` 到 `9`、`F1` 到 `F24`、`Space`、`Esc`、`Enter`、`Tab`、`Backspace`、`Delete`、`Insert`、`Home`、`End`、`PageUp`、`PageDown`、四个方向键、`Num0` 到 `Num9`、`Minus`、`Plus` 等 |

写法示例：`Win+Alt+M`、`Ctrl+Shift+Q`、`Ctrl+Alt+F9`、`Alt+Space`、`F11`、`Ctrl+-`。

不带修饰键时只允许功能键（`F1` 到 `F24`），避免抢走正常打字输入。`F12` 是系统保留键，无法注册，程序会直接拒绝。`Alt+Tab`、`Ctrl+Alt+Del`、`Win+L` 这类系统占用组合也会被拒绝。

被 Windows 自己占用的组合（例如 `Win+S` 打开搜索、`Win+D` 显示桌面）不在拒绝之列。这些组合用 `RegisterHotKey` 一定失败，程序改用键盘钩子接管，接管后系统原有功能不再可用。

## 三、配置文件 hotkey.ini

```ini
[general]
Hotkey=Win+S
Mode=minimize
Debug=false
```

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| Hotkey | Win+S | 全局热键，写法见上节 |
| Mode | minimize | `minimize` 最小化到任务栏；`hide` 直接隐藏窗口 |
| AutoStart | 不写入 | 写成 `true` 或 `false` 时，启动时按它同步开机自启项；整行删掉则不动注册表，尊重手动设置 |
| Debug | false | 是否写 `hotkey.log` 调试日志 |

程序生成的默认配置里不含 `AutoStart`。需要开机自启时自己补一行，或者用托盘菜单勾选。

旧版配置里的 `MinimizeToTray` 仍可作为 `AutoStart` 的别名使用。

配置写错时不会打断已有功能：解析失败会保留上一次可用的热键，并在托盘气泡和日志里给出原因。

配置文件默认放在 exe 同目录。若该目录不可写（例如解包到 `C:\Program Files`），自动改用 `%APPDATA%\WindowMinimize\hotkey.ini`。

## 四、托盘菜单

| 菜单项 | 作用 |
| --- | --- |
| 最小化当前窗口 | 等同按一次热键 |
| 还原上次隐藏的窗口 | 把上一次用 `hide` 方式隐藏的窗口放回前台；没有隐藏过窗口时不可点 |
| 热键：xxx | 只显示当前生效的热键 |
| 暂停热键 / 恢复热键 | 临时停用，适合玩游戏或录屏时使用；按住 Shift 点击则暂停 5 分钟后自动恢复 |
| 打开配置文件 | 用记事本打开 `hotkey.ini` |
| 重新载入配置 | 手动重新读取，正常情况不需要 |
| 开机自动启动 | 勾选后写入注册表 `HKCU\...\Run`，只改注册表，不动配置文件 |
| 退出 | 结束程序 |

双击托盘图标同样会最小化当前窗口。

## 五、命令行参数

```
WindowMinimize.exe [选项]

--hotkey <组合>   临时指定热键，覆盖配置文件
--mode <方式>     minimize 或 hide
--config <路径>   指定配置文件
--debug           写调试日志
--no-tray         不显示托盘图标（也可写 --quiet）
--help            帮助
```

## 六、实现要点

热键注册优先用系统 `RegisterHotKey`。若该组合已被别的程序占用（Win32 错误码 1409），自动退回到低级键盘钩子 `WH_KEYBOARD_LL` 抢下按键，保证热键始终可用，此时托盘气泡会说明接管方式。

`RegisterHotKey` 的注册项按“窗口 + id”唯一，换热键要先撤掉旧的再注册新的。新组合用不了时，程序会把上一个还能用的热键恢复回来，改配置文件写错不会弄得没有热键可用。

被 Windows 自己占用的组合（例如 `Win+S` 打开搜索、`Win+D` 显示桌面）走的是同一套回退。这类组合注册必然失败，气泡会写明是被 Windows 占用、接管后系统原有功能不再可用。

键盘钩子吞掉按键后，系统在 Win（或 Alt）按住期间看不到别的按键，松开 Win 会被当成“单独按了一下 Win”而弹出开始面板。程序因此在吞键的同时补一次掩码键（轻敲 Ctrl，Ctrl 已在组合里时改用 Shift），先松 Win 还是先松 S 都不会弹。

最小化按窗口类型分三条路径：常规窗口发 `SW_MINIMIZE`；被 DWM 遮蔽的 UWP 挂起窗口改发 `WM_SYSCOMMAND`/`SC_MINIMIZE`；个别自绘窗口首次最小化无效时，会在 350 毫秒后复查并按需补发或改为放前台后强制最小化。

取前台窗口时会跳过系统界面：任务栏、桌面、开始菜单、搜索，以及本程序自己的窗口（点托盘菜单时前台往往是任务栏或菜单本身）。这些窗口不会被当成最小化目标，以免把系统界面收起来。

## 七、构建

需要 .NET SDK 8，执行：

```
pwsh -NoProfile -File build\build.ps1
```

产物为 `dist\WindowMinimize.exe`，自包含发布，拷到别的 Windows 10/11 机器可直接运行。仓库内 `global.json` 把 SDK 固定到 8.0.403 系列。

单元测试：

```
dotnet test test\unit\WindowMinimize.Tests.csproj
```

目录结构：

```
WindowMinimize
  build        构建脚本与图标生成脚本
  src          源码与 app.ico
  test/e2e     端到端验证脚本，需要真实窗口与全局热键
  test/unit    HotkeySpec 解析的单元测试
  dist         构建产物（不入库）
  start.bat    启动入口
```

## 八、已知限制

一、目标窗口以管理员权限运行时，普通权限的进程无法向其发送消息，最小化会失败。需要处理这类窗口时，请以管理员身份运行本程序。

二、`hide` 方式会把窗口完全隐藏，任务栏上不再有按钮，要用托盘菜单的“还原上次隐藏的窗口”找回。这项记录只留在内存里，程序重启后找不回，只能靠任务管理器。日常建议用默认的 `minimize`。

三、低级键盘钩子属于全局钩子，个别反作弊严格的游戏可能对全局钩子敏感。若热键无需抢键（正常注册成功），程序不会安装钩子。

四、用键盘钩子接管被 Windows 占用的组合（例如 `Win+S`）后，系统原本的功能不再响应，例如搜索面板不再弹出。
