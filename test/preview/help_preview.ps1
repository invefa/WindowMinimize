# HelpBadge preview. Run after building the project:
# pwsh -NoProfile -File test\preview\help_preview.ps1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$dllCandidates = @(
    (Join-Path $PSScriptRoot '..\..\src\bin\Release\net8.0-windows\win-x64\WindowMinimize.dll'),
    (Join-Path $PSScriptRoot '..\..\dist\WindowMinimize.dll')
)
$dll = $dllCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $dll) {
    throw 'Build the project first: dotnet build src\WindowMinimize.csproj -c Release'
}

$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $dll))
$badgeType = $assembly.GetType('WindowMinimize.HelpBadge')
$form = New-Object System.Windows.Forms.Form
$form.Text = 'HelpBadge preview'
$form.Width = 360
$form.Height = 180
$form.StartPosition = 'CenterScreen'

$toolTip = New-Object System.Windows.Forms.ToolTip
$toolTip.AutoPopDelay = 15000
$toolTip.InitialDelay = 250
$toolTip.ShowAlways = $true

$rows = @(
    @('Hotkey', 'Global hotkey that minimizes the current foreground window.'),
    @('Screen pair', 'The two screens used by the screen-switch hotkey.'),
    @('Updates', 'Check GitHub releases and install a newer build.')
)

for ($i = 0; $i -lt $rows.Count; $i++) {
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $rows[$i][0]
    $label.AutoSize = $true
    $label.Location = New-Object System.Drawing.Point(18, 20 + ($i * 42))
    $form.Controls.Add($label)

    $badge = [Activator]::CreateInstance($badgeType, $true)
    $badge.Location = New-Object System.Drawing.Point(120, 16 + ($i * 42))
    $toolTip.SetToolTip($badge, $rows[$i][1])
    $form.Controls.Add($badge)
}

[void]$form.ShowDialog()
