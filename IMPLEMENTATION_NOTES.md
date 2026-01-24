# BalatroMobile Implementation Notes

## STATUS: WORKING (January 24, 2026)

**Modded Balatro running on Android!** The hybrid approach works:
- Lovely dump bundled in game.love
- Mods transferred to save directory
- Custom nativefs stub handles Android limitations

See `local/WORKING_STEPS.md` for complete working procedure.

---

## The Working Solution

### Key Insight: Hybrid Approach

The solution requires a **hybrid** bundling strategy:

1. **Bundle IN game.love:** Lovely dump files (mod loader), nativefs stub, lovely stub, SMODS/version.lua, json.lua
2. **Transfer to save directory:** Actual Mods folder

**Why:** LÖVE loads from game.love FIRST. Files in save directory won't override game.love, so the mod loader MUST be in game.love.

### Critical Configuration

**conf.lua must have `t.identity`:**
```lua
function love.conf(t)
    t.identity = "Balatro"  -- CRITICAL: Sets save path to save/Balatro/
    ...
end
```

Without `t.identity`, LÖVE uses unpredictable default save paths.

### NativeFS Stub

The FFI-based nativefs doesn't work on Android. Key requirements for the stub:

1. **Handle multiple read() signatures:**
   - `read(path)` → returns string
   - `read('data', path)` → returns FileData, NOT string!
   - `read('string', path, size)` → returns string with size limit

2. **newFileData() must support both forms:**
   - `newFileData(contents, filename)` - two args
   - `newFileData(filepath)` - one arg, reads file first

---

## History of Issues (Resolved)

### Issue 1: Build Environment Validation Failed
- **Root Cause:** `java -version` outputs to stderr
- **Fix:** Check stderr when stdout is empty

### Issue 2: Failed to Extract Game Content
- **Root Cause:** Code looked for Balatro.exe in Steam directory
- **Fix:** Look in current working directory

### Issue 3: JSON module not found
- **Root Cause:** Missing json.lua
- **Fix:** Copy from smods/libs/json/

### Issue 4: nativefs FFI errors
- **Root Cause:** LuaJIT FFI doesn't work on Android
- **Fix:** Created nativefs stub using love.filesystem

### Issue 5: SMODS.path is nil
- **Root Cause:** find_self() couldn't find core.lua
- **Multiple causes explored:**
  - lovely stub missing `path` field
  - Wrong save directory path (t.identity not set)
  - nativefs not properly handling working directory
- **Fix:** Proper t.identity, correct lovely.mod_dir, working nativefs stub

### Issue 6: SMODS/version and release not found
- **Root Cause:** main.lua requires 'SMODS.version' and 'SMODS.release'
- **Fix:** Copy both files to SMODS/ directory in game.love

### Issue 7: newFileData errors
- **Root Cause:** nativefs.read('data', path) returned string instead of FileData
- **Fix:** Check container type and return FileData when mode='data'

### Issue 8: Sound thread crashes
- **Root Cause:** newDecoder received invalid FileData
- **Fix:** Ensure nativefs.read('data', path) properly creates FileData

### Issue 9: Mods not loading (vanilla game shown)
- **Root Cause:** LÖVE loads game.love first, ignoring save directory files
- **Fix:** Bundle Lovely dump INTO game.love (hybrid approach)

---

## Key Files

| File | Purpose |
|------|---------|
| `local/WORKING_STEPS.md` | Complete working procedure |
| `local/hybrid-build/` | Working build directory |
| `src/BalatroMobile.Core/Services/ModTransferService.cs` | Needs update with working nativefs stub |
| `src/BalatroMobile.Core/Services/BuildService.cs` | Main build orchestration |

---

## What Works

1. ✅ APK builds successfully
2. ✅ Game installs on Android
3. ✅ Mods load correctly (Steamodded, Cryptid, Talisman)
4. ✅ MODS button appears in menu
5. ✅ Gameplay with mods works
6. ⚠️ HTTP module unavailable (expected - no networking on Android LÖVE)

---

## Next Steps

1. **Update ModTransferService.cs** with working nativefs stub
2. **Update BuildService.cs** to use hybrid approach:
   - Bundle Lovely dump in game.love
   - Transfer Mods separately
3. **Add t.identity** to conf.lua patching
4. **Test full build pipeline**
5. **Create PR and release**

---

## Testing Checklist

Before each release:
- [ ] Build APK with hybrid approach
- [ ] Install on Android device/emulator
- [ ] Launch game - should show MODS button
- [ ] Click MODS - should list installed mods
- [ ] Start game - mods should affect gameplay
- [ ] Check Cryptid content visible
