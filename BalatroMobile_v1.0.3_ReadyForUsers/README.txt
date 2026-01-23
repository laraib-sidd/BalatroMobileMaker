BalatroMobile Maker v1.0.3 - Original Style Interactive
==========================

A comprehensive tool for building Balatro mobile versions with mod support and save transfer capabilities.

USAGE:
------

Double-click BalatroMobile.Cli.exe or run:
BalatroMobile                          - Interactive mode (recommended for beginners)

Advanced usage:
BalatroMobile check                    - Run pre-flight checks
BalatroMobile build [options]          - Build for mobile
BalatroMobile transfer [options]       - Transfer saves between PC and Android

BUILD OPTIONS:
-------------
--platform <android|ios>              - Target platform (default: android)
--fps <default|none|60>               - FPS cap (default: default)
--no-landscape                         - Disable landscape lock
--high-dpi                             - Enable high DPI mode
--disable-crt                          - Disable CRT shader
--inject-mods                          - Inject mods during build
--output <path>                        - Output file path

TRANSFER OPTIONS:
----------------
--from <pc|android>                    - Source platform (default: pc)
--to <android|pc>                      - Target platform (default: android)
--no-backup                            - Skip backup creation
--no-mods                              - Skip mod-related files

EXAMPLES:
--------
BalatroMobile transfer
BalatroMobile transfer --from android --to pc
BalatroMobile build --inject-mods --high-dpi --output my-balatro.apk

REQUIREMENTS:
------------
- Steam Balatro installed on PC
- Java Runtime Environment (JRE)
- Android device with USB debugging enabled
- ADB (Android Debug Bridge) in PATH or system
- Internet connection for tool downloads

FEATURES:
--------
✓ Original mobile maker experience - exact same Y/N prompts and workflow!
✓ Just double-click and answer questions like the classic tool
✓ Build modded APKs with Cryptid, Talisman, and other Steamodded mods
✓ Comprehensive pre-flight validation
✓ Bidirectional save transfer (PC ↔ Android)
✓ Automatic backups and safety checks
✓ Cross-platform compatibility
✓ Same patching questions: FPS, landscape, high DPI, CRT shader

CHANGELOG:
----------
v1.0.3 - Original mobile maker experience! Exact same interactive flow with Y/N prompts
v1.0.2 - Interactive mode with guided setup prompts
v1.0.1 - Fixed startup crash and console encoding issues
v1.0.0 - Initial release with full mod support and save transfer

For more information, visit the GitHub repository or run: BalatroMobile --help