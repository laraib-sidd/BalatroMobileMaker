# BalatroMobile Implementation Notes

## Current Issue (January 2026)

**Crash:** `[SMODS _ "src/game_object.lua"]:433: bad argument #2 to 'newFileData' (string expected, got nil)`

### Root Cause
The `nativefs` stub in `ModTransferService.cs` does NOT properly resolve paths relative to the working directory.

When SMODS does:
```lua
nfs.setWorkingDirectory("Mods/smods/assets/2x")
nfs.read("mod_tags.png")  -- Expects to read Mods/smods/assets/2x/mod_tags.png
```

Our current stub does:
```lua
function nfs.read(path) return lf.read(path) end  -- Just reads "mod_tags.png" - FAILS!
```

It should use the working directory to construct the full path.

### The Fix Required
The `nativefs` stub needs a `resolvePath()` helper that prepends the working directory:

```lua
local function resolvePath(path)
    if path:sub(1,1) == "/" then return path end  -- absolute path
    if _workingDir ~= "" then
        return _workingDir .. "/" .. path
    end
    return path
end

function nfs.read(path)
    local resolved = resolvePath(path)
    local data = lf.read(resolved)
    if data then return data end
    return lf.read(path)  -- fallback
end
```

---

## History of Issues

### Issue 1: Build Environment Validation Failed
- **Symptom:** Java and APKTool tests pass but validation fails
- **Root Cause:** `java -version` outputs to stderr, not stdout. JavaTool only returned stdout on success.
- **Fix:** Modified `ExecuteAndCaptureOutputAsync` to return stderr when stdout is empty

### Issue 2: Failed to Extract Game Content
- **Symptom:** Balatro.exe couldn't be found
- **Root Cause:** Code tried to read from Steam directory, but users needed to copy Balatro.exe locally
- **Fix:** Changed to look for Balatro.exe in current working directory

### Issue 3: JSON module not found
- **Symptom:** `module 'json' not found`
- **Root Cause:** Missing json.lua in game.love
- **Fix:** Need to copy json.lua from mods

### Issue 4: nativefs FFI errors
- **Symptom:** FFI calls fail on Android
- **Root Cause:** nativefs uses LuaJIT FFI which doesn't work on Android
- **Fix:** Created nativefs stub using love.filesystem

### Issue 5: SMODS.path is nil
- **Symptom:** `main.lua:1365: attempt to concatenate field 'path' (a nil value)`
- **Root Cause:** `lovely` module stub didn't have `path` field
- **Fix:** Added `lovely.path = "Mods"` to lovely stub

### Issue 6: SMODS/version not found
- **Symptom:** `no 'SMODS/version' in LOVE game directories`
- **Root Cause:** main.lua requires 'SMODS.version' but files are in Mods/smods/
- **Fix:** Copy version.lua and release.lua to SMODS/ directory

### Issue 7: nativefs.read returns nil (FIXED in this commit)
- **Symptom:** `bad argument #2 to 'newFileData' (string expected, got nil)`
- **Root Cause:** nativefs stub doesn't use working directory for read()
- **Fix:** Added resolvePath() helper that prepends _workingDir to relative paths
- **Additional Fix:** Now creates nativefs.lua at root AND replaces all FFI nativefs.lua in mods

---

## Key Files

| File | Purpose |
|------|---------|
| `src/BalatroMobile.Core/Services/ModTransferService.cs` | Creates nativefs and lovely stubs |
| `src/BalatroMobile.Core/Services/BuildService.cs` | Main build orchestration |
| `src/BalatroMobile.Infrastructure/Tools/GameExtractor.cs` | Extracts Balatro.exe content |

---

## What Works

1. ✅ APK builds successfully
2. ✅ Game installs on Android
3. ✅ Base game runs (without mods)
4. ✅ Mods get bundled into APK
5. ⏳ Modded game - testing needed after nativefs fix

---

## Original balatro-mobile-maker Approach

The original blake502/balatro-mobile-maker:
- **Does NOT bundle mods into APK**
- Creates vanilla Balatro APK only
- Users must transfer modded saves via ADB separately
- Mods are "not officially supported"

Our approach tries to bundle mods directly, which requires the nativefs compatibility layer.

---

## Testing Checklist

Before each release:
- [ ] Build APK with mods enabled
- [ ] Install on Android device/emulator
- [ ] Launch game - should reach main menu
- [ ] Check SMODS loads correctly
- [ ] Check Cryptid/Talisman content visible

---

## Next Steps

1. Fix nativefs stub in ModTransferService.cs
2. Test on Android VM
3. Create PR with fix
4. Tag new release
