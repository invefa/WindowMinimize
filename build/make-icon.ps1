param(
    [string]$Out = (Join-Path $PSScriptRoot '..\src\app.ico')
)

# 生成 app.ico。构图：向右收拢的三条横线（示意最小化），在 32 单位坐标系里绘制。
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$lines = @(
    @(6.0, 22.0, 26.0, 22.0),
    @(10.5, 16.5, 21.5, 16.5),
    @(14.5, 11.0, 17.5, 11.0)
)
$penWidth = 2.6
$fg = [System.Drawing.Color]::FromArgb(255, 32, 36, 42)

function New-Glyph([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $scale = $size / 32.0
    $pen = New-Object System.Drawing.Pen($fg, ([single]($penWidth * $scale)))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    foreach ($l in $lines) {
        $x1 = [single]($l[0] * $scale); $y1 = [single]($l[1] * $scale)
        $x2 = [single]($l[2] * $scale); $y2 = [single]($l[3] * $scale)
        $g.DrawLine($pen, $x1, $y1, $x2, $y2)
    }
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
