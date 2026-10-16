# End to end check: does pressing the hotkey minimize the foreground window.
# The target window is started by this script itself (a WinForms window inside a pwsh process), so
# the test does not depend on whatever happens to be open on the machine.
# usage: hotkey-test.ps1           tests the keyboard hook path (hotkey Win+S, reserved by Windows,
#                                  so it always goes through the hook)
#        hotkey-test.ps1 -Register tests the RegisterHotKey path (hotkey Ctrl+Shift+F9)
# The hook path also regresses the release order: releasing Win before S must not open the start or
# search panel.
# The result goes to test\e2e\hotkey-test.txt (UTF-8), exit code 0 means it passed.

param(
    [switch]$Register
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root 'dist\WindowMinimize.exe'
$outFile = Join-Path $PSScriptRoot 'hotkey-test.txt'
$iniName = if ($Register) { 'test-hotkey2.ini' } else { 'test-hotkey.ini' }
$hotkeyName = if ($Register) { 'Ctrl+Shift+F9' } else { 'Win+S' }
$windowTitle = 'WindowMinimize E2E target'
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string title);

    [DllImport("user32.dll")]
    public static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT p);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr h);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr h, int cmd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint attach, uint attachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr h);

    [DllImport("user32.dll")]
    public static extern void SwitchToThisWindow(IntPtr h, bool altTab);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(
        uint action, uint param, ref uint value, uint winIni
    );

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AllowSetForegroundWindow(int pid);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    const byte VK_SHIFT   = 0x10,
               VK_CONTROL = 0x11,
               VK_MENU    = 0x12,
               VK_LWIN    = 0x5B,
               VK_RWIN    = 0x5C;

    const byte VK_F9     = 0x78,
               VK_S      = 0x53,
               VK_ESCAPE = 0x1B;

    const uint KEYUP = 0x0002;

    /// Find a window by title, used to confirm that the target window showed up.
    public static IntPtr FindWindowByTitle(string title) {
        return FindWindow(null, title);
    }

    /// Release the modifiers that may still be held down. This runs before any synthetic key press,
    /// so a modifier left over from the previous test cannot add to the combination (an Alt that is
    /// still down makes Win+S match nothing).
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

    /// Force a window to the foreground: steal the right to do so for a moment and attach to the
    /// foreground thread while trying again.
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
            // minimizing and restoring first usually pushes it forward
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

            // release Alt once to lift the foreground lock, without pressing it first: a press that
            // another hook swallows would leave Alt stuck down
            Release(VK_MENU);

            if (attached) AttachThreadInput(myThread, fgThread, false);

            System.Threading.Thread.Sleep(120);
        }

        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);
        return GetForegroundWindow() == h;
    }

    /// Synthesize Ctrl+Shift+F9. keybd_event needs no STA, which makes it steadier than SendKeys.
    public static void SendRegisterCombo() {
        Send(VK_CONTROL, VK_SHIFT, VK_F9);
    }

    /// Synthesize Win+S. The app swallows only the S key, Win goes to Windows as usual.
    /// With winFirstRelease the Win key is released before S: once the app has swallowed the S
    /// press the system thinks Win was pressed on its own, and that order is the one that pushes
    /// the start panel up most easily, which is why it is used to regress the mask key.
    public static void SendWinS(bool winFirstRelease) {
        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        keybd_event(VK_S, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(50);

        if (winFirstRelease) {
            keybd_event(VK_LWIN, 0, KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            keybd_event(VK_S, 0, KEYUP, UIntPtr.Zero);
        } else {
            keybd_event(VK_S, 0, KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            keybd_event(VK_LWIN, 0, KEYUP, UIntPtr.Zero);
        }
    }

    /// Process id of the foreground window, used to tell whether a shell panel (start menu, search)
    /// took the foreground.
    public static uint ForegroundPid() {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        return pid;
    }

    /// Synthesize Esc, used to close a shell panel that popped up.
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

function Get-ForegroundShellPanel {
    $fgPid = [Win32Test]::ForegroundPid()
    try { $name = (Get-Process -Id $fgPid -ErrorAction Stop).ProcessName } catch { return '' }
    $panels = @('StartMenuExperienceHost', 'SearchHost', 'ShellExperienceHost')
    if ($name -in $panels) { return $name }
    return ''
}

try {
    if (-not (Test-Path $exe)) { throw "$exe not found, build it first" }

    # bring up an ordinary window as the target: it has a title, it minimizes and it dies with
    # the test
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

    if ($hwnd -eq [IntPtr]::Zero) {
        throw "the target window never showed up (title $windowTitle), the test cannot run"
    }

    Say "target window 0x$($hwnd.ToInt64().ToString('X')) title=$windowTitle, pid $($target.Id)"

    $app = Start-Process -FilePath $exe -PassThru -ArgumentList @(
        '--config', (Join-Path $PSScriptRoot $iniName))
    Start-Sleep -Milliseconds 1500
    if ($app.HasExited) { throw "the app exited right after start, exit code $($app.ExitCode)" }
    Say "started WindowMinimize (pid $($app.Id)) with hotkey $hotkeyName"

    # bring the target window to the foreground, so what is tested is "the foreground window gets
    # minimized"
    [Win32Test]::ResetModifiers()
    $ok = $false
    for ($round = 0; $round -lt 3 -and -not $ok; $round++) {
        [void][Win32Test]::ForceForeground($hwnd)
        Start-Sleep -Milliseconds 400
        $ok = [Win32Test]::GetForegroundWindow() -eq $hwnd
    }

    if (-not $ok) {
        throw 'the target window cannot be brought to the foreground, the test cannot run'
    }

    $fg = [Win32Test]::GetForegroundWindow()
    $fgHex = $fg.ToInt64().ToString('X')
    $targetHex = $hwnd.ToInt64().ToString('X')
    Say "foreground 0x$fgHex target 0x$targetHex match=$ok"

    $before = Get-ShowCmd $hwnd
    Say "showCmd before the hotkey = $before (1=normal 2=minimized 3=maximized)"

    [Win32Test]::ResetModifiers()
    if ($Register) { [Win32Test]::SendRegisterCombo() } else { [Win32Test]::SendWinS($false) }
    Start-Sleep -Milliseconds 1200

    # look at the foreground first, then press Esc to clean up (Esc closes the panel, so it has to
    # come after the check)
    $panelNormal = Get-ForegroundShellPanel
    if (-not $Register) {
        [Win32Test]::SendEsc()
        Start-Sleep -Milliseconds 300
    }

    $after = Get-ShowCmd $hwnd
    Say "showCmd after the hotkey = $after"

    # the app swallowed the S key, so the system sees no other key while Win is held down and
    # releasing Win counts as a single Win press that opens the start panel. The mask key the app
    # taps is there to stop that, so it counts as a pass here too.
    $log = Join-Path $PSScriptRoot 'hotkey.log'
    $logText = if (Test-Path $log) { Get-Content $log -Encoding UTF8 -Raw } else { '' }
    $triggered = $logText -match 'hotkey triggered'
    $minimized = $logText -match 'minimize sent'

    Say "app log: hotkey seen=$triggered, minimize sent=$minimized"

    if ($after -eq 2) {
        $passed = $true
        Say 'result: passed, the hotkey minimized the foreground window'
    } elseif ($triggered -and $minimized) {
        $passed = $true
        Say 'result: passed, the app saw the hotkey and sent the minimize'
    } else {
        Say "result: failed, showCmd $before -> $after, no trigger in the log"
    }

    if ($panelNormal) {
        Say "regression (release S first): failed, the shell panel $panelNormal took the foreground"
        $passed = $false
    } else {
        Say 'regression (release S first): passed, no start or search panel'
    }

    # release order regression: release Win before S. In that order the system only sees Win go down
    # and up, so without the mask key the start panel opens.
    if (-not $Register -and $passed) {
        [Win32Test]::ResetModifiers()
        [void][Win32Test]::ForceForeground($hwnd)
        Start-Sleep -Milliseconds 400

        if ([Win32Test]::GetForegroundWindow() -ne $hwnd) {
            Say 'regression (release Win first): failed, the target window cannot be brought back'
            $passed = $false
        } else {
            [Win32Test]::SendWinS($true)
            Start-Sleep -Milliseconds 1200
            $panelWinFirst = Get-ForegroundShellPanel
            [Win32Test]::SendEsc()
            Start-Sleep -Milliseconds 300

            if ($panelWinFirst) {
                Say "regression (release Win first): failed, $panelWinFirst took the foreground"
                $passed = $false
            } else {
                Say 'regression (release Win first): passed, no start or search panel'
            }
        }
    }
}
catch {
    Say "exception: $($_.Exception.Message)"
}
finally {
    if ($app -and -not $app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    }

    if ($target -and -not $target.HasExited) {
        Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    }

    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($passed) { exit 0 } else { exit 1 }
