# AGENTS.md

[English](AGENTS.md) | [简体中文](AGENTS.zh-Hans.md)

## 工作流

本项目启用 agent-workflow。开始任务时读取
[工作流入口](https://github.com/invefa/agent-workflow/blob/main/README.md)，按任务读取对应专题：

- 界面与控件：[图形界面](https://github.com/invefa/agent-workflow/blob/main/standards/ui.md)
- 代码、配置和持久数据：[工程标准](https://github.com/invefa/agent-workflow/blob/main/standards/engineering.md)
- 文档、命名和视觉表达：[书写与组织](https://github.com/invefa/agent-workflow/blob/main/standards/writing.md)
- 验证、提交和交付：[验证与交付](https://github.com/invefa/agent-workflow/blob/main/standards/delivery.md)

个人偏好以[个人偏好](https://github.com/invefa/agent-workflow/blob/main/preferences.md)为准。

## 项目概况

WindowMinimize是Windows托盘工具，使用C#、.NET8和WinForms。程序支持自定义全局热键、最小化窗口、屏幕切换、最小化历史、设置界面和GitHub更新。

源码在`src`，单元测试在`test/unit`，桌面端到端测试在`test/e2e`，构建入口为`build/build.ps1`。

`src`按职责分目录，所有文件统一使用`WindowMinimize`命名空间：

| 目录 | 职责 |
|---|---|
| `app` | 程序入口、托盘外壳、单实例、版本、日志 |
| `config` | 设置模型与`hotkey.ini`存取 |
| `hotkey` | 快捷键语法与全局热键注册 |
| `window` | Win32声明、窗口操作、任务栏互操作 |
| `ui` | 设置对话框、自定义控件、托盘图标、文案访问 |
| `update` | GitHub版本检查与安装 |
| `Resources` | 仅资源字符串表，不含代码 |

## 构建与验证

```powershell
dotnet build src\WindowMinimize.csproj -c Release --nologo
dotnet test test\unit\WindowMinimize.Tests.csproj --nologo -q
pwsh -NoProfile -File build\build.ps1
pwsh -NoProfile -File build\build.ps1 -Lite
```

`test/e2e`需要真实桌面、前台窗口和全局热键，按受影响范围运行。界面改动要在实际窗口尺寸下检查。
新增自定义控件时，在`test/preview`中建立预览，并在交付时给出运行命令。

## 变更规则

- 代码、注释、日志和提交信息用英文；用户可见文案放在`src/Resources`资源中，源码不直接写中文界面文案。
- 使用4个空格缩进，行宽上限100列，C#采用K&R风格；沿用现有WinForms控件和项目结构。
- 修改行为先检查同类入口和共享校验，避免只修一个触发路径。
- 每次代码改动完成后使用英文约定式提交标题提交，不把无关格式清理混入功能提交。
- 小型修补和界面调整先完成实现、构建和必要验证，等待用户验收确认后再发布。未验收前不提升版本号，不打标签，不创建GitHub Release，不推送发布资产。
- 发布前从最终提交重新构建，核对版本号、Release资产和更新检测所用的文件名；发布后核对实际Release状态。

## 字符与表达

- 不用emoji或装饰性Unicode字符，只用键盘可直接输入的字符；箭头写`->`。
- 中文正文使用中文标点；代码、路径、URL和命令内部保持半角。
- 英文与汉字之间不写空格。
- 回复先给结论，再给必要依据、验证和边界；不写过程流水账。
- 列表条目名称与解释分开放，层级用间距、线宽和字号表达，颜色只表达稳定语义。
- 文件地址使用可点击的Markdown链接，仓库文档使用相对路径。

## 界面约定

- 设置页优先使用现有WinForms控件、系统颜色和项目资源，不引入固定色板或装饰性重色。
- 自定义控件先在`test/preview`中展示固定示例和交互状态，再接入设置页；交付时给出运行命令。
- 独立帮助文档使用标准Windows标题栏帮助按钮。`HELP.html`和`HELP.zh-Hans.html`作为可维护源文件，同时作为程序内嵌回退。
- 检查窗口在100%、125%和150%缩放下的对齐、截断和点击区域；视觉改动以实际运行截图验收。
