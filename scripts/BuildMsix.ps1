# scripts/BuildMsix.ps1
# Automates the build and creation of an MSIX package for Microsoft Store and local testing.

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Platform = "win-x64",
    [string]$OutputDir = "",
    [switch]$SignForLocalTest,
    [string]$CertificatePath = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "       Panak MSIX Package Builder for Store        " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

# 1. Resolve Project Root and Configuration
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Location).Path
}
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "publish"
} else {
    $OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
}
$csprojPath = "$repoRoot\Panak.csproj"
$configPath = "$repoRoot\Package\MsixConfig.json"
$manifestTemplatePath = "$repoRoot\Package\AppxManifest.xml"
$assetsDir = "$repoRoot\Package\Assets"
$stagingDir = "$repoRoot\staging_msix"

if (!(Test-Path $csprojPath)) {
    Write-Error "Panak.csproj not found at $csprojPath"
    exit 1
}

# 2. Extract Version from Panak.csproj
[xml]$csprojXml = Get-Content -Path $csprojPath -Raw
$rawVersion = $csprojXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($rawVersion)) {
    $rawVersion = "2.0.5"
}
# Convert to 4-part quad version required by MSIX (e.g., 2.0.5.0)
$verParts = $rawVersion.Split('.')
while ($verParts.Count -lt 4) {
    $verParts += "0"
}
$quadVersion = ($verParts[0..3] -join '.')
Write-Host "[1/6] Target Version: $rawVersion (MSIX Quad: $quadVersion)" -ForegroundColor Green

# 3. Read MsixConfig.json
if (!(Test-Path $configPath)) {
    Write-Error "MsixConfig.json not found at $configPath"
    exit 1
}
$config = Get-Content -Path $configPath -Raw | ConvertFrom-Json

$identityName = if ($config.Identity.Name) { $config.Identity.Name } else { "HrantHakobyan.Panak" }
$publisher = if ($config.Identity.Publisher) { $config.Identity.Publisher } else { "CN=YOUR-PUBLISHER-ID-FROM-PARTNER-CENTER" }
$publisherDisplayName = if ($config.Identity.PublisherDisplayName) { $config.Identity.PublisherDisplayName } else { "Hrant Hakobyan" }
$appDisplayName = if ($config.Application.DisplayName) { $config.Application.DisplayName } else { "Panak" }
$appDescription = if ($config.Application.Description) { $config.Application.Description } else { "Panak - Android-Style Popup Folders and Widgets for Windows" }
$tileShortName = if ($config.Application.TileShortName) { $config.Application.TileShortName } else { "Panak" }

Write-Host "[2/6] Store Identity:" -ForegroundColor Green
Write-Host "      Package Identity Name : $identityName"
Write-Host "      Publisher ID          : $publisher"
Write-Host "      Publisher Display Name: $publisherDisplayName"
Write-Host "      Display Name          : $appDisplayName"

# 4. Ensure Store Visual Assets Exist
if (!(Test-Path "$assetsDir\StoreLogo.png") -or !(Test-Path "$assetsDir\Square150x150Logo.png")) {
    Write-Host "Assets not found. Generating visual assets..." -ForegroundColor Yellow
    & "$PSScriptRoot\generate_store_assets.ps1"
}

# 5. Locate MakeAppx.exe & SignTool.exe
Write-Host "[3/6] Locating Windows SDK packaging tools..." -ForegroundColor Green
$makeappx = $null
$signtool = $null

$candidates = @()
$nugetSdk = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*\x64\makeappx.exe" }
if ($nugetSdk) {
    $candidates += ($nugetSdk | Select-Object -ExpandProperty FullName)
}
if (Test-Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin") {
    $kitsTools = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*\x64\makeappx.exe" }
    if ($kitsTools) {
        $candidates += ($kitsTools | Select-Object -ExpandProperty FullName)
    }
}
$cmdTool = Get-Command makeappx.exe -ErrorAction SilentlyContinue
if ($cmdTool) {
    $candidates += $cmdTool.Source
}

if ($candidates.Count -gt 0) {
    $makeappx = $candidates[0]
    $signtool = Join-Path (Split-Path $makeappx) "signtool.exe"
} else {
    Write-Host "MakeAppx.exe not found. Restoring Microsoft.Windows.SDK.BuildTools..." -ForegroundColor Yellow
    $tempToolDir = "$env:TEMP\panak_tool_restore"
    New-Item -ItemType Directory -Path $tempToolDir -Force | Out-Null
    dotnet new classlib -o $tempToolDir --force | Out-Null
    dotnet add $tempToolDir package Microsoft.Windows.SDK.BuildTools --version 10.0.26100.1742 | Out-Null
    Remove-Item -Recurse -Force $tempToolDir -ErrorAction SilentlyContinue

    $nugetSdk = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*\x64\makeappx.exe" }
    if ($nugetSdk) {
        $makeappx = ($nugetSdk | Select-Object -ExpandProperty FullName)[0]
        $signtool = Join-Path (Split-Path $makeappx) "signtool.exe"
    }
}

if (!$makeappx -or !(Test-Path $makeappx)) {
    Write-Error "MakeAppx.exe could not be located. Please ensure Microsoft.Windows.SDK.BuildTools is restored."
    exit 1
}
Write-Host "      Using MakeAppx: $makeappx" -ForegroundColor Gray

# 6. Build and Publish Application Payload
Write-Host "[4/6] Publishing Panak application and components..." -ForegroundColor Green
if (Test-Path $stagingDir) {
    Remove-Item -Recurse -Force $stagingDir
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# Stop running instances to avoid file locks
Get-Process -Name "Panak" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "      - Publishing Panak (Self-Contained win-x64)..."
dotnet publish -c $Configuration -r $Platform --self-contained true -o $stagingDir "$csprojPath"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish Panak.csproj"
    exit $LASTEXITCODE
}

Write-Host "      - Publishing Shell Extension..."
dotnet publish -c $Configuration -r $Platform --self-contained false -o $stagingDir "$repoRoot\Panak.ShellExt\Panak.ShellExt.csproj"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish Panak.ShellExt.csproj"
    exit $LASTEXITCODE
}

# 7. Stage Assets and Generate AppxManifest.xml
Write-Host "[5/6] Preparing package layout and AppxManifest.xml..." -ForegroundColor Green
$stagingAssets = "$stagingDir\Assets"
New-Item -ItemType Directory -Path $stagingAssets -Force | Out-Null
Copy-Item -Path "$assetsDir\*" -Destination $stagingAssets -Recurse -Force

# Read manifest template and substitute configured values
[xml]$manifest = Get-Content -Path $manifestTemplatePath -Raw
$manifest.Package.Identity.Name = $identityName
$manifest.Package.Identity.Publisher = $publisher
$manifest.Package.Identity.Version = $quadVersion
$manifest.Package.Identity.ProcessorArchitecture = "x64"

$manifest.Package.Properties.DisplayName = $appDisplayName
$manifest.Package.Properties.PublisherDisplayName = $publisherDisplayName
$manifest.Package.Properties.Description = $appDescription

$appNode = $manifest.Package.Applications.Application
$appNode.VisualElements.DisplayName = $appDisplayName
$appNode.VisualElements.Description = $appDescription
$appNode.VisualElements.DefaultTile.ShortName = $tileShortName

$finalManifestPath = "$stagingDir\AppxManifest.xml"
$manifest.Save($finalManifestPath)

# 8. Create MSIX Package
Write-Host "[6/6] Packing MSIX package..." -ForegroundColor Green
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$msixFileName = "Panak-v$rawVersion.msix"
$msixPath = "$OutputDir\$msixFileName"
if (Test-Path $msixPath) {
    Remove-Item -Force $msixPath
}

& $makeappx pack /v /h SHA256 /d $stagingDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    Write-Error "MakeAppx packing failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 9. Optional Local Test Signing
if ($SignForLocalTest) {
    Write-Host "Signing package for local testing..." -ForegroundColor Yellow
    $cert = $null

    if ($CertificatePath -and (Test-Path $CertificatePath)) {
        Write-Host "Using provided certificate: $CertificatePath"
        $signArgs = @("sign", "/fd", "SHA256", "/f", $CertificatePath)
        if ($CertificatePassword) {
            $signArgs += @("/p", $CertificatePassword)
        }
        $signArgs += $msixPath
        & $signtool @signArgs
    } else {
        # Check or create self-signed test certificate matching the manifest publisher
        Write-Host "Creating/Using self-signed development certificate for $publisher..."
        $existingCert = Get-ChildItem -Path "Cert:\CurrentUser\My" -ErrorAction SilentlyContinue | Where-Object { $_.Subject -eq $publisher }
        if (!$existingCert) {
            $existingCert = New-SelfSignedCertificate -Type Custom `
                -Subject $publisher `
                -KeyUsage DigitalSignature `
                -FriendlyName "Panak Development Test Certificate" `
                -CertStoreLocation "Cert:\CurrentUser\My" `
                -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
            Write-Host "Generated new test certificate in Cert:\CurrentUser\My: $($existingCert.Thumbprint)" -ForegroundColor Green
        }
        $certThumb = ($existingCert | Select-Object -First 1).Thumbprint
        & $signtool sign /fd SHA256 /sha1 $certThumb $msixPath
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Package signed successfully for local testing." -ForegroundColor Green
        Write-Host "To install locally, ensure the certificate is trusted in 'Local Computer > Trusted People'." -ForegroundColor Yellow
    } else {
        Write-Warning "Signing failed. For Microsoft Store submission, signing is NOT required (Microsoft signs it automatically)."
    }
}

# 10. Clean Staging Directory
Remove-Item -Recurse -Force $stagingDir -ErrorAction SilentlyContinue

$fileInfo = Get-Item $msixPath
$sizeMb = [Math]::Round($fileInfo.Length / 1MB, 2)

Write-Host "===================================================" -ForegroundColor Green
Write-Host "  SUCCESS! MSIX Package ready for Microsoft Store: " -ForegroundColor Green
Write-Host "  Path: $msixPath" -ForegroundColor White
Write-Host "  Size: $sizeMb MB" -ForegroundColor White
Write-Host "===================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps for Microsoft Store Submission:" -ForegroundColor Cyan
Write-Host "1. In Microsoft Partner Center (partner.microsoft.com), go to your app -> Product Identity."
Write-Host "2. Copy the exact Package/Identity/Name, Publisher, and PublisherDisplayName into Package\MsixConfig.json."
Write-Host "3. Re-run this script to produce the final Store package."
Write-Host "4. Upload '$msixFileName' to Partner Center in the 'Packages' step."
Write-Host ""
