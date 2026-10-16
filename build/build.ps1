# 构建 WindowMinimize：生成 dist\WindowMinimize.exe（自包含单文件，不依赖本机 .NET）
#
# 用法: pwsh -NoProfile -File build\build.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\WindowMinimize.csproj'
$dist = Join-Path $root 'dist'

Write-Host "项目: $project"
Write-Host "输出: $dist"

dotnet publish $project -c Release -r win-x64 -o $dist --nologo
if ($LASTEXITCODE -ne 0) {
    throw "构建失败，退出码 $LASTEXITCODE"
}

$exe = Join-Path $dist 'WindowMinimize.exe'
if (-not (Test-Path $exe)) {
    throw "构建结束但没有生成 $exe"
}

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "完成: $exe ($size MB)"
