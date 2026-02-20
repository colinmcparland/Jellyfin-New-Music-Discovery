---
date: 2026-02-20
feature: Saved Recommendations — Bug Fixes
plan: thoughts/shared/plans/003_saved_recommendations.md
status: in_progress
last_commit: b5c1ae3
---

# Session 020: Saved Recommendations Bug Fixes

## Objectives
- Resume saved recommendations work from session 019
- Fix bugs found during manual testing

## Accomplishments

### Bug Fix 1: Comma-in-values breaks Check endpoint
- **Problem**: The `GET /MusicDiscovery/Saved/Check` endpoint split `names`, `artists`, and `types` query params on `,`. Artist/album names containing commas (e.g., "Earth, Wind & Fire") would misalign the parallel arrays and produce incorrect results.
- **Fix**: Changed from `[HttpGet("Check")]` with `[FromQuery] string` params to `[HttpPost("Check")]` with `[FromBody] CheckRecommendationsRequest` containing `List<string>` properties. Updated the JS `checkSavedState()` function to use `ApiClient.ajax()` POST with a JSON body instead of `ApiClient.getJSON()` with comma-joined query params.
- **Files changed**: `SavedRecommendationDto.cs` (new DTO), `SavedRecommendationsController.cs` (endpoint signature), `discoveryPanel.js` (client call)

### Bug Fix 2: Homepage section insertBefore error
- **Problem**: `renderHomepageSection()` used `container.insertBefore(section, firstSection)` but `container.querySelector('.verticalSection')` returned a deeply nested descendant, not a direct child of `container`. This caused `NotFoundError: The node before which the new node is to be inserted is not a child of this node`.
- **Fix**: Changed to `firstSection.parentNode.insertBefore(section, firstSection)` so the section is inserted as a sibling of wherever the first `.verticalSection` actually lives in the DOM.
- **File changed**: `discoveryPanel.js` line 168

## Decisions Made
- POST with JSON body is more robust than GET with delimited query params for the Check endpoint — no delimiter escaping needed, handles any characters in names

## Open Questions / Needs Testing
- All items from session 019 still apply (see that session for full list)
- Verify the homepage section now renders correctly after the insertBefore fix
- Verify the Check endpoint correctly identifies saved items with the new POST body
- Continue manual testing of save/delete/homepage/View All flows

## File Changes
```
 Jellyfin.Plugin.MusicDiscovery/Api/SavedRecommendationDto.cs        | +7 (new CheckRecommendationsRequest DTO)
 Jellyfin.Plugin.MusicDiscovery/Api/SavedRecommendationsController.cs | changed (GET→POST, query→body)
 Jellyfin.Plugin.MusicDiscovery/Web/discoveryPanel.js                 | changed (POST ajax, parentNode.insertBefore)
```

## Test Status
- [x] `dotnet build` passes with 0 warnings, 0 errors
- [ ] Manual testing in running Jellyfin instance
- [ ] Multi-user isolation testing
- [ ] Persistence after restart testing

## Ready to Resume
To continue this work:
1. Read this session: `thoughts/shared/sessions/020_saved_recs_bugfixes.md`
2. Check plan: `thoughts/shared/plans/003_saved_recommendations.md`
3. Deploy to a running Jellyfin 10.11 instance and test manually
4. Fix any runtime issues found during testing
5. If all manual tests pass, finalize the feature
