# Build WindowMinimize: produce dist\WindowMinimize.exe (self-contained single file, no local .NET)
#
# Usage: pwsh -NoProfile -File build\build.ps1

$ErrorActionPreference = 'Stop'
$root                  = Split-Path -Parent $PSScriptRoot
$project               = Join-Path $root 'src\WindowMinimize.csproj'
$dist                  = Join-Path $root 'dist'

Write-Host "project: $project"
Write-Host "output: $dist"

dotnet publish $project -c Release -r win-x64 -o $dist --nologo
if ($LASTEXITCODE -ne 0) { throw "publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $dist 'WindowMinimize.exe'
if (-not (Test-Path $exe)) { throw "publish finished but $exe was not produced" }

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "done: $exe ($size MB)"
