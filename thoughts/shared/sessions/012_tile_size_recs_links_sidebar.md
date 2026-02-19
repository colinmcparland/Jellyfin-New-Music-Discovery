---
date: 2026-02-19
feature: Tile Size, Recommendations, Last.fm Links, Sidebar Fix
plan: thoughts/shared/plans/002_tile_refactor_native_style_preview.md
status: complete
last_commit: dd91132
---

# Session 012: Tile Size, Recs Count, Last.fm Links, Sidebar Fix

## Objectives
- Match discovery tile size to native 'More Like This' tiles (173x173 responsive)
- Increase default recommendations from 8 to 12
- Add Last.fm links to card footer text (album/track name + artist name)
- Fix admin sidebar item not appearing for plugin config

## Accomplishments

### 1. Native Tile Sizing
- Switched from `squareCard`/`cardPadder-square` to `overflowSquareCard`/`cardPadder-overflowSquare`
- Added `cardBox-bottompadded` and `card-hoverable` classes
- Removed `cardFooter` wrapper — text divs are now direct children of `cardBox` (matches native DOM)
- Removed explicit `backgroundSize`/`backgroundPosition` (handled by `coveredImage` class)
- Switched scroller to `emby-scroller`/`emby-itemscontainer` custom elements

### 2. Default Recommendations → 12
- `PluginConfiguration.cs`: Default changed from 8 to 12
- `configPage.html`: Added `<option value="12">12</option>`
- `configPage.js`: Fallback updated from 8 to 12
- `MusicDiscoveryController.cs`: Album recommendation fetching now uses `(limit + 3) / 2` similar artists instead of hardcoded 5

### 3. Last.fm Links on Footer Text
- Album/track name: links to `rec.Links.LastFmUrl` (entity's Last.fm page)
- Artist name: links to `https://www.last.fm/music/{encodedArtistName}`
- Uses native `textActionButton` class for underline-on-hover
- Opens in new tab with `target="_blank"` and `rel="noopener noreferrer"`

### 4. Admin Sidebar Fix
- Added `DisplayName = "Music Discovery"` to `PluginPageInfo` in `Plugin.cs`
- `MenuSection` and `MenuIcon` were already present but `DisplayName` was missing

## File Changes
- `Jellyfin.Plugin.MusicDiscovery/Web/discoveryPanel.js` — Card classes + scroller + Last.fm links (118 lines changed)
- `Jellyfin.Plugin.MusicDiscovery/Api/MusicDiscoveryController.cs` — Dynamic artist count for album recs
- `Jellyfin.Plugin.MusicDiscovery/Configuration/configPage.html` — Added 12 option
- `Jellyfin.Plugin.MusicDiscovery/Configuration/configPage.js` — Updated default fallback
- `Jellyfin.Plugin.MusicDiscovery/Plugin.cs` — Added DisplayName
- `Jellyfin.Plugin.MusicDiscovery/PluginConfiguration.cs` — Default 8 → 12

## Decisions Made
- Used responsive `overflowSquareCard` vw-based sizing instead of hardcoding 173px
- Artist Last.fm URL constructed client-side (`https://www.last.fm/music/` + encoded name) rather than adding a new backend field
- Album recommendation artist count scales with limit: `(limit + 3) / 2` = 7 for limit=12, × 2 albums = 14 fetched, take 12

## Test Status
- [x] Build passes (0 warnings, 0 errors)
- [ ] Manual testing in Jellyfin UI

## Ready to Resume
To continue this work:
1. Build and deploy the plugin to Jellyfin
2. Verify tiles match native 'More Like This' size
3. Verify 12 recommendations load
4. Verify Last.fm links open correctly
5. Verify sidebar item appears in admin area
