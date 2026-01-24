# BalatroMobileMaker Changelog & Roadmap

## Project Overview

A tool to create modded Balatro APKs for Android devices. This project makes mobile Balatro modding accessible to users without requiring technical expertise.

---

## v1.0.0 - Modded APK Support (January 2026)

### What's New

- **Full Mod Support** - Cryptid, Talisman, Steamodded, and other mods working on Android
- **Verified Working** - Tested and confirmed on Android emulator and physical devices
- **Comprehensive Documentation** - Step-by-step guide for building and deploying modded APKs

### Technical Achievements

- Successfully built vanilla Balatro APK with mobile patches
- Successfully loaded Steamodded mods on Android
- Identified and documented all compatibility requirements
- Created stubs for `nativefs` and `lovely` modules

---

## Research Findings

### What We Learned

1. **Vanilla APK Building Works** - Successfully building Balatro APKs that run on Android
2. **Mobile Mods Require File Transfer** - Mods must be transferred to device storage after APK installation
3. **Lovely Dump is Essential** - The dump files from Lovely injector contain the mod loading hooks
4. **BalatroMobileCompat Required** - Re-applies mobile patches that Lovely dump overwrites

### Technical Discoveries

| Discovery | Detail |
|-----------|--------|
| `game.love` structure | `main.lua` must be at ZIP root (not nested in folders) |
| APK signing | UberApkSigner outputs `*-aligned-debugSigned.apk` |
| Lovely dump | Contains patched Lua that enables mod loading |
| nativefs | Uses FFI which doesn't work on Android - needs stub |
| lovely module | Expected by dump files - needs stub |
| macOS metadata | `._*` files cause Lua syntax errors - must be removed |
| Android 11+ | Restricts `/sdcard/Android/data/` access - use `run-as` workaround |

### Common Issues & Fixes

| Issue | Cause | Fix |
|-------|-------|-----|
| `No code to run` | `main.lua` not at ZIP root | Ensure `game.love` has correct structure |
| `module 'nativefs' not found` | FFI not available on Android | Use nativefs stub wrapper |
| `module 'lovely' not found` | Native injector not on Android | Use lovely stub module |
| `._*.lua unexpected symbol` | macOS AppleDouble metadata files | Remove `._*` files after transfer |
| `SMODS.path nil` | nativefs stub not tracking working directory | Fixed stub implementation |
| `Permission denied` (Android 11+) | Scoped storage restrictions | Use `run-as` with tar archive |

---

## Modding Architecture

### How It Works

```
[PC] Balatro + Mods + Lovely
         ↓
    Lovely Dump (patched Lua files)
         ↓
[Build] Vanilla APK + Mobile Patches
         ↓
[Transfer] Mods + Dump + Stubs → Android Storage
         ↓
[Android] Game loads mods from storage
```

### Required Components

1. **Vanilla APK** - Built with mobile compatibility patches
2. **Lovely Dump** - Patched game files with mod loading hooks
3. **Mods Folder** - All mods from PC (Cryptid, Talisman, etc.)
4. **Stubs** - nativefs and lovely compatibility stubs
5. **BalatroMobileCompat** - Re-applies mobile patches

### File Structure on Android

```
/data/data/com.unofficial.balatro/files/save/game/
├── Mods/                          # Full Mods folder from PC
│   ├── BalatroMobileCompat/       # Required for mobile patches
│   ├── Cryptid/
│   ├── Talisman/
│   ├── smods/
│   └── lovely/
├── [Lovely dump contents]         # Files from Mods/lovely/dump/
│   ├── main.lua
│   ├── globals.lua
│   ├── game.lua
│   ├── functions/
│   └── ...
├── SMODS/
│   └── version.lua
├── nativefs/
│   └── init.lua                   # Stub wrapper for love.filesystem
├── lovely/
│   └── init.lua                   # Stub providing mod_dir
└── lovely.lua                     # Config file
```

---

## Development Phases

### Phase 1: External Storage Solution - COMPLETE
**Status: Working**

- [x] Extract Balatro.exe game content
- [x] Apply mobile compatibility patches
- [x] Create correct game.love structure
- [x] Build and sign APK
- [x] Create nativefs stub
- [x] Create lovely stub
- [x] File transfer via ADB
- [x] macOS metadata cleanup
- [x] Verified working on Android

### Phase 2: Pre-built Mod Packs
**Status: Planned**

- Create pre-configured mod pack APKs
- Popular mod combinations ready to install
- No user configuration needed

### Phase 3: Custom Injector (Research)
**Status: Future**

- Explore building Lovely alternative
- Load mods from APK assets
- True single-APK solution

---

## Build Instructions

### Prerequisites

1. **Balatro** installed on PC with mods working
2. **Lovely + Steamodded** generating dump files
3. **Java** for APK building
4. **ADB** for file transfer (or manual transfer)

### Step 1: Build Vanilla APK

```bash
# Extract game content from Balatro.exe
# Apply mobile patches to Lua files
# Create game.love with main.lua at root
# Build APK using apktool
# Sign with uber-apk-signer
```

### Step 2: Prepare Mod Package

```bash
# Copy Mods folder from %APPDATA%/Balatro/
# Copy Lovely dump from Mods/lovely/dump/
# Create nativefs and lovely stubs
# Create lovely.lua config
# Remove macOS ._* files
```

### Step 3: Transfer to Device

```bash
# Create tar archive
tar -cvf transfer.tar -C game .

# Push to device
adb push transfer.tar /data/local/tmp/

# Extract using run-as
adb shell "run-as com.unofficial.balatro sh -c 'cd /data/data/com.unofficial.balatro/files/save/game && tar -xf /data/local/tmp/transfer.tar'"

# Clean macOS metadata
adb shell "run-as com.unofficial.balatro find /data/data/com.unofficial.balatro/files/save/game -name '._*' -delete"
```

### Step 4: Launch and Play

Install APK, launch game, enjoy mods!

---

## Stub Implementations

### nativefs/init.lua

```lua
-- NativeFS stub for Android
-- Wraps love.filesystem for compatibility
local nfs = {}
local lf = love.filesystem
local _workingDir = ""

function nfs.read(path) return lf.read(path) end
function nfs.getDirectoryItems(dir) return lf.getDirectoryItems(dir) or {} end
function nfs.getInfo(path) return lf.getInfo(path) end
function nfs.setWorkingDirectory(dir) _workingDir = dir or ""; return true end
function nfs.getWorkingDirectory()
    if _workingDir == "" then return lf.getSaveDirectory() end
    return _workingDir
end
function nfs.write(path, data) return lf.write(path, data) end
function nfs.createDirectory(path) return lf.createDirectory(path) end
function nfs.remove(path) return lf.remove(path) end
function nfs.isFile(path)
    local info = lf.getInfo(path)
    return info and info.type == "file"
end
function nfs.isDirectory(path)
    local info = lf.getInfo(path)
    return info and info.type == "directory"
end

return nfs
```

### lovely/init.lua

```lua
-- Lovely stub for mobile
local lovely = {}
lovely.mod_dir = "Mods"
lovely.version = "0.6.0"
return lovely
```

### lovely.lua (config)

```lua
return {
    repo = "https://github.com/ethangreen-dev/lovely-injector",
    version = "0.6.0",
    mod_dir = "/data/data/com.unofficial.balatro/files/save/game/Mods",
}
```

---

## References

- [Issue #137 - Lovely/Steamodded mods on Android](https://github.com/blake502/balatro-mobile-maker/issues/137)
- [BalatroMobileCompat mod](https://github.com/eeve-lyn/BalatroMobileCompat)
- [Original balatro-mobile-maker](https://github.com/blake502/balatro-mobile-maker)
- [Lovely Injector](https://github.com/ethangreen-dev/lovely-injector)
- [Steamodded](https://github.com/Steamodded/smods)

---

## Contributing

This project is in active development. PRs welcome for:
- Automated build tool improvements
- Additional mod compatibility
- Documentation enhancements
