# Build WindowMinimize.
#
# Plain this produces dist\WindowMinimize.exe, a self-contained single file that needs nothing but
# Windows. With -Lite it produces dist\WindowMinimize-lite.exe instead, a single file of about
# 200 KB that uses the .NET 8 Desktop Runtime of the machine it runs on.
#
# Usage: pwsh -NoProfile -File build\build.ps1 [-Lite]

param(
    [switch] $Lite
)

$ErrorActionPreference = 'Stop'
$root                  = Split-Path -Parent $PSScriptRoot
$project               = Join-Path $root 'src\WindowMinimize.csproj'
$dist                  = Join-Path $root 'dist'
$name                  = if ($Lite) { 'WindowMinimize-lite' } else { 'WindowMinimize' }
$exe                   = Join-Path $dist "$name.exe"
# a framework dependent single file cannot be compressed, but it is small either way
$extra                 = if ($Lite) {
    @('--self-contained', 'false', "-p:AssemblyName=$name",
      '-p:EnableCompressionInSingleFile=false')
} else { @() }

Write-Host "project: $project"
Write-Host "output:  $exe"

dotnet publish $project -c Release -r win-x64 -o $dist @extra --nologo
if ($LASTEXITCODE -ne 0) { throw "publish failed with exit code $LASTEXITCODE" }

if (-not (Test-Path $exe)) { throw "publish finished but $exe was not produced" }

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "done: $exe ($size MB)"
