# 实测 Win+S 能否被本程序接管：独立日志目录，避免与其他测试互相覆盖。
# 结果写入 test\wins-test.txt（UTF-8），退出码 0 表示注册成功。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
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
        '[general]', 'Hotkey=Win+S', 'Mode=minimize', 'AutoStart=false', 'Debug=true'
    ) -Encoding UTF8
    Remove-Item $log -ErrorAction SilentlyContinue

    $app = Start-Process -FilePath $exe -PassThru -ArgumentList @('--config', $cfg)
    Start-Sleep -Seconds 2
    if ($app.HasExited) { throw "程序启动后立即退出，退出码 $($app.ExitCode)" }

    $text = if (Test-Path $log) { Get-Content $log -Encoding UTF8 -Raw } else { '' }
    Say "--- 启动日志 ---"
    foreach ($line in ($text -split "`r?`n" | Where-Object { $_ -ne '' })) { Say $line }

    $registered = $text -match '热键已注册：Win\+S'
    $viaHook = $text -match '热键已注册：Win\+S（低级键盘钩子'
    $ok = $registered

    Say ''
    Say "注册成功 = $registered；接管方式 = $(if ($viaHook) { '低级键盘钩子（系统搜索键被抢下）' } else { 'RegisterHotKey' })"
    if ($ok) { Say '结果: 通过，Win+S 可用' } else { Say '结果: 未通过，Win+S 没注册上' }
}
catch {
    Say "异常: $($_.Exception.Message)"
}
finally {
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($ok) { exit 0 } else { exit 1 }
