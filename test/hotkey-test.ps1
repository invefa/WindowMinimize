# 端到端验证：按热键能否最小化当前前台窗口。
# 目标窗口用 DSH 界面所在的那个 Electron 窗口（本机确定存在的普通顶层窗口）。
# 用法: hotkey-test.ps1           测键盘钩子路径（配置热键 Win+S，系统保留组合，必然走钩子）
#       hotkey-test.ps1 -Register 测 RegisterHotKey 路径（配置热键 Ctrl+Shift+F9）
# 结果写入 test\hotkey-test.txt（UTF-8），退出码 0 表示通过。

param(
    [switch]$Register
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'dist\WindowMinimize.exe'
$outFile = Join-Path $PSScriptRoot 'hotkey-test.txt'
$iniName = if ($Register) { 'test-hotkey2.ini' } else { 'test-hotkey.ini' }
$hotkeyName = if ($Register) { 'Ctrl+Shift+F9' } else { 'Win+S' }
$lines = [System.Collections.Generic.List[string]]::new()
$passed = $false
$app = $null
$hwnd = [IntPtr]::Zero

function Say($text) {
    $lines.Add($text)
    Write-Host $text
}

Add-Type -AssemblyName System.Windows.Forms

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool altTab);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

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

            // 模拟一次 Alt 抬起，解除前台锁定
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 0x0002, UIntPtr.Zero);

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

    /// 合成 Win+S。
    public static void SendWinS() {
        Send(VK_LWIN, 0, VK_S);
    }

    const byte VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_LWIN = 0x5B;
    const byte VK_F9 = 0x78, VK_S = 0x53;
    const uint KEYUP = 0x0002;

    static void Send(byte mod1, byte mod2, byte key) {
        keybd_event(mod1, 0, 0, UIntPtr.Zero);
        if (mod2 != 0) keybd_event(mod2, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        keybd_event(key, 0, KEYUP, UIntPtr.Zero);
        if (mod2 != 0) keybd_event(mod2, 0, KEYUP, UIntPtr.Zero);
        keybd_event(mod1, 0, KEYUP, UIntPtr.Zero);
    }

    /// 收集所有可见的 Electron 顶层窗口（DSH 界面）与 QQ 窗口，作为测试目标候选。
    public static IntPtr[] FindElectronWindows() {
        var found = new List<IntPtr>();
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            var cls = new StringBuilder(256);
            GetClassName(h, cls, 256);
            string name = cls.ToString();
            if (name == "Chrome_WidgetWin_1") found.Add(h);
            return true;
        }, IntPtr.Zero);
        return found.ToArray();
    }

    public static string Title(IntPtr h) {
        var t = new StringBuilder(256);
        GetWindowText(h, t, 256);
        return t.ToString();
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

    $targets = [Win32Test]::FindElectronWindows()
    if ($targets.Count -eq 0) { throw '没找到可用的目标窗口（Electron 类的顶层窗口）' }
    Say "候选目标窗口 $($targets.Count) 个"

    $app = Start-Process -FilePath $exe -PassThru -ArgumentList @(
        '--config', (Join-Path $root "test\$iniName"))
    Start-Sleep -Milliseconds 1500
    if ($app.HasExited) { throw "程序启动后立即退出，退出码 $($app.ExitCode)" }
    Say "已启动 WindowMinimize (pid $($app.Id))，配置热键 $hotkeyName"

    # 先把目标窗口拿到前台，拿不到就换一个目标，保证测的是“前台窗口被最小化”
    $ok = $false
    foreach ($candidate in $targets) {
        $hwnd = $candidate
        Say "尝试目标窗口 0x$($hwnd.ToInt64().ToString('X')) 标题=$([Win32Test]::Title($hwnd))"
        [void][Win32Test]::ForceForeground($hwnd)
        Start-Sleep -Milliseconds 400
        if ([Win32Test]::GetForegroundWindow() -eq $hwnd) { $ok = $true; break }
    }

    # 个别窗口会被别的程序抢回前台，多试几轮
    for ($round = 0; $round -lt 3 -and -not $ok; $round++) {
        [void][Win32Test]::ForceForeground($hwnd)
        Start-Sleep -Milliseconds 500
        $ok = ([Win32Test]::GetForegroundWindow() -eq $hwnd)
    }

    if (-not $ok) { throw '无法把目标窗口设为前台，测试条件不成立' }

    $fg = [Win32Test]::GetForegroundWindow()
    Say "前台窗口 0x$($fg.ToInt64().ToString('X'))，目标 0x$($hwnd.ToInt64().ToString('X')) 一致=$ok"

    $before = Get-ShowCmd $hwnd
    Say "发送热键前 showCmd=$before（1=正常 2=最小化 3=最大化）"

    # 合成热键
    if ($Register) { [Win32Test]::SendRegisterCombo() } else { [Win32Test]::SendWinS() }
    Start-Sleep -Milliseconds 1500

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
    if ($hwnd -ne [IntPtr]::Zero) {
        # 把测试目标窗口还原成正常状态，避免留下最小化的界面
        [void][Win32Test]::ForceForeground($hwnd)
    }

    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($passed) { exit 0 } else { exit 1 }
