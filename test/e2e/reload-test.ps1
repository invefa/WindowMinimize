# Checks live reload: while the app runs the config file is rewritten and the hotkey has to switch.
# The result goes to test\e2e\reload-test.txt (UTF-8), exit code 0 means it passed.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root 'dist\WindowMinimize.exe'
$log = Join-Path $PSScriptRoot 'hotkey.log'
$cfg = Join-Path $PSScriptRoot 'reload-test.ini'
$outFile = Join-Path $PSScriptRoot 'reload-test.txt'
$lines = [System.Collections.Generic.List[string]]::new()
$passed = $false
$app = $null

function Say($text) {
    $lines.Add($text)
    Write-Host $text
}

function Wait-Registered([string]$pattern, [int]$timeoutMs = 4000) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if (Test-Path $log) {
            $hit = Select-String -Path $log -Pattern $pattern -Encoding UTF8 | Select-Object -Last 1
            if ($hit) { return $hit.Line }
        }

        Start-Sleep -Milliseconds 150
    }

    return ''
}

try {
    if (-not (Test-Path $exe)) { throw "$exe not found, build it first" }

    Set-Content -Path $cfg -Value @(
        '[general]', 'Hotkey=Ctrl+Alt+M', 'Mode=minimize', 'Debug=true'
    ) -Encoding UTF8
    Remove-Item $log -ErrorAction SilentlyContinue

    $app = Start-Process -FilePath $exe -PassThru -ArgumentList @('--config', $cfg)
    Start-Sleep -Milliseconds 1500
    if ($app.HasExited) { throw "the app exited right after start, exit code $($app.ExitCode)" }

    $first = Wait-Registered 'hotkey registered'
    Say "registered at startup: $first"

    # rewrite the config and watch whether it switches to the new hotkey
    Set-Content -Path $cfg -Value @(
        '[general]', 'Hotkey=Ctrl+Shift+F10', 'Mode=minimize', 'Debug=true'
    ) -Encoding UTF8
    Start-Sleep -Milliseconds 1500

    $second = Wait-Registered 'hotkey registered: Ctrl\+Shift\+F10'
    Say "registered after the config was rewritten: $second"

    if ($first -match 'Ctrl\+Alt\+M' -and $second -match 'Ctrl\+Shift\+F10') {
        $passed = $true
        Say 'result: passed, the hotkey switched by itself after the config changed'
    } else {
        Say 'result: failed, no automatic switch of the hotkey was seen'
    }
}
catch {
    Say "exception: $($_.Exception.Message)"
}
finally {
    if ($app -and -not $app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    }

    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($passed) { exit 0 } else { exit 1 }
