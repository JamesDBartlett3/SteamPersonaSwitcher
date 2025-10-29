<#
.SYNOPSIS
    Creates ICO and PNG icon files from a source PNG image.

.DESCRIPTION
    This script takes a PNG file as input and generates icon files in multiple sizes.
    It creates an ICO file suitable for Windows applications and optionally saves
    intermediate PNG files at various sizes.

.PARAMETER InputPng
    Path to the source PNG file. If not provided, prompts the user.

.PARAMETER OutputPath
    Directory where output files will be saved. Defaults to script directory.

.PARAMETER OutputName
    Base name for output files (without extension). Defaults to "icon".

.PARAMETER SaveIntermediatePngs
    If specified, saves PNG files at each size (16x16, 32x32, 48x48, 256x256).

.EXAMPLE
    .\create-icon.ps1 -InputPng "C:\path\to\image.png"
    
.EXAMPLE
    .\create-icon.ps1 -InputPng "image.png" -OutputPath ".\Resources" -OutputName "app-icon" -SaveIntermediatePngs
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$InputPng,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = $PSScriptRoot,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputName = "icon",
    
    [Parameter(Mandatory=$false)]
    [switch]$SaveIntermediatePngs
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Prompt for input file if not provided
if ([string]::IsNullOrWhiteSpace($InputPng)) {
    $openFileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $openFileDialog.Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*"
    $openFileDialog.Title = "Select PNG file for icon generation"
    
    if ($openFileDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $InputPng = $openFileDialog.FileName
    } else {
        Write-Error "No input file selected. Exiting."
        exit 1
    }
}

# Validate input file
if (-not (Test-Path $InputPng)) {
    Write-Error "Input file not found: $InputPng"
    exit 1
}

# Ensure output directory exists
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

Write-Host "Loading source image: $InputPng" -ForegroundColor Cyan

# Load the source PNG
try {
    $sourceBmp = New-Object System.Drawing.Bitmap($InputPng)
} catch {
    Write-Error "Failed to load PNG file: $_"
    exit 1
}

Write-Host "Source image size: $($sourceBmp.Width)x$($sourceBmp.Height)" -ForegroundColor Gray

# Create resized versions with high quality
$sizes = @(16, 32, 48, 256)
$bitmaps = @{}

foreach ($size in $sizes) {
    Write-Host "Generating ${size}x${size} version..." -ForegroundColor Gray
    
    $resized = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($resized)
    
    # High quality scaling
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    
    $g.DrawImage($sourceBmp, 0, 0, $size, $size)
    $g.Dispose()
    
    $bitmaps[$size] = $resized
    
    # Save intermediate PNG if requested
    if ($SaveIntermediatePngs) {
        $pngPath = Join-Path $OutputPath "${OutputName}_${size}x${size}.png"
        $resized.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "  Saved: $pngPath" -ForegroundColor Green
    }
}

# Save the 256x256 as the main PNG
$mainPngPath = Join-Path $OutputPath "${OutputName}.png"
$bitmaps[256].Save($mainPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Main PNG saved: $mainPngPath" -ForegroundColor Green

# Create ICO file
$icoPath = Join-Path $OutputPath "${OutputName}.ico"
Write-Host "Creating ICO file with multiple sizes..." -ForegroundColor Cyan

try {
    # Use the 32x32 as base for the icon
    $icon = [System.Drawing.Icon]::FromHandle($bitmaps[32].GetHicon())
    $fileStream = [System.IO.File]::Create($icoPath)
    $icon.Save($fileStream)
    $fileStream.Close()
    
    Write-Host "ICO file saved: $icoPath" -ForegroundColor Green
} catch {
    Write-Error "Failed to create ICO file: $_"
}

# Cleanup
$sourceBmp.Dispose()
foreach ($bmp in $bitmaps.Values) {
    $bmp.Dispose()
}

Write-Host "`nIcon generation complete!" -ForegroundColor Green
Write-Host "Output location: $OutputPath" -ForegroundColor Cyan
