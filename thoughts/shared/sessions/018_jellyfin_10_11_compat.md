---
date: 2026-02-19
feature: Jellyfin 10.11 Compatibility Fix
plan: thoughts/shared/plans/001_music_discovery_plugin.md
status: complete
last_commit: 93af0a7
---

# Session 018: Jellyfin 10.11 Compatibility Fix

## Objectives
- Fix `MissingMethodException` when calling `/MusicDiscovery/Similar/{itemId}` on Jellyfin 10.11.x

## Accomplishments

### 1. Root Cause Identified
- `ILibraryManager.GetItemList()` return type changed from `List<BaseItem>` to `IReadOnlyList<BaseItem>` in Jellyfin 10.11
- The CLR treats a return-type change as a completely different method, so the plugin compiled against 10.10 throws `MissingMethodException` at runtime on 10.11
- This is a known breaking change affecting multiple community plugins (TMDb Box Sets, Plexyfin, Merge Versions)

### 2. Fix Applied
- Updated target framework: `net8.0` → `net9.0`
- Updated NuGet packages: `Jellyfin.Controller` and `Jellyfin.Model` `10.10.0` → `10.11.6`
- Updated `meta.json`: `targetAbi` → `10.11.0.0`, version → `0.14.0.0`
- No C# code changes needed — LINQ calls (`.Select()`, `.OfType()`, `.ToHashSet()`) work on both `List<T>` and `IReadOnlyList<T>` via `IEnumerable<T>`

## Decisions Made
- Target Jellyfin 10.11+ only (no dual 10.10/10.11 support via reflection)
- Used latest stable NuGet packages (10.11.6) rather than minimum 10.11.0

## File Changes
- `Jellyfin.Plugin.MusicDiscovery/Jellyfin.Plugin.MusicDiscovery.csproj` — TFM and package versions
- `Jellyfin.Plugin.MusicDiscovery/meta.json` — targetAbi and version

## Test Status
- [x] `dotnet build` succeeds (0 warnings, 0 errors)
- [ ] Manual testing — deploy DLL and verify recommendations load without error

## Ready to Resume
To continue this work:
1. Deploy the new `bin/Debug/net9.0/Jellyfin.Plugin.MusicDiscovery.dll` to Jellyfin
2. Restart Jellyfin and verify the MissingMethodException is gone
3. Verify recommendations load on artist/album/track pages
4. If all good, consider tagging a release
