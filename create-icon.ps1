Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Create bitmap
$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Fill background with blue
$g.Clear([System.Drawing.Color]::FromArgb(30, 136, 229))

# Draw text
$font = New-Object System.Drawing.Font("Segoe UI", 70, [System.Drawing.FontStyle]::Bold)
$text = "SPS"
$textSize = $g.MeasureString($text, $font)
$x = ($size - $textSize.Width) / 2
$y = ($size - $textSize.Height) / 2
$g.DrawString($text, $font, [System.Drawing.Brushes]::White, $x, $y)

# Save as PNG first (easier than ICO)
$pngPath = "$PSScriptRoot\icon.png"
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Create smaller versions for ICO
$bmp16 = New-Object System.Drawing.Bitmap($bmp, 16, 16)
$bmp32 = New-Object System.Drawing.Bitmap($bmp, 32, 32)
$bmp48 = New-Object System.Drawing.Bitmap($bmp, 48, 48)

# Convert to ICO using Icon class
$icoPath = "$PSScriptRoot\icon.ico"
$icon = [System.Drawing.Icon]::FromHandle($bmp32.GetHicon())
$fileStream = [System.IO.File]::Create($icoPath)
$icon.Save($fileStream)
$fileStream.Close()

# Cleanup
$g.Dispose()
$font.Dispose()
$bmp.Dispose()
$bmp16.Dispose()
$bmp32.Dispose()
$bmp48.Dispose()

Write-Host "Icon created successfully at $icoPath"
