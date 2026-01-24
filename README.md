# BalatroMobile 🃏📱

**Build Balatro for Mobile Devices with Full Mod Support**

BalatroMobile is a modern, re-architected tool for building Balatro (the popular roguelike deck-building game) for Android devices, with comprehensive mod support including Cryptid, Talisman, and other popular mods.

## ✨ Features

- 🖱️ **Double-Click to Run** - No CLI knowledge needed, just download and run
- 🧙 **Interactive Wizard** - Guided setup with simple Y/N prompts  
- 🔍 **Smart Path Detection** - Auto-finds Balatro in any Steam library, or prompts you
- 🛡️ **Pre-flight Checks** - Comprehensive system validation before building
- 🔧 **Full Mod Support** - Automatic Lovely + Steamodded injection
- 📱 **Android Support** - Build modded APKs for Android devices
- 📦 **Single EXE** - Self-contained, no .NET runtime required
- ⬇️ **Auto-Download Tools** - Java, APKTool, and other dependencies downloaded automatically

## 🏗️ Architecture

```
BalatroMobile/
├── Core/                          # Business logic layer
│   ├── Services/
│   │   ├── BuildService.cs       # APK/IPA building orchestration
│   │   ├── PatchService.cs       # Game patching logic
│   │   ├── TransferService.cs    # Save transfer operations
│   │   └── ModInjectionService.cs # Mod injection pipeline
│   ├── Models/                   # Domain models
│   └── Validators/               # Validation logic
├── Configuration/                # Configuration layer
│   ├── Models/                   # Configuration models
│   ├── Services/                 # Configuration services
│   └── appsettings.json          # Runtime configuration
├── Infrastructure/              # External dependencies
│   ├── Tools/                    # External tool wrappers
│   ├── Downloads/                # Download management
│   └── Platforms/                # Platform-specific operations
└── Cli/                         # Presentation layer
    └── Program.cs               # Command-line interface
```

## 🚀 Quick Start

### For Users (Recommended)

1. **Download** `BalatroMobile.exe` from the [Releases](https://github.com/laraib-sidd/BalatroMobileMaker/releases) page
2. **Double-click** to run - no installation needed!
3. **Follow the prompts** - the interactive wizard will guide you through everything

That's it! The tool will:
- Auto-detect your Balatro installation (or ask you to provide the path)
- Run pre-flight checks to ensure everything is ready
- Guide you through build options with simple Y/N questions
- Create your modded APK

### Prerequisites

Before using BalatroMobile, ensure:

1. **Steam Balatro** installed and working on your PC
2. **Mods properly set up** (Lovely, Steamodded, Cryptid, etc.) and working
3. **Run the game once** with mods to generate the Lovely dump
4. **(Optional)** Android device with USB debugging for save transfer - NOT required for building APKs

**Note:** Java, APKTool, and other build tools are **automatically downloaded** on first run. No manual installation needed!

### Pre-Flight Check

The tool automatically runs pre-flight checks, but you can also run them manually:

```bash
# Run all pre-flight checks
BalatroMobile check

# Check specific requirement
BalatroMobile check --check-name ADBConnectionWorking
```

The pre-flight check will validate:
- ✅ Steam Balatro installation
- ✅ Mod folder structure and functionality
- ✅ Lovely injector and dump generation
- ✅ Android developer options and USB debugging
- ✅ ADB connection and device communication
- ✅ Java runtime availability
- ✅ Internet connectivity for downloads

### Building

Once all checks pass:

```bash
# Build for Android
BalatroMobile build

# Build with custom options
BalatroMobile build --fps 60 --high-dpi --inject-mods
```

### Save Transfer

Transfer your saves between PC and mobile:

```bash
# Transfer saves from PC to Android
BalatroMobile transfer --from pc --to android

# Transfer saves from Android to PC
BalatroMobile transfer --from android --to pc
```

### Managing Tools

BalatroMobile automatically downloads required tools (Java, APKTool, etc.) on first run. You can manage these tools manually:

```bash
# Check tools status
BalatroMobile tools status

# Force re-download all tools
BalatroMobile tools download

# Clear tool cache (forces re-download on next build)
BalatroMobile tools clear
```

Tools are cached in `%LOCALAPPDATA%\BalatroMobile\tools\` and only need to be downloaded once.

## 📋 Pre-Flight Checklist Details

The pre-flight check validates the following requirements:

### Game & Mod Setup
- **SteamBalatroInstalled**: Steam version of Balatro is installed
- **GameWorkingOnPC**: Balatro launches and works correctly on PC
- **ModsFolderStructure**: Mods folder has correct structure (Lovely, Steamodded, Cryptid, etc.)
- **LovelyInjectorWorking**: Lovely injector is installed and working
- **LovelyDumpExists**: Lovely dump exists and is not empty
- **ModsWorkingOnPC**: Mods work correctly on PC (Cryptid content visible)

### Android Setup
- **AndroidDeveloperOptions**: Android developer options are enabled
- **USBDebuggingEnabled**: USB debugging is enabled on Android device
- **ADBConnectionWorking**: ADB can communicate with Android device
- **AndroidStorageSpace**: Android device has sufficient storage space (optional)

### Development Environment
- **JavaRuntimeAvailable**: Java runtime is available for APK building
- **InternetConnection**: Internet connection available for downloading tools

## 🛠️ Building from Source

```bash
# Clone the repository
git clone https://github.com/laraib-sidd/BalatroMobileMaker.git
cd BalatroMobile

# Build the solution
dotnet build

# Run the application
dotnet run --project src/BalatroMobile.Cli -- check
```

## 📁 Project Structure

- **BalatroMobile.Core**: Core business logic and services
- **BalatroMobile.Configuration**: Configuration models and services
- **BalatroMobile.Infrastructure**: External tool integrations and platform operations
- **BalatroMobile.Cli**: Command-line interface

## 🔧 Configuration

Create an `appsettings.json` file in the Cli project directory to customize tool URLs and behavior:

```json
{
  "Tools": {
    "ApktoolUrl": "https://github.com/iBotPeaches/Apktool/releases/download/v2.9.3/apktool_2.9.3.jar",
    "UberApkSignerUrl": "https://github.com/patrickfav/uber-apk-signer/releases/download/v1.3.0/uber-apk-signer-1.3.0.jar",
    "Love2dApkUrl": "https://github.com/love2d/love-android/releases/download/11.5a/love-11.5-android-embed.apk"
  },
  "Build": {
    "DefaultFpsCap": 60,
    "DefaultLandscape": true,
    "DownloadTimeoutSeconds": 300
  }
}
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Run pre-flight checks: `dotnet run --project src/BalatroMobile.Cli -- check`
4. Implement your changes
5. Add tests and ensure they pass
6. Submit a pull request

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- The original Balatro team for creating an amazing game
- The Balatro modding community (especially Lovely, Steamodded, and Cryptid)
- All contributors to the original balatro-mobile-maker project
- The LÖVE framework developers

## 📱 Mobile Modding Guide

### How Mobile Modding Works

1. **Build vanilla APK** with mobile compatibility patches
2. **Transfer mod files** to device storage after installation
3. **Game loads mods** from storage location on launch

### Supported Mods (Tested)

- ✅ Steamodded (SMODS)
- ✅ Cryptid
- ✅ Talisman
- ✅ BalatroMobileCompat (required)
- ✅ Tag Manager
- ✅ Restart Run Button
- ✅ Brainstorm Reroll Button

### Quick Start with Helper Scripts (Windows)

We provide PowerShell scripts to automate the mod preparation and transfer process:

```powershell
# Step 1: Build APK using BalatroMobile.exe
.\BalatroMobile.exe build

# Step 2: Prepare mod package (creates stubs, copies files)
.\scripts\prepare-mods.ps1

# Step 3: Install APK on device and launch once
adb install balatro.apk
# Launch app manually on device, then close it

# Step 4: Transfer mods to device
.\scripts\transfer-mods.ps1
```

That's it! Launch Balatro on your device and enjoy mods.

### Manual Method (All Platforms)

#### Required Files for Modding

After building and installing the APK, transfer these files to your device:

```
/data/data/com.unofficial.balatro/files/save/game/
├── Mods/                      # Your PC Mods folder
│   ├── BalatroMobileCompat/   # Required!
│   ├── Cryptid/
│   ├── Talisman/
│   └── ...
├── [Lovely dump files]        # From Mods/lovely/dump/
├── nativefs/init.lua          # Stub file (see CHANGELOG.md)
├── lovely/init.lua            # Stub file (see CHANGELOG.md)
└── lovely.lua                 # Config file
```

#### Transfer via ADB

```bash
# Create tar archive of mod files
tar -cvf transfer.tar -C game .

# Push to device
adb push transfer.tar /data/local/tmp/

# Extract (Android 11+ compatible)
adb shell "run-as com.unofficial.balatro sh -c 'cd /data/data/com.unofficial.balatro/files/save/game && tar -xf /data/local/tmp/transfer.tar'"

# Clean macOS metadata files (important!)
adb shell "run-as com.unofficial.balatro find /data/data/com.unofficial.balatro/files/save/game -name '._*' -delete"
```

### Important Notes

- **macOS users**: Always remove `._*` files before transfer (they cause Lua syntax errors)
- **Android 11+**: Use `run-as` method shown above (direct push to app data is blocked)
- **First launch**: Install APK and launch once before transferring mods (creates directories)
- **BalatroMobileCompat**: This mod is **required** - it re-applies mobile patches that Lovely overwrites

See [CHANGELOG.md](CHANGELOG.md) for detailed technical documentation and stub implementations.

---

## 🐛 Troubleshooting

### Common Issues

**ADB Connection Failed**
```
❌ ADBConnectionWorking: ADB can communicate with Android device
💡 Connect Android device via USB, accept RSA prompt, run 'adb devices' to verify
```

**Mods Not Working**
```
❌ LovelyDumpExists: Lovely dump exists and is not empty
💡 Launch modded Balatro on PC, wait 10 seconds on main menu, then exit
```

**Java Not Found**
```
❌ JavaRuntimeAvailable: Java runtime is available for APK building
💡 Install OpenJDK or ensure Java is in PATH
```

**Game Crashes with "unexpected symbol" in ._*.lua**
```
❌ macOS metadata files included in transfer
💡 Remove ._* files: find . -name "._*" -delete (before transfer)
    Or on device: adb shell "run-as com.unofficial.balatro find ... -name '._*' -delete"
```

**Permission Denied on Android 11+**
```
❌ Cannot push to /sdcard/Android/data/
💡 Use the run-as method with tar archives (see Mobile Modding Guide above)
```

### Getting Help

- Check the [Issues](https://github.com/laraib-sidd/BalatroMobileMaker/issues) page
- Join the [Balatro Modding Discord](https://discord.gg/balatro)
- Read the [CHANGELOG.md](CHANGELOG.md) for technical details

---

**Happy modding! 🎴✨**