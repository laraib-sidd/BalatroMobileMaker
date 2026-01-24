# BalatroMobile Mod Preparation Script (Windows PowerShell)
# This script prepares mod files for transfer to Android

param(
    [string]$BalatroAppData = "$env:APPDATA\Balatro",
    [string]$OutputDir = ".\mod-package",
    [switch]$SkipCleanup
)

Write-Host "=== BalatroMobile Mod Preparation ===" -ForegroundColor Cyan
Write-Host ""

# Verify Balatro AppData exists
if (-not (Test-Path $BalatroAppData)) {
    Write-Host "ERROR: Balatro AppData not found at: $BalatroAppData" -ForegroundColor Red
    Write-Host "Make sure Balatro with mods is installed and has been run at least once."
    exit 1
}

$ModsPath = Join-Path $BalatroAppData "Mods"
if (-not (Test-Path $ModsPath)) {
    Write-Host "ERROR: Mods folder not found at: $ModsPath" -ForegroundColor Red
    exit 1
}

# Check for Lovely dump
$LovelyDumpPath = Join-Path $ModsPath "lovely\dump"
if (-not (Test-Path $LovelyDumpPath)) {
    Write-Host "ERROR: Lovely dump not found at: $LovelyDumpPath" -ForegroundColor Red
    Write-Host "Run Balatro with mods on PC first to generate the dump files."
    exit 1
}

Write-Host "Found Balatro mods at: $ModsPath" -ForegroundColor Green
Write-Host "Found Lovely dump at: $LovelyDumpPath" -ForegroundColor Green
Write-Host ""

# Create output directory
if (Test-Path $OutputDir) {
    if (-not $SkipCleanup) {
        Write-Host "Cleaning existing output directory..."
        Remove-Item -Recurse -Force $OutputDir
    }
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$GameDir = Join-Path $OutputDir "game"
New-Item -ItemType Directory -Force -Path $GameDir | Out-Null

Write-Host "Preparing mod package in: $OutputDir" -ForegroundColor Yellow
Write-Host ""

# Step 1: Copy Mods folder
Write-Host "[1/6] Copying Mods folder..." -ForegroundColor Cyan
$ModsDestPath = Join-Path $GameDir "Mods"
Copy-Item -Recurse -Force $ModsPath $ModsDestPath
Write-Host "  Copied Mods folder" -ForegroundColor Green

# Step 2: Copy Lovely dump files to game root
Write-Host "[2/6] Copying Lovely dump files..." -ForegroundColor Cyan
Get-ChildItem -Path $LovelyDumpPath | ForEach-Object {
    Copy-Item -Recurse -Force $_.FullName $GameDir
}
Write-Host "  Copied dump files" -ForegroundColor Green

# Step 3: Create SMODS folder with version.lua
Write-Host "[3/6] Creating SMODS folder..." -ForegroundColor Cyan
$SmodsLibPath = Join-Path $ModsPath "smods"
$SmodsDestPath = Join-Path $GameDir "SMODS"
New-Item -ItemType Directory -Force -Path $SmodsDestPath | Out-Null
if (Test-Path (Join-Path $SmodsLibPath "version.lua")) {
    Copy-Item -Force (Join-Path $SmodsLibPath "version.lua") $SmodsDestPath
}
if (Test-Path (Join-Path $SmodsLibPath "release.lua")) {
    Copy-Item -Force (Join-Path $SmodsLibPath "release.lua") $SmodsDestPath
}
Write-Host "  Created SMODS folder" -ForegroundColor Green

# Step 4: Create nativefs stub
Write-Host "[4/6] Creating nativefs stub..." -ForegroundColor Cyan
$NativefsDir = Join-Path $GameDir "nativefs"
New-Item -ItemType Directory -Force -Path $NativefsDir | Out-Null
$NativefsStub = @'
-- NativeFS stub for Android - with working directory support
-- CRITICAL: This stub must resolve paths relative to the working directory
local nfs = {}
local lf = love.filesystem
local _workingDir = ""

-- Helper to resolve paths relative to working directory
local function resolvePath(path)
    if not path then return path end
    if path:sub(1,1) == "/" then return path end
    if _workingDir ~= "" then
        return _workingDir .. "/" .. path
    end
    return path
end

function nfs.read(path)
    local resolved = resolvePath(path)
    local data = lf.read(resolved)
    if data then return data end
    return lf.read(path)
end

function nfs.load(path)
    local resolved = resolvePath(path)
    local chunk = lf.load(resolved)
    if chunk then return chunk end
    return lf.load(path)
end

function nfs.getDirectoryItems(dir)
    local resolved = resolvePath(dir)
    local items = lf.getDirectoryItems(resolved)
    if items and #items > 0 then return items end
    return lf.getDirectoryItems(dir) or {}
end

function nfs.getDirectoryItemsInfo(dir, filtertype)
    local resolved = resolvePath(dir)
    local items = lf.getDirectoryItems(resolved)
    if not items or #items == 0 then
        items = lf.getDirectoryItems(dir) or {}
        resolved = dir
    end
    local result = {}
    for _, item in ipairs(items) do
        local fullpath = resolved .. "/" .. item
        local info = lf.getInfo(fullpath)
        if info then
            if not filtertype or info.type == filtertype then
                info.name = item
                table.insert(result, info)
            end
        end
    end
    return result
end

function nfs.getInfo(path)
    local resolved = resolvePath(path)
    local info = lf.getInfo(resolved)
    if info then return info end
    return lf.getInfo(path)
end

function nfs.setWorkingDirectory(dir)
    _workingDir = dir or ""
    return true
end

function nfs.getWorkingDirectory()
    if _workingDir == "" then return lf.getSaveDirectory() end
    return _workingDir
end

function nfs.write(path, data) return lf.write(resolvePath(path), data) end
function nfs.append(path, data) return lf.append(resolvePath(path), data) end
function nfs.createDirectory(path) return lf.createDirectory(resolvePath(path)) end
function nfs.remove(path) return lf.remove(resolvePath(path)) end
function nfs.getRealDirectory(path) return lf.getRealDirectory(resolvePath(path)) end
function nfs.getSaveDirectory() return lf.getSaveDirectory() end
function nfs.getSourceBaseDirectory() return lf.getSourceBaseDirectory and lf.getSourceBaseDirectory() or "" end

function nfs.isFile(path)
    local resolved = resolvePath(path)
    local info = lf.getInfo(resolved)
    if not info then info = lf.getInfo(path) end
    return info and info.type == "file"
end

function nfs.isDirectory(path)
    local resolved = resolvePath(path)
    local info = lf.getInfo(resolved)
    if not info then info = lf.getInfo(path) end
    return info and info.type == "directory"
end

function nfs.mount(archive, mountpoint, appendToPath) return lf.mount(archive, mountpoint, appendToPath) end
function nfs.unmount(archive) return lf.unmount(archive) end
function nfs.lines(path) return lf.lines(resolvePath(path)) end
function nfs.newFile(path, mode) return lf.newFile(resolvePath(path), mode) end
function nfs.newFileData(contents, name) return lf.newFileData(contents, name) end

return nfs
'@
Set-Content -Path (Join-Path $NativefsDir "init.lua") -Value $NativefsStub -NoNewline
Write-Host "  Created nativefs/init.lua" -ForegroundColor Green

# Step 5: Create lovely stub
Write-Host "[5/6] Creating lovely stub..." -ForegroundColor Cyan
$LovelyDir = Join-Path $GameDir "lovely"
New-Item -ItemType Directory -Force -Path $LovelyDir | Out-Null
$LovelyStub = @'
-- Lovely stub for mobile
local lovely = {}
lovely.mod_dir = "Mods"
lovely.path = "Mods"
lovely.version = "0.6.0"
return lovely
'@
Set-Content -Path (Join-Path $LovelyDir "init.lua") -Value $LovelyStub -NoNewline
Write-Host "  Created lovely/init.lua" -ForegroundColor Green

# Step 6: Create lovely.lua config
Write-Host "[6/6] Creating lovely.lua config..." -ForegroundColor Cyan
$LovelyConfig = @'
return {
    repo = "https://github.com/ethangreen-dev/lovely-injector",
    version = "0.6.0",
    mod_dir = "/data/data/com.unofficial.balatro/files/save/game/Mods",
}
'@
Set-Content -Path (Join-Path $GameDir "lovely.lua") -Value $LovelyConfig -NoNewline
Write-Host "  Created lovely.lua" -ForegroundColor Green

Write-Host ""
Write-Host "=== Mod package prepared successfully! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Output location: $OutputDir\game\" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Build the APK using BalatroMobile.exe"
Write-Host "2. Install the APK on your Android device"
Write-Host "3. Launch the game once (creates directories)"
Write-Host "4. Run transfer-mods.ps1 to transfer files via ADB"
Write-Host ""
