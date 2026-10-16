# Checks every --hotkey spelling one by one, from parsing down to the registration result.
# The result goes to test\e2e\parse-test.txt (UTF-8), exit code 0 means everything passed.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root 'dist\WindowMinimize.exe'
$cfg = Join-Path $PSScriptRoot 'parse-test.ini'
# the log always sits next to the config file and is called hotkey.log
$log = Join-Path $PSScriptRoot 'hotkey.log'
$outFile = Join-Path $PSScriptRoot 'parse-test.txt'
$lines = [System.Collections.Generic.List[string]]::new()
$failures = 0

function Say($text) {
    $lines.Add($text)
    Write-Host $text
}

# spelling -> the canonical hotkey text it parses to, $null when parsing has to fail
# note: Ctrl+Alt+M, Alt+Space and friends are already registered by other programs on this machine,
# so this also walks the "combination is taken, use the keyboard hook" path
$cases = @(
    @{ arg = 'Ctrl+Alt+M';     expect = 'Ctrl+Alt+M' },
    @{ arg = 'ctrl+alt+m';     expect = 'Ctrl+Alt+M' },
    @{ arg = 'Win+Alt+M';      expect = 'Win+Alt+M' },
    @{ arg = 'Win+S';          expect = 'Win+S' },
    @{ arg = 'Ctrl+C';         expect = 'Ctrl+C' },
    @{ arg = 'Alt+A';          expect = 'Alt+A' },
    @{ arg = 'Alt+Ctrl+M';     expect = 'Ctrl+Alt+M' },
    @{ arg = 'M+Ctrl+Alt';     expect = 'Ctrl+Alt+M' },
    @{ arg = 'Ctrl-Alt-M';     expect = 'Ctrl+Alt+M' },
    @{ arg = 'Ctrl+Ctrl+M';    expect = 'Ctrl+M' },
    @{ arg = 'Ctrl+Shift+F10'; expect = 'Ctrl+Shift+F10' },
    @{ arg = 'F9';             expect = 'F9' },
    @{ arg = 'Alt+Space';      expect = 'Alt+Space' },
    @{ arg = 'Ctrl+Minus';     expect = 'Ctrl+Minus' },
    @{ arg = 'Ctrl+-';         expect = 'Ctrl+Minus' },
    @{ arg = 'Ctrl++';         expect = 'Ctrl+Plus' },
    @{ arg = 'Ctrl+Alt+Win+M'; expect = 'Win+Ctrl+Alt+M' },
    @{ arg = 'Ctrl+Num5';      expect = 'Ctrl+Num5' },
    @{ arg = 'M';              expect = $null },
    @{ arg = 'M+N';            expect = $null },
    @{ arg = 'Ctrl+M+N';       expect = $null },
    @{ arg = 'Ctrl+Alt';       expect = $null },
    @{ arg = 'F12';            expect = $null },
    @{ arg = 'Ctrl+Foo';       expect = $null },
    @{ arg = 'Alt+Tab';        expect = $null }
)

function Get-LogLine([string]$pattern) {
    for ($i = 0; $i -lt 40; $i++) {
        if (Test-Path $log) {
            $hit = Select-String -Path $log -Pattern $pattern -Encoding UTF8 |
                Select-Object -First 1
            if ($hit) { return $hit.Line }
        }

        Start-Sleep -Milliseconds 150
    }

    return ''
}

# the process of the previous case has just been killed, the exe may still be locked, so retry
function Start-App([string[]]$arguments) {
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            return Start-Process -FilePath $exe -PassThru -ArgumentList $arguments
        }
        catch {
            Start-Sleep -Milliseconds 300
        }
    }

    throw "cannot start $exe, the file stayed locked"
}

try {
    if (-not (Test-Path $exe)) { throw "$exe not found, build it first" }

    # every run writes a clean config: AutoStart is left out so the test never touches the registry
    Set-Content -Path $cfg -Value @(
        '[general]', 'Hotkey=Win+Alt+M', 'Mode=minimize', 'Debug=false'
    ) -Encoding UTF8

    foreach ($case in $cases) {
        Remove-Item $log -ErrorAction SilentlyContinue
        $p = Start-App @(
            '--hotkey', $case.arg, '--config', $cfg, '--debug', '--no-tray', '--lang', 'en'
        )

        # wait for the config to load: the startup lines are "WindowMinimize started..." and
        # "hotkey X parsed as Y"
        $startLine = Get-LogLine 'WindowMinimize started'
        $parseLine = Get-LogLine 'parsed as'
        $registerLine = Get-LogLine 'hotkey registered'
        $errorLine = Get-LogLine 'error: '
        $exited = $p.HasExited
        $exitCode = if ($exited) { $p.ExitCode } else { $null }

        # the Y in "hotkey X parsed as Y" is the canonical hotkey
        $effective = $null
        $anchor = 'parsed as '
        $start = $parseLine.IndexOf($anchor)
        if ($start -ge 0) {
            $rest = $parseLine.Substring($start + $anchor.Length)
            $effective = ($rest -split '\(')[0].Trim()
        }

        $registered = $null
        if ($registerLine -match 'hotkey registered: (.+?) \(') { $registered = $Matches[1] }
        elseif ($registerLine -match 'hotkey registered: (.+)$') { $registered = $Matches[1] }

        # the parse result has to match the expectation, and a registration has to use the same key
        $ok = if ($case.expect -eq $null) {
            $errorLine.Length -gt 0
        } else {
            $effective -eq $case.expect -and
                ($registered -eq $null -or $registered -eq $case.expect)
        }

        if (-not $ok) { $failures++ }
        $mark = if ($ok) { 'OK  ' } else { 'FAIL' }
        $why = if ($errorLine.Length -gt 0) { ($errorLine -split 'error: ')[-1] } else { '' }
        $extra = if ($ok) { '' } else { " exited=$exited code=$exitCode" }
        $detail = "expected=$($case.expect) parsed=$effective registered=$registered $why$extra"
        Say "$mark $($case.arg.PadRight(16)) $detail"

        if (-not $p.HasExited) { $p.Kill() }
        $p.WaitForExit(2000) | Out-Null
        $p.Dispose()
    }
}
catch {
    $failures++
    Say "exception: $($_.Exception.Message)"
}
finally {
    Remove-Item $log -ErrorAction SilentlyContinue
    Say ''
    Say "$($cases.Count) cases, $failures failed"
    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($failures -eq 0) { exit 0 } else { exit 1 }
