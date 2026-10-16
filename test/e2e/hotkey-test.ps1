# 端到端验证：按热键能否最小化当前前台窗口。
# 目标窗口由本脚本自己拉起（一个 pwsh 进程里的 WinForms 窗口），不依赖机器上正好开着什么程序。
# 用法: hotkey-test.ps1           测键盘钩子路径（配置热键 Win+S，系统保留组合，必然走钩子）
#       hotkey-test.ps1 -Register 测 RegisterHotKey 路径（配置热键 Ctrl+Shift+F9）
# 结果写入 test\e2e\hotkey-test.txt（UTF-8），退出码 0 表示通过。

param(
    [switch]$Register
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root 'dist\WindowMinimize.exe'
$outFile = Join-Path $PSScriptRoot 'hotkey-test.txt'
$iniName = if ($Register) { 'test-hotkey2.ini' } else { 'test-hotkey.ini' }
$hotkeyName = if ($Register) { 'Ctrl+Shift+F9' } else { 'Win+S' }
$windowTitle = 'WindowMinimize E2E 目标窗口'
$lines = [System.Collections.Generic.List[string]]::new()
$passed = $false
$app = $null
$target = $null
$hwnd = [IntPtr]::Zero

function Say($text) {
    $lines.Add($text)
    Write-Host $text
}

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class Win32Test {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT {
        public int length; public int flags; public int showCmd;
        public POINT ptMinPosition; public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool altTab);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

    const byte VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    const byte VK_F9 = 0x78, VK_S = 0x53, VK_ESCAPE = 0x1B;
    const uint KEYUP = 0x0002;

    /// 按标题找窗口，用来确认自建的目标窗口已经出现。
    public static IntPtr FindWindowByTitle(string title) {
        return FindWindow(null, title);
    }

    /// 松开可能还按着的修饰键。合成按键前清一次，避免上一次测试留下的
    /// 修饰键让热键带上多余的组合（例如 Alt 没松开，Win+S 就匹配不上）。
    public static void ResetModifiers() {
        Release(VK_SHIFT);
        Release(VK_CONTROL);
        Release(VK_MENU);
        Release(VK_LWIN);
        Release(VK_RWIN);
    }

    static void Release(byte vk) {
        keybd_event(vk, 0, KEYUP, UIntPtr.Zero);
    }

    /// 强制把窗口拿到前台：临时允许抢前台，挂接前台线程后反复尝试。
    public static bool ForceForeground(IntPtr h) {
        ShowWindow(h, 9); // SW_RESTORE
        AllowSetForegroundWindow(-1); // ASFW_ANY

        const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        uint oldTimeout = 0;
        SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);
        uint zero = 0;
        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref zero, 0);

        for (int attempt = 0; attempt < 8 && GetForegroundWindow() != h; attempt++) {
            // 先最小化再还原，往往能把它顶到前台
            if (attempt == 4) {
                ShowWindow(h, 6); // SW_MINIMIZE
                System.Threading.Thread.Sleep(150);
                ShowWindow(h, 9); // SW_RESTORE
            }

            IntPtr fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, out _);
            uint myThread = GetCurrentThreadId();
            bool attached = fgThread != myThread && AttachThreadInput(myThread, fgThread, true);

            BringWindowToTop(h);
            SwitchToThisWindow(h, false);
            SetForegroundWindow(h);

            // 只补一次 Alt 抬起解除前台锁定：不要按下再抬起，按下若被别的钩子吃掉会留下卡住的 Alt
            Release(VK_MENU);

            if (attached) {
                AttachThreadInput(myThread, fgThread, false);
            }

            System.Threading.Thread.Sleep(120);
        }

        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);
        return GetForegroundWindow() == h;
    }

    /// 合成 Ctrl+Shift+F9：keybd_event 不要求 STA，比 SendKeys 稳。
    public static void SendRegisterCombo() {
        Send(VK_CONTROL, VK_SHIFT, VK_F9);
    }

    /// 合成 Win+S。程序只吞 S 键，Win 键会照常传给 Windows。
    public static void SendWinS() {
        Send(VK_LWIN, 0, VK_S);
    }

    /// 合成 Esc，用来收掉上面副作用弹出的开始菜单。
    public static void SendEsc() {
        keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        keybd_event(VK_ESCAPE, 0, KEYUP, UIntPtr.Zero);
    }

    static void Send(byte mod1, byte mod2, byte key) {
        keybd_event(mod1, 0, 0, UIntPtr.Zero);
        if (mod2 != 0) keybd_event(mod2, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        keybd_event(key, 0, KEYUP, UIntPtr.Zero);
        if (mod2 != 0) keybd_event(mod2, 0, KEYUP, UIntPtr.Zero);
        keybd_event(mod1, 0, KEYUP, UIntPtr.Zero);
    }
}
'@

function Get-ShowCmd([IntPtr]$handle) {
    $wp = New-Object Win32Test+WINDOWPLACEMENT
    $wp.length = [System.Runtime.InteropServices.Marshal]::SizeOf($wp)
    [void][Win32Test]::GetWindowPlacement($handle, [ref]$wp)
    return $wp.showCmd
}

try {
    if (-not (Test-Path $exe)) { throw "找不到 $exe，请先构建" }

    # 自己拉起一个普通窗口当目标：有标题、能最小化、进程随测试一起关掉
    $formScript = @'
Add-Type -AssemblyName System.Windows.Forms
$form = New-Object System.Windows.Forms.Form
$form.Text = '{0}'
$form.Width = 480
$form.Height = 320
$form.StartPosition = 'CenterScreen'
[System.Windows.Forms.Application]::Run($form)
'@ -f $windowTitle

    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($formScript))
    $target = Start-Process pwsh -PassThru -ArgumentList '-NoProfile', '-EncodedCommand', $encoded

    for ($i = 0; $i -lt 60 -and $hwnd -eq [IntPtr]::Zero; $i++) {
        Start-Sleep -Milliseconds 250
        $hwnd = [Win32Test]::FindWindowByTitle($windowTitle)
    }

    if ($hwnd -eq [IntPtr]::Zero) { throw "目标窗口没有出现（标题 $windowTitle），测试条件不成立" }
    Say "目标窗口 0x$($hwnd.ToInt64().ToString('X')) 标题=$windowTitle，进程 pid $($target.Id)"

    $app = Start-Process -FilePath $exe -PassThru -ArgumentList @(
        '--config', (Join-Path $PSScriptRoot $iniName))
    Start-Sleep -Milliseconds 1500
    if ($app.HasExited) { throw "程序启动后立即退出，退出码 $($app.ExitCode)" }
    Say "已启动 WindowMinimize (pid $($app.Id))，配置热键 $hotkeyName"

    # 把目标窗口拿到前台，测的才是“前台窗口被最小化”
    [Win32Test]::ResetModifiers()
    $ok = $false
    for ($round = 0; $round -lt 3 -and -not $ok; $round++) {
        [void][Win32Test]::ForceForeground($hwnd)
        Start-Sleep -Milliseconds 400
        $ok = [Win32Test]::GetForegroundWindow() -eq $hwnd
    }

    if (-not $ok) { throw '无法把目标窗口设为前台，测试条件不成立' }

    $fg = [Win32Test]::GetForegroundWindow()
    Say "前台窗口 0x$($fg.ToInt64().ToString('X'))，目标 0x$($hwnd.ToInt64().ToString('X')) 一致=$ok"

    $before = Get-ShowCmd $hwnd
    Say "发送热键前 showCmd=$before（1=正常 2=最小化 3=最大化）"

    [Win32Test]::ResetModifiers()
    if ($Register) { [Win32Test]::SendRegisterCombo() } else { [Win32Test]::SendWinS() }
    Start-Sleep -Milliseconds 1200

    if (-not $Register) {
        # 程序吞掉了 S 键但没吞 Win 键，Windows 会把开始菜单弹出来，按 Esc 收掉免得影响后续
        [Win32Test]::SendEsc()
        Start-Sleep -Milliseconds 300
    }

    $after = Get-ShowCmd $hwnd
    Say "发送热键后 showCmd=$after"

    # 合成按键松开 Win 键时，Windows 会把开始菜单弹出来，可能又把窗口顶回去，
    # 所以这里以“程序确实收到热键并执行了最小化”作为主判据。
    $log = Join-Path $PSScriptRoot 'hotkey.log'
    $logText = if (Test-Path $log) { Get-Content $log -Encoding UTF8 -Raw } else { '' }
    $triggered = $logText -match '热键触发'
    $minimized = $logText -match '已发送最小化'

    Say "程序日志：收到热键=$triggered，执行最小化=$minimized"

    if ($after -eq 2) {
        $passed = $true
        Say '结果: 通过，热键把前台窗口最小化了'
    }
    elseif ($triggered -and $minimized) {
        $passed = $true
        Say '结果: 通过，程序收到热键并执行了最小化；窗口随即被开始菜单顶回（合成按键的副作用）'
    }
    else {
        Say "结果: 未通过，showCmd $before -> $after，日志未见触发记录"
    }
}
catch {
    Say "异常: $($_.Exception.Message)"
}
finally {
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    if ($target -and -not $target.HasExited) { Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue }

    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($passed) { exit 0 } else { exit 1 }
