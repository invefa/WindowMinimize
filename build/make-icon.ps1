param(
    [string]$Out = (Join-Path $PSScriptRoot '..\src\app.ico')
)

# 生成 app.ico。构图：两条向下的折线箭头收到底部一条横线上，示意“把窗口收起到任务栏”。
# 在 32 单位坐标系里绘制。同一套几何也写在 src\IconFactory.cs 的 Draw() 里（托盘图标要按
# 系统小图标尺寸现画才清晰），改这里请同步改那边。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$stroke = 3.0
$fg = [System.Drawing.Color]::FromArgb(255, 46, 124, 246)
$chevrons = @(
    @(10.5, 6.6, 16.0, 12.0, 21.5, 6.6),
    @(10.5, 13.4, 16.0, 18.8, 21.5, 13.4)
)
$bar = @(7.5, 24.6, 24.5, 24.6)

function New-Glyph([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $scale = $size / 32.0
    $pen = New-Object System.Drawing.Pen($fg, ([single]($stroke * $scale)))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    foreach ($c in $chevrons) {
        [System.Drawing.PointF[]]$pts = @(
            (New-Object System.Drawing.PointF(([single]($c[0] * $scale)), ([single]($c[1] * $scale)))),
            (New-Object System.Drawing.PointF(([single]($c[2] * $scale)), ([single]($c[3] * $scale)))),
            (New-Object System.Drawing.PointF(([single]($c[4] * $scale)), ([single]($c[5] * $scale))))
        )
        $g.DrawLines($pen, $pts)
    }

    $g.DrawLine(
        $pen,
        ([single]($bar[0] * $scale)), ([single]($bar[1] * $scale)),
        ([single]($bar[2] * $scale)), ([single]($bar[3] * $scale)))

    $pen.Dispose()
    $g.Dispose()
    return $bmp
}

$bitmaps = @()
foreach ($s in $sizes) { $bitmaps += (New-Glyph $s) }

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([UInt16]0)             # reserved
$writer.Write([UInt16]1)             # type: icon
$writer.Write([UInt16]$bitmaps.Count)

$offset = 6 + 16 * $bitmaps.Count
$pngBlobs = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $blob = $ms.ToArray()
    $pngBlobs += ,$blob
    $ms.Dispose()
}

for ($i = 0; $i -lt $bitmaps.Count; $i++) {
    $size = $sizes[$i]
    $blob = $pngBlobs[$i]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([Byte]$dim)
    $writer.Write([Byte]$dim)
    $writer.Write([Byte]0)     # 调色板数
    $writer.Write([Byte]0)     # reserved
    $writer.Write([UInt16]1)   # 色彩平面
    $writer.Write([UInt16]32)  # 位深
    $writer.Write([UInt32]$blob.Length)
    $writer.Write([UInt32]$offset)
    $offset += $blob.Length
}

foreach ($blob in $pngBlobs) { $writer.Write($blob) }

$writer.Flush()
$target = [System.IO.Path]::GetFullPath($Out)
[System.IO.File]::WriteAllBytes($target, $stream.ToArray())
$writer.Dispose(); $stream.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "ico written: $target ($((Get-Item $target).Length) bytes, $($sizes.Count) sizes)"
