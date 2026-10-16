# 逐条验证 --hotkey 写法的解析与注册结果。
# 结果写入 test\parse-test.txt（UTF-8），退出码 0 表示全部通过。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'dist\WindowMinimize.exe'
$cfg = Join-Path $PSScriptRoot 'parse-test.ini'
# 日志固定与配置文件同目录，文件名 hotkey.log
$log = Join-Path $PSScriptRoot 'hotkey.log'
$outFile = Join-Path $PSScriptRoot 'parse-test.txt'
$lines = [System.Collections.Generic.List[string]]::new()
$failures = 0

function Say($text) {
    $lines.Add($text)
    Write-Host $text
}

# 写法 -> 解析后的规范化热键文本，解析失败时为 $null
# 注意：本机上 Ctrl+Alt+M、Alt+Space 等组合已被别的程序注册，能顺带验证“被占用时改用键盘钩子”这条路径
$cases = @(
    @{ arg = 'Ctrl+Alt+M';     expect = 'Ctrl+Alt+M' },
    @{ arg = 'ctrl+alt+m';     expect = 'Ctrl+Alt+M' },
    @{ arg = 'Win+Alt+M';      expect = 'Alt+Win+M' },
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
    @{ arg = 'Ctrl+Alt+Win+M'; expect = 'Ctrl+Alt+Win+M' },
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
            $hit = Select-String -Path $log -Pattern $pattern -Encoding UTF8 | Select-Object -First 1
            if ($hit) { return $hit.Line }
        }
        Start-Sleep -Milliseconds 150
    }
    return ''
}

try {
    if (-not (Test-Path $exe)) { throw "找不到 $exe，请先构建" }

    foreach ($case in $cases) {
        Remove-Item $log -ErrorAction SilentlyContinue
        $p = Start-Process -FilePath $exe -PassThru -ArgumentList @(
            '--hotkey', $case.arg, '--config', $cfg, '--debug', '--no-tray')

        # 等待配置加载完成：启动后会写“WindowMinimize 启动...”与“解析 ... 得到 ...”
        $startLine = Get-LogLine 'WindowMinimize 启动'
        $parseLine = Get-LogLine '得到'
        $registerLine = Get-LogLine '热键已注册'
        $errorLine = Get-LogLine '错误'
        $exited = $p.HasExited
        $exitCode = if ($exited) { $p.ExitCode } else { $null }

        # 日志里“解析 “X” 得到 Y”的 Y 才是规范化后的热键
        $effective = $null
        $anchor = '得到 '
        $start = $parseLine.IndexOf($anchor)
        if ($start -ge 0) {
            $rest = $parseLine.Substring($start + $anchor.Length)
            $effective = ($rest -split '（')[0].Trim()
        }

        $registered = $null
        if ($registerLine -match '热键已注册：(.+?)（') { $registered = $Matches[1] }
        elseif ($registerLine -match '热键已注册：(.+)$') { $registered = $Matches[1] }

        # 解析结果必须与期望一致；若注册成功，注册的也必须是同一个热键
        $ok = if ($case.expect -eq $null) {
            $errorLine.Length -gt 0
        }
        else {
            $effective -eq $case.expect -and ($registered -eq $null -or $registered -eq $case.expect)
        }

        if (-not $ok) { $failures++ }
        $mark = if ($ok) { 'OK  ' } else { 'FAIL' }
        $why = if ($errorLine.Length -gt 0) { ($errorLine -split '错误: ')[-1] } else { '' }
        $extra = if ($ok) { '' } else { " 已退出=$exited 退出码=$exitCode" }
        Say "$mark $($case.arg.PadRight(16)) 期望解析=$($case.expect) 解析=$effective 已注册=$registered $why$extra"

        if (-not $p.HasExited) { $p.Kill() }
        $p.WaitForExit(2000) | Out-Null
    }
}
catch {
    $failures++
    Say "异常: $($_.Exception.Message)"
}
finally {
    Remove-Item $log -ErrorAction SilentlyContinue
    Say ''
    Say "合计 $($cases.Count) 条，失败 $failures 条"
    [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

if ($failures -eq 0) { exit 0 } else { exit 1 }
