---
date: 2026-02-19
feature: Main Menu Sidebar + Custom Element Fix
plan: thoughts/shared/plans/002_tile_refactor_native_style_preview.md
status: complete
last_commit: 6084c2b
---

# Session 014: Main Menu Sidebar Entry + Custom Element Creation Fix

## Objectives
- Enable the plugin config page in Jellyfin's admin sidebar main menu
- Fix custom element creation syntax for emby-scroller and emby-itemscontainer

## Accomplishments

### 1. Enable Main Menu Sidebar Entry
- Added `EnableInMainMenu = true` to the `PluginPageInfo` in `Plugin.cs`
- This makes the Music Discovery config page appear in Jellyfin's admin sidebar under the "server" section with the `music_note` icon

### 2. Fix Custom Element Creation Syntax
- **Root cause**: `document.createElement('div', { is: 'emby-scroller' })` uses the options object form of createElement, but Jellyfin's custom element registry expects the second argument as a plain string
- **Fix**: Changed to `document.createElement('div', 'emby-scroller')` for both `emby-scroller` and `emby-itemscontainer`
- The `setAttribute('is', ...)` call was already present as a fallback, so functionality was partially working before, but the proper creation syntax ensures the custom element lifecycle hooks fire correctly

## File Changes
- `Jellyfin.Plugin.MusicDiscovery/Plugin.cs` — Added `EnableInMainMenu = true` to PluginPageInfo
- `Jellyfin.Plugin.MusicDiscovery/Web/discoveryPanel.js` — Fixed createElement calls for emby-scroller and emby-itemscontainer

## Decisions Made
- Used string form of createElement second arg to match Jellyfin's custom element pattern

## Test Status
- [ ] Manual testing in Jellyfin UI — verify sidebar entry appears
- [ ] Manual testing — verify scroller behavior unchanged or improved

## Ready to Resume
To continue this work:
1. Build and deploy the plugin to Jellyfin
2. Verify the config page appears in the admin sidebar
3. Verify the horizontal scroller initializes correctly with native scroll behavior
