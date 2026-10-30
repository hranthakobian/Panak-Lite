# scripts/generate_store_assets.ps1
# Generates high-quality Microsoft Store and Windows tile/splash visual assets from Panak icons.

param (
    [string]$SourceIcon = "$PSScriptRoot\..\Panak.ico",
    [string]$SourcePng = "$PSScriptRoot\..\src\Logo.png",
    [string]$OutputDir = "$PSScriptRoot\..\Package\Assets"
)

Add-Type -AssemblyName System.Drawing

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "Generating Microsoft Store & Windows Shell assets in: $OutputDir" -ForegroundColor Cyan

# Select best source image
$sourceImage = $null
if (Test-Path $SourcePng) {
    $sourceImage = [System.Drawing.Bitmap]::FromFile($SourcePng)
} elseif (Test-Path $SourceIcon) {
    $ico = New-Object System.Drawing.Icon($SourceIcon, 256, 256)
    $sourceImage = $ico.ToBitmap()
} else {
    Write-Error "No valid source image or icon found at $SourcePng or $SourceIcon"
    exit 1
}

function Create-ResizedImage {
    param (
        [System.Drawing.Image]$source,
        [int]$targetWidth,
        [int]$targetHeight,
        [string]$outputPath,
        [float]$paddingRatio = 0.0 # 0.0 means fill max bounding box, 0.15 adds breathing room
    )

    $bmp = New-Object System.Drawing.Bitmap($targetWidth, $targetHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Compute aspect ratio preserving fit
    $availWidth = $targetWidth * (1.0 - (2 * $paddingRatio))
    $availHeight = $targetHeight * (1.0 - (2 * $paddingRatio))

    $scale = [Math]::Min($availWidth / $source.Width, $availHeight / $source.Height)
    $drawW = [int]($source.Width * $scale)
    $drawH = [int]($source.Height * $scale)
    $posX = [int](($targetWidth - $drawW) / 2)
    $posY = [int](($targetHeight - $drawH) / 2)

    $g.DrawImage($source, $posX, $posY, $drawW, $drawH)
    $g.Dispose()

    $bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  -> $([System.IO.Path]::GetFileName($outputPath)) (${targetWidth}x${targetHeight})" -ForegroundColor Green
}

# 1. Store Logos
Create-ResizedImage $sourceImage 50 50 "$OutputDir\StoreLogo.png" 0.05
Create-ResizedImage $sourceImage 50 50 "$OutputDir\StoreLogo.scale-100.png" 0.05
Create-ResizedImage $sourceImage 63 63 "$OutputDir\StoreLogo.scale-125.png" 0.05
Create-ResizedImage $sourceImage 75 75 "$OutputDir\StoreLogo.scale-150.png" 0.05
Create-ResizedImage $sourceImage 100 100 "$OutputDir\StoreLogo.scale-200.png" 0.05
Create-ResizedImage $sourceImage 200 200 "$OutputDir\StoreLogo.scale-400.png" 0.05

# 2. Square44x44 (App List / Taskbar / Small icon)
Create-ResizedImage $sourceImage 44 44 "$OutputDir\Square44x44Logo.png" 0.08
Create-ResizedImage $sourceImage 44 44 "$OutputDir\Square44x44Logo.scale-100.png" 0.08
Create-ResizedImage $sourceImage 55 55 "$OutputDir\Square44x44Logo.scale-125.png" 0.08
Create-ResizedImage $sourceImage 66 66 "$OutputDir\Square44x44Logo.scale-150.png" 0.08
Create-ResizedImage $sourceImage 88 88 "$OutputDir\Square44x44Logo.scale-200.png" 0.08
Create-ResizedImage $sourceImage 176 176 "$OutputDir\Square44x44Logo.scale-400.png" 0.08

Create-ResizedImage $sourceImage 16 16 "$OutputDir\Square44x44Logo.targetsize-16.png" 0.0
Create-ResizedImage $sourceImage 24 24 "$OutputDir\Square44x44Logo.targetsize-24.png" 0.0
Create-ResizedImage $sourceImage 32 32 "$OutputDir\Square44x44Logo.targetsize-32.png" 0.0
Create-ResizedImage $sourceImage 48 48 "$OutputDir\Square44x44Logo.targetsize-48.png" 0.0
Create-ResizedImage $sourceImage 256 256 "$OutputDir\Square44x44Logo.targetsize-256.png" 0.0

Create-ResizedImage $sourceImage 16 16 "$OutputDir\Square44x44Logo.altform-unplated_targetsize-16.png" 0.0
Create-ResizedImage $sourceImage 24 24 "$OutputDir\Square44x44Logo.altform-unplated_targetsize-24.png" 0.0
Create-ResizedImage $sourceImage 32 32 "$OutputDir\Square44x44Logo.altform-unplated_targetsize-32.png" 0.0
Create-ResizedImage $sourceImage 48 48 "$OutputDir\Square44x44Logo.altform-unplated_targetsize-48.png" 0.0
Create-ResizedImage $sourceImage 256 256 "$OutputDir\Square44x44Logo.altform-unplated_targetsize-256.png" 0.0

# 3. Square71x71 (Small Tile)
Create-ResizedImage $sourceImage 71 71 "$OutputDir\Square71x71Logo.png" 0.08
Create-ResizedImage $sourceImage 71 71 "$OutputDir\Square71x71Logo.scale-100.png" 0.08
Create-ResizedImage $sourceImage 142 142 "$OutputDir\Square71x71Logo.scale-200.png" 0.08

# 4. Square150x150 (Medium Tile)
Create-ResizedImage $sourceImage 150 150 "$OutputDir\Square150x150Logo.png" 0.1
Create-ResizedImage $sourceImage 150 150 "$OutputDir\Square150x150Logo.scale-100.png" 0.1
Create-ResizedImage $sourceImage 188 188 "$OutputDir\Square150x150Logo.scale-125.png" 0.1
Create-ResizedImage $sourceImage 225 225 "$OutputDir\Square150x150Logo.scale-150.png" 0.1
Create-ResizedImage $sourceImage 300 300 "$OutputDir\Square150x150Logo.scale-200.png" 0.1
Create-ResizedImage $sourceImage 600 600 "$OutputDir\Square150x150Logo.scale-400.png" 0.1

# 5. Wide310x150 (Wide Tile)
Create-ResizedImage $sourceImage 310 150 "$OutputDir\Wide310x150Logo.png" 0.1
Create-ResizedImage $sourceImage 310 150 "$OutputDir\Wide310x150Logo.scale-100.png" 0.1
Create-ResizedImage $sourceImage 620 300 "$OutputDir\Wide310x150Logo.scale-200.png" 0.1

# 6. Square310x310 (Large Tile)
Create-ResizedImage $sourceImage 310 310 "$OutputDir\Square310x310Logo.png" 0.15
Create-ResizedImage $sourceImage 310 310 "$OutputDir\Square310x310Logo.scale-100.png" 0.15
Create-ResizedImage $sourceImage 620 620 "$OutputDir\Square310x310Logo.scale-200.png" 0.15

# 7. SplashScreen
Create-ResizedImage $sourceImage 620 300 "$OutputDir\SplashScreen.png" 0.15
Create-ResizedImage $sourceImage 620 300 "$OutputDir\SplashScreen.scale-100.png" 0.15
Create-ResizedImage $sourceImage 1240 600 "$OutputDir\SplashScreen.scale-200.png" 0.15

$sourceImage.Dispose()
Write-Host "All Microsoft Store & Windows assets generated successfully." -ForegroundColor Cyan
