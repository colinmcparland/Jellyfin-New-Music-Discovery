---
date: 2026-02-20
feature: Saved Recommendations — Native Overlay & Spacing Improvements
plan: thoughts/shared/plans/003_saved_recommendations.md
status: in_progress
last_commit: 8f20c53
---

# Session 024: Native Overlay & Spacing Improvements

## Objectives
- Move bookmark button to bottom-right of tiles (matching native favorite button position)
- Use same bookmark button on "All Recommendations" page instead of separate delete button
- Match card spacing in sliders and grid to native Jellyfin emby sliders

## Accomplishments

### Restructured card overlay to match native Jellyfin pattern
- **Before**: Custom `md-play-overlay` (full-card overlay div), separate `md-bookmark-btn` (top-right absolute), and `md-artist-img-link` (artist-only full-card link)
- **After**: Single `cardOverlayContainer` div (native Jellyfin class) containing:
  - Play button as `cardOverlayFab-primary` (centered, albums/tracks only) — uses native Jellyfin overlay button classes
  - Bookmark button inside `cardOverlayButton-br flex` container (bottom-right) — matches native favorite button position
- Artist cards: overlay with just the bottom-right bookmark (no play button), card name text links to Last.fm
- Removed `createPlayButton()` function, updated `handlePlayClick()` and `stopPreview()` for new element structure
- **File changed**: `Web/discoveryPanel.js`

### Unified bookmark button on All Recommendations page
- **Before**: Red `md-delete-btn` with `close` (X) icon in top-right corner
- **After**: Same `cardOverlayContainer` + `cardOverlayButton-br` + `md-bookmark-btn` structure as recommendation tiles, showing filled `bookmark` icon with `md-saved` class. Clicking deletes the save and removes the card.
- **File changed**: `Web/savedRecommendationsPage.js`

### CSS cleanup and spacing fixes
- Removed all custom overlay styles: `md-play-overlay`, `md-play-btn`, `md-artist-img-link`, `md-bookmark-btn` absolute positioning, `md-delete-btn`
- Bookmark now uses native `cardOverlayButton` classes for styling; `md-bookmark-btn` class only controls saved-state visibility (`md-saved { opacity: 1 }`)
- Changed `.md-saved-grid` from CSS grid to flexbox so `overflowSquareCard` native widths control tile sizing/spacing
- Added `.cardOverlayContainer.md-playing { opacity: 1 }` to keep overlay visible during audio playback
- **File changed**: `Web/discoveryPanel.css`

## Decisions Made
- Used native `cardOverlayContainer`, `cardOverlayFab-primary`, and `cardOverlayButton-br` classes to get Jellyfin's built-in overlay hover behavior and button styling for free
- Kept `md-bookmark-btn` as an additional class (alongside native classes) for saved-state CSS targeting
- Removed artist full-card link overlay; artist names/titles link to Last.fm via text below the card
- All Recommendations page uses flexbox instead of CSS grid to inherit native card widths

## Open Questions / Needs Testing
- All items from sessions 019-023 still apply
- Verify overlay shows on hover with play button centered and bookmark bottom-right
- Verify bookmark toggle works (save/unsave) from detail page tiles
- Verify All Recommendations page shows filled bookmark that deletes on click
- Verify audio preview playback still works with new overlay structure
- Verify card spacing matches native Jellyfin sliders on both detail pages and homepage

## File Changes
```
 Jellyfin.Plugin.MusicDiscovery/Web/discoveryPanel.css             | -129 lines (removed custom overlay/button styles)
 Jellyfin.Plugin.MusicDiscovery/Web/discoveryPanel.js              | restructured createCard(), handlePlayClick(), stopPreview()
 Jellyfin.Plugin.MusicDiscovery/Web/savedRecommendationsPage.js    | replaced delete btn with bookmark btn
```

## Test Status
- [x] `dotnet build` passes with 0 warnings, 0 errors
- [ ] Manual testing in running Jellyfin instance
- [ ] Visual comparison with native Jellyfin sliders

## Ready to Resume
To continue this work:
1. Read this session: `thoughts/shared/sessions/024_native_overlay_and_spacing.md`
2. Check plan: `thoughts/shared/plans/003_saved_recommendations.md`
3. Deploy to a running Jellyfin 10.11 instance and test visually
4. Compare overlay behavior, button positions, and spacing with native sliders
5. Fix any visual or functional issues found
