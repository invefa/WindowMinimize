# Checks whether this app can really take Win+S over, using a log directory of its own so the other
# tests cannot overwrite it.
# The result goes to test\e2e\wins-test.txt (UTF-8), exit code 0 means the registration worked.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root 'dist\WindowMinimize.exe'
$dir = Join-Path $PSScriptRoot 'wins'
$cfg = Join-Path $dir 'wins.ini'
$log = Join-Path $dir 'hotkey.log'
$outFile = Join-Path $PSScriptRoot 'wins-test.txt'
$lines = [System.Collections.Generic.List[string]]::new()
$ok = $false
$app = $null

function Say($text) {
    $lines.Add($text)
    Write-Host $text
}

try {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Set-Content -Path $cfg -Value @(
        '[general]', 'Hotkey=Win+S', 'Mode=minimize', 'Debug=true'
    ) -Encoding UTF8
    Remove-Item $log -ErrorAction SilentlyContinue

    $app = Start-Process -FilePath $exe -PassThru -ArgumentList @('--config', $cfg)
    Start-Sleep -Seconds 2
    if ($app.HasExited) { throw "the app exited right after start, exit code $($app.ExitCode)" }

    $text = if (Test-Path $log) { Get-Content $log -Encoding UTF8 -Raw } else { '' }
    Say '--- startup log ---'
    foreach ($line in ($text -split "`r?`n" | Where-Object { $_ -ne '' })) { Say $line }

    $registered = $text -match 'hotkey registered: Win\+S'
    $viaHook = $text -match 'hotkey registered: Win\+S \(low level keyboard hook'
    $ok = $registered

    Say ''
    if ($viaHook) {
        $how = 'low level keyboard hook (the system search key was taken over)'
    } else {
        $how = 'RegisterHotKey'
    }

    Say "registered = $registered; way = $how"
    if ($ok) {
        Say 'result: passed, Win+S can be used'
    } else {
        Say 'result: failed, Win+S was not registered'
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

if ($ok) { exit 0 } else { exit 1 }
