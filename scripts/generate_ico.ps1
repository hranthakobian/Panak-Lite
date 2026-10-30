[Reflection.Assembly]::LoadWithPartialName('System.Drawing') | Out-Null

function Convert-PngToIco($pngPath, $icoPath) {
    if (-not (Test-Path $pngPath)) {
        Write-Warning "Source file not found: $pngPath"
        return
    }

    $src = [System.Drawing.Image]::FromFile($pngPath)
    $sizes = @(16, 24, 32, 48, 64, 128, 256)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    # Header
    $bw.Write([uint16]0) # Reserved
    $bw.Write([uint16]1) # Type (1=Icon)
    $bw.Write([uint16]$sizes.Count) # Image count

    $pngBytesList = New-Object System.Collections.Generic.List[byte[]]

    foreach ($sz in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($src, 0, 0, $sz, $sz)
        $g.Dispose()

        $imgMs = New-Object System.IO.MemoryStream
        $bmp.Save($imgMs, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $bytes = $imgMs.ToArray()
        $imgMs.Dispose()
        $pngBytesList.Add($bytes)
    }
    $src.Dispose()

    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz = $sizes[$i]
        $bytes = $pngBytesList[$i]

        $w = if ($sz -ge 256) { 0 } else { $sz }
        $h = if ($sz -ge 256) { 0 } else { $sz }

        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0) # Color count
        $bw.Write([byte]0) # Reserved
        $bw.Write([uint16]1) # Color planes
        $bw.Write([uint16]32) # Bit count
        $bw.Write([uint32]$bytes.Length) # Image bytes
        $bw.Write([uint32]$offset) # Offset
        $offset += $bytes.Length
    }

    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $bw.Write($pngBytesList[$i])
    }

    $allBytes = $ms.ToArray()
    [System.IO.File]::WriteAllBytes($icoPath, $allBytes)
    $bw.Dispose()
    $ms.Dispose()

    Write-Host "Generated $icoPath (size: $((Get-Item $icoPath).Length) bytes) from $pngPath"
}

$baseDir = Split-Path -Parent $PSScriptRoot
if (-not $baseDir -or -not (Test-Path (Join-Path $baseDir "Panak.csproj"))) {
    $baseDir = "c:\Users\hrant\Documents\GitHub\Panak"
}

$iconSource = Join-Path $baseDir 'src\Logo.png'
if (-not (Test-Path $iconSource)) {
    $iconSource = Join-Path $baseDir 'Logo.png'
}

$setupSource = Join-Path $baseDir 'src\Setup.png'
if (-not (Test-Path $setupSource)) {
    $setupSource = Join-Path $baseDir 'Setup.png'
}

# 1. Main Application icon (Panak.ico from src\Logo.png)
Convert-PngToIco $iconSource (Join-Path $baseDir 'Panak.ico')

# 2. Setup Installer icon (Setup\Panak.ico from src\Setup.png)
Convert-PngToIco $setupSource (Join-Path $baseDir 'Setup\Panak.ico')

# 3. Uninstaller icon (Uninstall\Uninstall.ico from src\Uninstaller.png)
$uninstallerSource = Join-Path $baseDir 'src\Uninstaller.png'
if (Test-Path $uninstallerSource) {
    Convert-PngToIco $uninstallerSource (Join-Path $baseDir 'Uninstall\Uninstall.ico')
    Convert-PngToIco $uninstallerSource (Join-Path $baseDir 'Uninstall.ico')
}


