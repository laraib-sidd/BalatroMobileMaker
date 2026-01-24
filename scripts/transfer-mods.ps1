# BalatroMobile Mod Transfer Script (Windows PowerShell)
# This script transfers prepared mod files to Android device via ADB

param(
    [string]$ModPackagePath = ".\mod-package\game",
    [switch]$UseExternalStorage
)

Write-Host "=== BalatroMobile Mod Transfer ===" -ForegroundColor Cyan
Write-Host ""

# Check ADB is available
$adb = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adb) {
    Write-Host "ERROR: ADB not found in PATH" -ForegroundColor Red
    Write-Host "Install Android SDK Platform Tools and add to PATH"
    exit 1
}

# Check device is connected
$devices = & adb devices 2>&1
if ($devices -notmatch "device$") {
    Write-Host "ERROR: No Android device connected" -ForegroundColor Red
    Write-Host "Connect your device and enable USB debugging"
    exit 1
}
Write-Host "Android device connected" -ForegroundColor Green

# Verify mod package exists
if (-not (Test-Path $ModPackagePath)) {
    Write-Host "ERROR: Mod package not found at: $ModPackagePath" -ForegroundColor Red
    Write-Host "Run prepare-mods.ps1 first"
    exit 1
}
Write-Host "Found mod package at: $ModPackagePath" -ForegroundColor Green
Write-Host ""

# Set target path based on storage type
if ($UseExternalStorage) {
    $TargetPath = "/sdcard/Android/data/com.unofficial.balatro/files/save/game"
    Write-Host "Using EXTERNAL storage: $TargetPath" -ForegroundColor Yellow
    Write-Host "Note: This may not work on Android 11+ without root" -ForegroundColor Yellow
} else {
    $TargetPath = "/data/data/com.unofficial.balatro/files/save/game"
    Write-Host "Using INTERNAL storage: $TargetPath" -ForegroundColor Yellow
}
Write-Host ""

# Create tar archive
Write-Host "[1/4] Creating tar archive..." -ForegroundColor Cyan
$TarFile = ".\mod-transfer.tar"
if (Test-Path $TarFile) {
    Remove-Item -Force $TarFile
}

# Use tar command (available in Windows 10+)
Push-Location $ModPackagePath
& tar -cvf "..\..\..\mod-transfer.tar" . 2>&1 | Out-Null
Pop-Location

if (-not (Test-Path $TarFile)) {
    Write-Host "ERROR: Failed to create tar archive" -ForegroundColor Red
    exit 1
}
Write-Host "  Created tar archive" -ForegroundColor Green

# Push tar to device
Write-Host "[2/4] Pushing tar to device..." -ForegroundColor Cyan
& adb push $TarFile /data/local/tmp/mod-transfer.tar 2>&1
Write-Host "  Pushed to /data/local/tmp/" -ForegroundColor Green

# Extract on device using run-as
Write-Host "[3/4] Extracting on device..." -ForegroundColor Cyan
if ($UseExternalStorage) {
    # For external storage, try direct push (may fail on Android 11+)
    & adb shell "mkdir -p $TargetPath" 2>&1
    & adb shell "cd $TargetPath && tar -xf /data/local/tmp/mod-transfer.tar" 2>&1
} else {
    # For internal storage, use run-as
    & adb shell "run-as com.unofficial.balatro sh -c 'mkdir -p $TargetPath && cd $TargetPath && tar -xf /data/local/tmp/mod-transfer.tar'" 2>&1
}
Write-Host "  Extracted files" -ForegroundColor Green

# Clean up macOS metadata files (in case package was prepared on Mac)
Write-Host "[4/4] Cleaning up metadata files..." -ForegroundColor Cyan
if ($UseExternalStorage) {
    & adb shell "find $TargetPath -name '._*' -type f -delete" 2>&1
} else {
    & adb shell "run-as com.unofficial.balatro find $TargetPath -name '._*' -type f -delete" 2>&1
}
Write-Host "  Cleaned up ._* files" -ForegroundColor Green

# Cleanup local tar
Remove-Item -Force $TarFile -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Transfer complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Verifying installation..." -ForegroundColor Yellow
if ($UseExternalStorage) {
    $modsCheck = & adb shell "ls $TargetPath/Mods/ 2>/dev/null" 2>&1
} else {
    $modsCheck = & adb shell "run-as com.unofficial.balatro ls $TargetPath/Mods/ 2>/dev/null" 2>&1
}
Write-Host "Mods found: $modsCheck" -ForegroundColor Cyan
Write-Host ""
Write-Host "Launch Balatro on your device to test!" -ForegroundColor Green
