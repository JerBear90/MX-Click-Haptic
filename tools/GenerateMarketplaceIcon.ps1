$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$iconPath = Join-Path $PSScriptRoot '..\MxClickHaptics.LogitechPlugin\package\metadata\Icon256x256.png'
$bitmap = [System.Drawing.Bitmap]::new(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(17, 24, 39))

$background = [System.Drawing.Drawing2D.GraphicsPath]::new()
$background.AddArc(12, 12, 48, 48, 180, 90)
$background.AddArc(196, 12, 48, 48, 270, 90)
$background.AddArc(196, 196, 48, 48, 0, 90)
$background.AddArc(12, 196, 48, 48, 90, 90)
$background.CloseFigure()
$backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(31, 41, 55))
$graphics.FillPath($backgroundBrush, $background)

$ringPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(45, 196, 184), 8)
$ringPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$ringPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($ringPen, 45, 83, 62, 70, 125, 110)
$graphics.DrawArc($ringPen, 149, 83, 62, 70, 305, 110)

$mouseBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(243, 244, 246))
$mousePath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$mousePath.AddArc(79, 45, 98, 98, 180, 180)
$mousePath.AddLine(177, 94, 177, 163)
$mousePath.AddArc(79, 116, 98, 98, 0, 180)
$mousePath.CloseFigure()
$graphics.FillPath($mouseBrush, $mousePath)

$dividerPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(17, 24, 39), 7)
$dividerPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$dividerPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($dividerPen, 128, 55, 128, 101)

$clickBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(45, 196, 184))
$graphics.FillEllipse($clickBrush, 119, 68, 18, 18)
$font = [System.Drawing.Font]::new('Segoe UI', 19, [System.Drawing.FontStyle]::Bold)
$textFormat = [System.Drawing.StringFormat]::new()
$textFormat.Alignment = [System.Drawing.StringAlignment]::Center
$textFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString(
    'MX',
    $font,
    $mouseBrush,
    [System.Drawing.RectangleF]::new(14, 216, 228, 28),
    $textFormat
)

$metadata = Split-Path -Parent $iconPath
New-Item -ItemType Directory -Force -Path $metadata | Out-Null
$bitmap.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)

$clickBrush.Dispose()
$textFormat.Dispose()
$font.Dispose()
$dividerPen.Dispose()
$mousePath.Dispose()
$mouseBrush.Dispose()
$ringPen.Dispose()
$backgroundBrush.Dispose()
$background.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Output "Created $iconPath"
