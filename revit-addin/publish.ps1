# Build BimDown installer with embedded bundle
# Usage: powershell -ExecutionPolicy Bypass -File publish.ps1
# Output: publish/BimDownInstaller.exe (single file, contains everything)

param(
    [string[]]$RevitVersions = @("2025", "2026"),
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$projectFile = Join-Path $scriptDir "BimDown.RevitAddin.csproj"
$publishDir = Join-Path $scriptDir "publish"
$bundleDir = Join-Path $publishDir "BimDown.bundle"
$installerDir = Join-Path $scriptDir "Installer"

# Clean previous output
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

# Build each Revit version
foreach ($version in $RevitVersions) {
    Write-Host "Building for Revit $version..." -ForegroundColor Cyan

    # Clean obj to avoid cache conflicts between Revit versions
    $objDir = Join-Path $scriptDir "obj"
    if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }
    $binDir = Join-Path $scriptDir "bin"
    if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }

    dotnet build $projectFile -c $Configuration -p:RevitVersion=$version

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for Revit $version"
        exit 1
    }

    # Copy build output to bundle
    $buildOutput = Join-Path (Join-Path (Join-Path $scriptDir "bin") $Configuration) "net8.0-windows"
    $contentsDir = Join-Path (Join-Path $bundleDir "Contents") $version
    New-Item -ItemType Directory -Path $contentsDir -Force | Out-Null
    Copy-Item "$buildOutput\*" $contentsDir -Recurse -Force

    # Write .addin manifest
    @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BimDown</Name>
    <Assembly>BimDown.RevitAddin.dll</Assembly>
    <FullClassName>BimDown.RevitAddin.BimDownApp</FullClassName>
    <ClientId>b01d0000-ba7c-4e0f-a55e-7ab123456780</ClientId>
    <VendorId>BIMX</VendorId>
    <VendorDescription>BimDown</VendorDescription>
  </AddIn>
</RevitAddIns>
"@ | Out-File -FilePath (Join-Path $contentsDir "BimDown.addin") -Encoding utf8

    Write-Host "  -> $contentsDir" -ForegroundColor Green
}

# Copy PackageContents.xml to bundle root
Copy-Item (Join-Path $scriptDir "PackageContents.xml") $bundleDir

Write-Host "`nBundle created at: $bundleDir" -ForegroundColor Green

# Zip bundle and place in Installer project as embedded resource
Write-Host "`nPackaging bundle into installer..." -ForegroundColor Cyan
$zipPath = Join-Path $installerDir "bundle.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath)
Write-Host "  Bundle zipped: $zipPath"

# Build installer with embedded bundle
Write-Host "Building installer..." -ForegroundColor Cyan
$installerProject = Join-Path $installerDir "BimDown.Installer.csproj"

# Clean installer build artifacts to force re-embed
$installerObj = Join-Path $installerDir "obj"
$installerBin = Join-Path $installerDir "bin"
if (Test-Path $installerObj) { Remove-Item $installerObj -Recurse -Force }
if (Test-Path $installerBin) { Remove-Item $installerBin -Recurse -Force }

dotnet publish $installerProject -c $Configuration -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Write-Error "Installer build failed"
    exit 1
}

# Clean up: remove zip and bundle dir, keep only the exe
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item $bundleDir -Recurse -Force -ErrorAction SilentlyContinue
# Remove publish artifacts from dotnet publish (pdb etc)
Get-ChildItem $publishDir -File | Where-Object { $_.Name -ne "BimDownInstaller.exe" } | Remove-Item -Force

Write-Host "`nDone! Distribute this single file:" -ForegroundColor Green
$exePath = Join-Path $publishDir "BimDownInstaller.exe"
$exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
Write-Host "  $exePath ($exeSize MB)" -ForegroundColor Green
