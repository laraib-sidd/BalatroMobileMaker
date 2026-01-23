# BalatroMobile 🃏📱

**Build Balatro for Mobile Devices with Full Mod Support**

BalatroMobile is a modern, re-architected tool for building Balatro (the popular roguelike deck-building game) for Android and iOS devices, with comprehensive mod support including Cryptid, Talisman, and other popular mods.

## ✨ Features

- 🖱️ **Double-Click to Run** - No CLI knowledge needed, just download and run
- 🧙 **Interactive Wizard** - Guided setup with simple Y/N prompts  
- 🔍 **Smart Path Detection** - Auto-finds Balatro in any Steam library, or prompts you
- 🛡️ **Pre-flight Checks** - Comprehensive system validation before building
- 🔧 **Full Mod Support** - Automatic Lovely + Steamodded injection
- 📱 **Cross-Platform** - Android and iOS support
- 📦 **Single EXE** - Self-contained, no .NET runtime required

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

Before using BalatroMobile, ensure your system meets all requirements:

1. **Steam Balatro** installed and working
2. **Mods properly set up** (Lovely, Steamodded, Cryptid, etc.)
3. **Java/OpenJDK** installed for APK building
4. **(Optional)** Android device with USB debugging for save transfer

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
# Build for Android (recommended)
BalatroMobile build --platform android

# Build for iOS (experimental)
BalatroMobile build --platform ios

# Build with custom options
BalatroMobile build --platform android --fps-cap 60 --landscape
```

### Save Transfer

Transfer your saves between PC and mobile:

```bash
# Transfer saves from PC to Android
BalatroMobile transfer --from pc --to android

# Transfer saves from Android to PC
BalatroMobile transfer --from android --to pc
```

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

### Getting Help

- Check the [Issues](https://github.com/laraib-sidd/BalatroMobileMaker/issues) page
- Join the [Balatro Modding Discord](https://discord.gg/balatro)
- Read the [Balatro Mobile Modding Guide](https://github.com/laraib-sidd/BalatroMobileMaker/wiki)

---

**Happy modding! 🎴✨**