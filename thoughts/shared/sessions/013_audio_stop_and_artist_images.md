---
date: 2026-02-19
feature: Audio Preview Stop + Artist Image Fix
plan: thoughts/shared/plans/002_tile_refactor_native_style_preview.md
status: complete
last_commit: f5351d6
---

# Session 013: Audio Preview Stop on Homepage Nav + Artist Image Fix

## Objectives
- Fix audio preview continuing to play when navigating to the homepage
- Fix artist recommendation images never loading

## Accomplishments

### 1. Audio Preview Stops on Homepage Navigation
- **Root cause**: `stopPreview()` was only called inside `checkPage()` after passing two guard conditions (detail page exists + URL has item ID). Navigating to the homepage failed both guards, so `checkPage()` returned early without stopping audio.
- **Fix**: Added `stopPreview()` calls to each early-return path in `checkPage()`. This is safe because `stopPreview()` is idempotent.
- Navigating to other album/artist pages already worked because those pass the guards and hit the existing `stopPreview()` at line 64.

### 2. Artist Images via Top Album Fallback
- **Root cause**: Last.fm deprecated artist images from their API (~2020). The `artist.getSimilar` response returns empty image URLs, so `GetBestImage()` always returned null for artists. Albums/tracks were unaffected since they use album art which Last.fm still serves.
- **Fix**: During artist enrichment, if `ImageUrl` is null, fetch the artist's top album via `GetArtistTopAlbumsAsync(name, 1)` and use that album's cover art.
- Changed enrichment to run for all artist recommendations (was previously limited to first 5), since every artist needs the image fallback.
- Results are cached by the Last.fm client, so no redundant API calls on revisits.

## File Changes
- `Jellyfin.Plugin.MusicDiscovery/Web/discoveryPanel.js` — Added `stopPreview()` to 3 early-return paths in `checkPage()`
- `Jellyfin.Plugin.MusicDiscovery/Api/MusicDiscoveryController.cs` — Artist enrichment: fetch top album cover as image fallback, enrich all recs not just first 5

## Decisions Made
- Used top album cover art as artist image proxy rather than adding a new API dependency (MusicBrainz, Spotify, etc.)
- Enrichment now runs for all artist recommendations since image fallback is needed for every result

## Test Status
- [x] Build passes (0 warnings, 0 errors)
- [ ] Manual testing in Jellyfin UI

## Ready to Resume
To continue this work:
1. Build and deploy the plugin to Jellyfin
2. Verify audio stops when navigating to homepage
3. Verify artist recommendation tiles now show album cover images
4. Verify no performance regression from enriching all artist recs (vs. first 5)
