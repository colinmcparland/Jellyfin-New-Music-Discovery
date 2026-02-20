# Saved Recommendations Implementation Plan

## Overview
Add the ability for users to save music recommendations for later reference. This includes save/delete buttons on recommendation tiles, a "Recent Saved" section on the homepage, and a dedicated "View All" plugin page for managing all saved recommendations. Each user's saved recommendations are private and stored per-user in JSON files.

## Current State Analysis

**Backend:**
- `MusicDiscoveryController` serves recommendations via `GET /MusicDiscovery/Similar/{itemId}` — no persistence
- No per-user data storage exists; all data is transient (Last.fm API cache, Jellyfin library queries)
- The controller uses `[Authorize]` but never extracts the authenticated user's ID
- Plugin configuration is global, not per-user

**Frontend (`Web/discoveryPanel.js`):**
- Recommendation tiles render inside an `emby-itemscontainer` element, which provides Jellyfin's native horizontal scrolling but also enables the **long-press multi-select** behavior (blue highlight + bulk action bar)
- Tiles have a play button overlay (albums/tracks) or artist link overlay — no save/bookmark UI
- The script only activates on **detail pages** (`.itemDetailPage`) — it has no homepage presence
- No mechanism to fetch or display per-user saved data

**Plugin pages:**
- Only one registered page: `MusicDiscoveryConfig` (configuration/settings)
- Pages are registered via `Plugin.GetPages()` and served as embedded resources

## Desired End State

1. **Save button on tiles**: Each recommendation tile has a small bookmark icon in the top-right corner. Unsaved tiles show `bookmark_border`; saved tiles show `bookmark` (filled). Clicking toggles the saved state.
2. **No multi-select**: The long-press bulk-selection behavior of `emby-itemscontainer` is disabled for recommendation tiles.
3. **Homepage section**: The Jellyfin homepage shows a "Saved Recommendations" horizontal scroller with the user's most recent saves (up to 12). A "View All" link in the section header navigates to the full page.
4. **View All page**: A dedicated plugin page lists all saved recommendations in a grid/card layout with delete buttons.
5. **Per-user isolation**: Users can only see and manage their own saved recommendations.
6. **Persistent storage**: Saved recommendations survive server restarts (JSON files in plugin data directory).

### Verification
- Save a recommendation from an album detail page → bookmark icon fills in
- Navigate to homepage → saved recommendation appears in "Saved Recommendations" section
- Click "View All" → navigates to full page showing all saved recommendations
- Delete from "View All" page → recommendation removed; homepage section updates on next visit
- Log in as a different user → no saved recommendations from other users are visible
- Restart Jellyfin server → saved recommendations persist

## What We're NOT Doing

- No sorting/filtering/search on the "View All" page (simple chronological list)
- No bulk delete operations (one at a time only)
- No export/import of saved recommendations
- No notifications or reminders about saved recommendations
- No sharing of saved recommendations between users
- No changes to the recommendation generation logic (Last.fm sourcing stays the same)
- No admin visibility into other users' saved recommendations

## Implementation Approach

**Storage**: JSON files per user in `{DataFolderPath}/saved-recommendations/{userId}.json`. This is the standard Jellyfin plugin pattern — simple, no external dependencies, survives upgrades. A singleton `SavedRecommendationStore` handles thread-safe reads/writes with `SemaphoreSlim`.

**User identification**: Inject `IAuthorizationContext` (from `MediaBrowser.Controller.Net`) into the controller to extract `auth.UserId` from the HTTP request.

**Disabling multi-select**: Replace `emby-itemscontainer` with a plain `div` for the recommendation slider. The `emby-itemscontainer` custom element is what provides the long-press selection behavior. Using a plain `div` with the same CSS classes (`scrollSlider focuscontainer-x`) preserves the visual layout while eliminating the unwanted selection behavior. The parent `emby-scroller` still handles horizontal scroll mechanics.

**Homepage injection**: Extend the existing MutationObserver to also detect the homepage (`.homePage:not(.hide)` or Jellyfin's home section container). When detected, fetch saved recommendations from the API and inject a scroller section.

**View All page**: Register a new plugin HTML page (`SavedRecommendationsPage`) with its own JS controller. Uses Jellyfin's native `emby-button` and card styling. Fetches all saved recommendations from the API and renders them with delete buttons.

---

## Phase 1: Backend — Storage Service and API Endpoints

### Overview
Create per-user persistent storage and REST API endpoints for saving, deleting, and retrieving recommendations.

### Changes Required:

#### 1. New file: `Data/SavedRecommendation.cs`
**Purpose**: Data model for saved recommendations.

```csharp
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MusicDiscovery.Data;

public class SavedRecommendation
{
    public string Name { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double MatchScore { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public string? LastFmUrl { get; set; }
    public DateTime SavedAt { get; set; }
}

public class UserSavedRecommendations
{
    public List<SavedRecommendation> Items { get; set; } = new();
}
```

#### 2. New file: `Data/SavedRecommendationStore.cs`
**Purpose**: Thread-safe file-based persistence, one JSON file per user.

```csharp
using System.Text.Json;

namespace Jellyfin.Plugin.MusicDiscovery.Data;

public class SavedRecommendationStore
{
    private readonly string _basePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SavedRecommendationStore()
    {
        _basePath = Path.Combine(Plugin.Instance!.DataFolderPath, "saved-recommendations");
        Directory.CreateDirectory(_basePath);
    }

    private string GetPath(Guid userId) => Path.Combine(_basePath, $"{userId}.json");

    public async Task<UserSavedRecommendations> LoadAsync(Guid userId)
    {
        var path = GetPath(userId);
        if (!File.Exists(path)) return new UserSavedRecommendations();

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<UserSavedRecommendations>(json, _jsonOptions)
                   ?? new UserSavedRecommendations();
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(Guid userId, UserSavedRecommendations data)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(GetPath(userId), json).ConfigureAwait(false);
        }
        finally { _lock.Release(); }
    }
}
```

#### 3. Modify: `ServiceRegistrator.cs`
**Changes**: Register `SavedRecommendationStore` as a singleton.

```csharp
// Add to RegisterServices():
serviceCollection.AddSingleton<Data.SavedRecommendationStore>();
```

#### 4. New file: `Api/SavedRecommendationDto.cs`
**Purpose**: Request/response DTOs for the saved recommendations API.

```csharp
namespace Jellyfin.Plugin.MusicDiscovery.Api;

public class SaveRecommendationRequest
{
    public string Name { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double MatchScore { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public string? LastFmUrl { get; set; }
}

public class DeleteRecommendationRequest
{
    public string Name { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
```

#### 5. New file: `Api/SavedRecommendationsController.cs`
**Purpose**: REST endpoints for save/delete/get operations.

```csharp
using MediaBrowser.Controller.Net;

namespace Jellyfin.Plugin.MusicDiscovery.Api;

[ApiController]
[Route("MusicDiscovery/Saved")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize]
public class SavedRecommendationsController : ControllerBase
{
    private readonly Data.SavedRecommendationStore _store;
    private readonly IAuthorizationContext _authContext;

    // Constructor injects store + authContext

    [HttpGet]
    // Returns all saved recommendations for the current user
    // Query param: ?limit=N for recent (homepage use)

    [HttpPost]
    // Saves a recommendation for the current user
    // Body: SaveRecommendationRequest
    // Deduplicates by (Name, ArtistName, Type) composite key

    [HttpDelete]
    // Deletes a saved recommendation for the current user
    // Body: DeleteRecommendationRequest
    // Matches by (Name, ArtistName, Type) composite key

    [HttpGet("Check")]
    // Checks which recommendations (from a list) are already saved
    // Query params: names=a,b&artists=x,y&types=t1,t2
    // Returns list of matching (Name, ArtistName, Type) tuples
    // Used by detail page to set initial bookmark state on tiles
}
```

**Endpoint details:**

| Method | Route | Purpose | Response |
|--------|-------|---------|----------|
| `GET` | `/MusicDiscovery/Saved?limit=12` | Get saved (most recent first), optional limit | `UserSavedRecommendations` |
| `POST` | `/MusicDiscovery/Saved` | Save a recommendation | `200 OK` |
| `DELETE` | `/MusicDiscovery/Saved` | Delete a saved recommendation | `200 OK` / `404` |
| `GET` | `/MusicDiscovery/Saved/Check` | Check if recommendations are already saved | `List<{Name, ArtistName, Type}>` |

### Success Criteria:

#### Automated:
- [x] Project compiles with `dotnet build`
- [x] No new warnings

#### Manual:
- [ ] `POST /MusicDiscovery/Saved` with a recommendation body → 200, file created in `saved-recommendations/`
- [ ] `GET /MusicDiscovery/Saved` returns the saved item
- [ ] `DELETE /MusicDiscovery/Saved` removes the item
- [ ] `GET /MusicDiscovery/Saved/Check` correctly identifies saved items
- [ ] Different users see only their own saves (test with two Jellyfin accounts)
- [ ] Data persists after Jellyfin restart

---

## Phase 2: Frontend — Save/Delete Buttons on Recommendation Tiles

### Overview
Add a bookmark icon overlay to each recommendation tile on detail pages. Disable the `emby-itemscontainer` multi-select behavior. Wire up save/delete to the new API.

### Changes Required:

#### 1. `Web/discoveryPanel.js` — Disable multi-select

In `renderPanel()`, change the slider element from `emby-itemscontainer` to a plain `div`:

```javascript
// BEFORE (line 166-168):
var slider = document.createElement('div', 'emby-itemscontainer');
slider.setAttribute('is', 'emby-itemscontainer');
slider.className = 'scrollSlider focuscontainer-x itemsContainer animatedScrollX';

// AFTER:
var slider = document.createElement('div');
slider.className = 'scrollSlider focuscontainer-x itemsContainer animatedScrollX';
```

This removes the custom element upgrade that provides multi-select behavior while keeping the CSS classes for layout.

#### 2. `Web/discoveryPanel.js` — Add bookmark overlay to `createCard()`

After the card is built, add a bookmark icon in the top-right corner:

```javascript
// Inside createCard(), after building cardScalable:
var bookmarkBtn = document.createElement('button');
bookmarkBtn.className = 'md-bookmark-btn';
bookmarkBtn.setAttribute('aria-label', 'Save recommendation');
var bookmarkIcon = document.createElement('span');
bookmarkIcon.className = 'material-icons';
bookmarkIcon.textContent = 'bookmark_border'; // unfilled by default
bookmarkBtn.appendChild(bookmarkIcon);

bookmarkBtn.addEventListener('click', function (e) {
    e.preventDefault();
    e.stopPropagation();
    handleBookmarkClick(bookmarkBtn, rec);
});

cardScalable.appendChild(bookmarkBtn);
```

#### 3. `Web/discoveryPanel.js` — Add `handleBookmarkClick()` function

```javascript
function handleBookmarkClick(btn, rec) {
    var icon = btn.querySelector('.material-icons');
    var isSaved = icon.textContent === 'bookmark';

    if (isSaved) {
        // Delete
        var url = ApiClient.getUrl('MusicDiscovery/Saved');
        ApiClient.ajax({
            type: 'DELETE', url: url,
            contentType: 'application/json',
            data: JSON.stringify({
                Name: rec.Name, ArtistName: rec.ArtistName, Type: rec.Type
            })
        }).then(function () {
            icon.textContent = 'bookmark_border';
            btn.setAttribute('aria-label', 'Save recommendation');
        });
    } else {
        // Save
        var url = ApiClient.getUrl('MusicDiscovery/Saved');
        ApiClient.ajax({
            type: 'POST', url: url,
            contentType: 'application/json',
            data: JSON.stringify({
                Name: rec.Name, ArtistName: rec.ArtistName,
                ImageUrl: rec.ImageUrl, MatchScore: rec.MatchScore,
                Tags: rec.Tags, Type: rec.Type,
                LastFmUrl: (rec.Links && rec.Links.LastFmUrl) || null
            })
        }).then(function () {
            icon.textContent = 'bookmark';
            btn.setAttribute('aria-label', 'Remove saved recommendation');
        });
    }
}
```

#### 4. `Web/discoveryPanel.js` — Check saved state on panel load

In `renderPanel()`, after rendering all cards, call the Check endpoint to set initial bookmark states:

```javascript
// After slider is populated and appended:
checkSavedState(data.Recommendations, slider);

function checkSavedState(recommendations, container) {
    var names = recommendations.map(function(r) { return r.Name; });
    var artists = recommendations.map(function(r) { return r.ArtistName; });
    var types = recommendations.map(function(r) { return r.Type; });

    var url = ApiClient.getUrl('MusicDiscovery/Saved/Check')
        + '?names=' + encodeURIComponent(names.join(','))
        + '&artists=' + encodeURIComponent(artists.join(','))
        + '&types=' + encodeURIComponent(types.join(','));

    ApiClient.getJSON(url).then(function (savedList) {
        // Build a Set of saved keys for O(1) lookup
        var savedKeys = {};
        savedList.forEach(function (s) {
            savedKeys[s.Name + '\0' + s.ArtistName + '\0' + s.Type] = true;
        });

        // Update bookmark icons in the rendered cards
        var bookmarkBtns = container.querySelectorAll('.md-bookmark-btn');
        recommendations.forEach(function (rec, i) {
            var key = rec.Name + '\0' + rec.ArtistName + '\0' + rec.Type;
            if (savedKeys[key] && bookmarkBtns[i]) {
                var icon = bookmarkBtns[i].querySelector('.material-icons');
                icon.textContent = 'bookmark';
                bookmarkBtns[i].setAttribute('aria-label', 'Remove saved recommendation');
            }
        });
    });
}
```

#### 5. `Web/discoveryPanel.css` — Add bookmark button styles

```css
/* Bookmark button — top-right corner of tile */
.md-bookmark-btn {
    position: absolute;
    top: 0.3em;
    right: 0.3em;
    z-index: 2;
    background: rgba(0, 0, 0, 0.5);
    border: none;
    border-radius: 50%;
    width: 2em;
    height: 2em;
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    cursor: pointer;
    opacity: 0;
    transition: opacity 0.2s ease, background 0.15s ease;
}

.cardScalable:hover .md-bookmark-btn {
    opacity: 1;
}

/* Always show filled bookmark (already saved) */
.md-bookmark-btn .material-icons[textContent="bookmark"] {
    /* CSS can't select on textContent, so we use a class instead */
}
.md-bookmark-btn.md-saved {
    opacity: 0.8;
}

.md-bookmark-btn:hover {
    background: rgba(0, 0, 0, 0.7);
}

.md-bookmark-btn .material-icons {
    font-size: 1.2em;
}
```

Note: We'll also add/remove a `md-saved` class when toggling so that saved bookmarks are always visible (not just on hover). This will be set alongside `icon.textContent = 'bookmark'`.

### Success Criteria:

#### Manual:
- [ ] Long-pressing a recommendation tile does NOT trigger blue multi-select
- [ ] Hovering a tile shows a bookmark icon in the top-right corner
- [ ] Clicking the bookmark icon fills it in and saves the recommendation (network tab confirms POST)
- [ ] Clicking a filled bookmark icon unfills it and deletes the save (network tab confirms DELETE)
- [ ] Navigating away and back to the same detail page shows correct saved state
- [ ] Bookmark button does not overlap or interfere with the play button overlay
- [ ] Artist tiles (which have no play overlay) also get the bookmark button

---

## Phase 3: Frontend — Homepage "Saved Recommendations" Section

### Overview
Inject a new horizontal scroller section on the Jellyfin homepage showing the user's most recently saved recommendations, with a "View All" link.

### Changes Required:

#### 1. `Web/discoveryPanel.js` — Extend MutationObserver to detect homepage

Add a new function `checkHomePage()` called from the observer alongside `checkPage()`:

```javascript
function checkHomePage() {
    // Detect Jellyfin homepage — the section container visible when on the home route
    var homePage = document.querySelector('.homePage:not(.hide)')
                || document.querySelector('#homeTab:not(.hide)');
    if (!homePage) return;

    // Don't re-inject if section already present
    if (homePage.querySelector('.md-saved-section')) return;

    // Fetch recent saved recommendations
    var url = ApiClient.getUrl('MusicDiscovery/Saved') + '?limit=12';
    ApiClient.getJSON(url).then(function (data) {
        if (!data || !data.Items || data.Items.length === 0) return;
        renderHomepageSection(data.Items, homePage);
    });
}
```

#### 2. `Web/discoveryPanel.js` — Add `renderHomepageSection()`

Renders a horizontal scroller section with a "View All" header link. Uses the same card structure as detail page tiles but with delete functionality (bookmark shows filled, clicking deletes).

```javascript
function renderHomepageSection(items, container) {
    var section = document.createElement('div');
    section.className = 'verticalSection md-saved-section';

    // Header with "View All" link
    var headerContainer = document.createElement('div');
    var header = document.createElement('h2');
    header.className = 'sectionTitle sectionTitle-cards';
    header.textContent = 'Saved Recommendations';

    var viewAllLink = document.createElement('a');
    viewAllLink.className = 'textActionButton';
    viewAllLink.href = '#/configurationpage?name=SavedRecommendationsPage';
    viewAllLink.textContent = 'View All >';
    viewAllLink.style.marginLeft = '1em';
    viewAllLink.style.fontSize = '0.8em';

    headerContainer.appendChild(header);
    headerContainer.appendChild(viewAllLink);
    section.appendChild(headerContainer);

    // Scroller — same pattern as detail page
    var scroller = document.createElement('div', 'emby-scroller');
    scroller.setAttribute('is', 'emby-scroller');
    // ... (same scroller setup as renderPanel)

    var slider = document.createElement('div');
    slider.className = 'scrollSlider focuscontainer-x itemsContainer animatedScrollX';
    slider.style.whiteSpace = 'nowrap';

    items.forEach(function (item) {
        // Adapt SavedRecommendation to the card format
        var rec = {
            Name: item.Name, ArtistName: item.ArtistName,
            ImageUrl: item.ImageUrl, Type: item.Type,
            Tags: item.Tags,
            Links: { LastFmUrl: item.LastFmUrl }
        };
        var card = createCard(rec);
        // Mark bookmark as saved
        var btn = card.querySelector('.md-bookmark-btn');
        if (btn) {
            btn.querySelector('.material-icons').textContent = 'bookmark';
            btn.classList.add('md-saved');
        }
        slider.appendChild(card);
    });

    scroller.appendChild(slider);
    section.appendChild(scroller);

    // Insert near the top of the homepage content
    var firstSection = container.querySelector('.verticalSection');
    if (firstSection) {
        container.insertBefore(section, firstSection);
    } else {
        container.appendChild(section);
    }
}
```

#### 3. `Web/discoveryPanel.js` — Wire up observer

In the debounced observer callback, call both checks:

```javascript
var observer = new MutationObserver(function () {
    if (_injecting) return;
    if (_debounceTimer) clearTimeout(_debounceTimer);
    _debounceTimer = setTimeout(function () {
        checkPage();
        checkHomePage();
    }, 200);
});
```

### Success Criteria:

#### Manual:
- [ ] Navigate to the Jellyfin homepage → "Saved Recommendations" section appears with saved items
- [ ] Section does NOT appear if the user has no saved recommendations
- [ ] Tiles display correctly with images, names, and filled bookmark icons
- [ ] Clicking a bookmark icon on the homepage deletes the recommendation and removes the card
- [ ] "View All >" link navigates to the saved recommendations page
- [ ] Navigating away from the homepage and back does not create duplicate sections
- [ ] Section appears near the top of the homepage content

---

## Phase 4: "View All Saved Recommendations" Plugin Page

### Overview
Register a new plugin page that displays all saved recommendations in a card grid with delete functionality.

### Changes Required:

#### 1. `Plugin.cs` — Register new page

Add two new `PluginPageInfo` entries in `GetPages()`:

```csharp
new PluginPageInfo
{
    Name = "SavedRecommendationsPage",
    EmbeddedResourcePath = ns + ".Web.savedRecommendationsPage.html"
},
new PluginPageInfo
{
    Name = "SavedRecommendationsPage.js",
    EmbeddedResourcePath = ns + ".Web.savedRecommendationsPage.js"
}
```

#### 2. New file: `Web/savedRecommendationsPage.html`
**Purpose**: HTML template for the saved recommendations page.

Uses Jellyfin's standard page structure with `div class="content-primary"` containing:
- Page title: "Saved Recommendations"
- Empty state message (shown when no recommendations are saved)
- A container div for dynamically rendered recommendation cards
- Cards use the same native Jellyfin card classes as detail page tiles
- Each card has a visible delete button (not hidden behind hover)

#### 3. New file: `Web/savedRecommendationsPage.js`
**Purpose**: JavaScript controller for the saved recommendations page.

```javascript
// On page load:
// 1. Fetch GET /MusicDiscovery/Saved (no limit — get all)
// 2. Render each recommendation as a card with a delete button
// 3. If empty, show "No saved recommendations yet" message
// 4. Delete button click → DELETE /MusicDiscovery/Saved → remove card from DOM
// 5. If last card deleted, show empty state message
```

The page uses a responsive CSS grid layout (similar to Jellyfin's library grid view) rather than a horizontal scroller, since this is a full page view where vertical scrolling is natural.

### Success Criteria:

#### Manual:
- [ ] Navigate to `#/configurationpage?name=SavedRecommendationsPage` → page loads
- [ ] All saved recommendations are displayed as cards in a grid
- [ ] Each card has a visible delete button
- [ ] Clicking delete removes the card and calls the API
- [ ] Empty state message appears when all recommendations are deleted
- [ ] Page is accessible via the "View All" link on the homepage

---

## Testing Strategy

### Manual Testing Steps:
1. **Save from artist detail page** — bookmark icon appears, click to save, verify filled state
2. **Save from album detail page** — same as above
3. **Save from track detail page** — same as above
4. **Navigate to homepage** — "Saved Recommendations" section shows recent saves
5. **Click "View All"** — navigates to full page with all saves
6. **Delete from "View All" page** — card removed, verify via API
7. **Return to detail page** — bookmark should now show `bookmark_border` (unfilled)
8. **Return to homepage** — deleted recommendation no longer appears
9. **Test with second user account** — verify isolation (no cross-user visibility)
10. **Restart Jellyfin server** — verify saved data persists
11. **Long-press recommendation tile** — verify NO multi-select behavior
12. **Rapid save/delete** — verify no race conditions or duplicate entries

### Edge Cases:
- Saving the same recommendation twice (should deduplicate, not create duplicates)
- Saving when the data directory doesn't exist yet (auto-create)
- Very long recommendation names — text truncation on cards
- No image URL — fallback icon still works on saved recommendation cards
- User with no Last.fm API key configured — save/delete endpoints should still work (they don't depend on Last.fm)
- Large number of saved recommendations (100+) — "View All" page should handle without performance issues

## Performance Considerations

- JSON file reads are fast for typical saved list sizes (< 100 items per user)
- The `Check` endpoint is called once per detail page load — sends recommendation names in query params rather than making N individual requests
- Homepage section fetches with `?limit=12` to avoid loading all saved recommendations
- `SemaphoreSlim` ensures thread-safe file access without locking issues
- No database overhead — pure file I/O with OS-level caching

## Migration Notes

- No data migration needed — this is a new feature with no prior state
- The `emby-itemscontainer` → plain `div` change for the recommendation slider is backward-compatible (CSS classes are preserved)
- New embedded resources (HTML/JS for saved page) require the plugin to be rebuilt and reloaded
- New API endpoints are additive — no breaking changes to existing endpoints
