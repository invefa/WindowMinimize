# 验证热加载：程序运行中改写配置文件，热键是否自动切换。
# 结果写入 test\reload-test.txt（UTF-8），退出码 0 表示通过。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
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
    if (-not (Test-Path $exe)) { throw "找不到 $exe，请先构建" }

    Set-Content -Path $cfg -Value @(
        '[general]', 'Hotkey=Ctrl+Alt+M', 'Mode=minimize', 'MinimizeToTray=false', 'Debug=true'
    ) -Encoding UTF8
    Remove-Item $log -ErrorAction SilentlyContinue

    $app = Start-Process -FilePath $exe -PassThru -ArgumentList @('--config', $cfg)
    Start-Sleep -Milliseconds 1500
    if ($app.HasExited) { throw "程序启动后立即退出，退出码 $($app.ExitCode)" }

    $first = Wait-Registered '热键已注册'
    Say "启动后注册：$first"

    # 改写配置，观察是否自动切换到新热键
    Set-Content -Path $cfg -Value @(
        '[general]', 'Hotkey=Ctrl+Shift+F10', 'Mode=minimize', 'MinimizeToTray=false', 'Debug=true'
    ) -Encoding UTF8
    Start-Sleep -Milliseconds 1500

    $second = Wait-Registered '热键已注册：Ctrl\+Shift\+F10'
    Say "改写配置后注册：$second"

    if ($first -match 'Ctrl\+Alt\+M' -and $second -match 'Ctrl\+Shift\+F10') {
        $passed = $true
        Say '结果: 通过，改动配置文件后热键自动切换'
    }
    else {
        Say '结果: 未通过，没有观察到热键自动切换'
    }
}
catch {
    Say "异常: $($_.Exception.Message)"
}
finally {
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($passed) { exit 0 } else { exit 1 }
