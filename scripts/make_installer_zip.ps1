$baseDir = Split-Path -Parent $PSScriptRoot
if (-not $baseDir -or -not (Test-Path (Join-Path $baseDir "Panak.csproj"))) {
    $baseDir = "c:\Users\hrant\Documents\GitHub\Panak Lite"
}
$staging = Join-Path $baseDir "staging_zip"
$zipPath = Join-Path $baseDir "Panak-Installer-v1.0.0.zip"
$publishZip = Join-Path $baseDir "publish\Panak-Installer-v1.0.0.zip"

if (Test-Path $staging) {
    Remove-Item -Recurse -Force $staging
}
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item (Join-Path $baseDir "publish\Setup.exe") -Destination $staging

$readmeContent = @"
==================================================
                 PANAK LITE v1.0.0
       Material You Popup Folders for Windows
==================================================

[Տեղադրում / Installation]
1. Գործարկեք ``Setup.exe``-ն (Run Setup.exe)
2. Ընտրեք Ձեր նախընտրած կարգավորումները (Auto-start, Shortcuts)
3. Սեղմեք «Տեղադրել» (Install)

[Հիմնական հնարավորություններ / Features]
• Ժամանակակից Android / Material You ոճի Popup թղթապանակներ Windows-ում
• Space Quick Look – պանակների ակնթարթային նախադիտում Windows Explorer-ում
• Dynamic 2x2 preview icon engine
• Fluent Acrylic ապակեպատ սահուն անիմացիաներ
• Windows-ի մեկնարկի ավտոմատ գործարկում (Auto-Start)
==================================================
"@

Set-Content -Path (Join-Path $staging "README.txt") -Value $readmeContent -Encoding UTF8

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path "$staging\*" -DestinationPath $zipPath -Force

Copy-Item $zipPath -Destination $publishZip -Force

Remove-Item -Recurse -Force $staging

Write-Host "Success! Created:" $zipPath "Size:" (Get-Item $zipPath).Length "bytes"
