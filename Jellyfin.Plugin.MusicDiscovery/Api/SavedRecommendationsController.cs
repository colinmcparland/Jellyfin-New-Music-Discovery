using System.Net.Mime;
using Jellyfin.Plugin.MusicDiscovery.Data;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MusicDiscovery.Api;

[ApiController]
[Route("MusicDiscovery/Saved")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize]
public class SavedRecommendationsController : ControllerBase
{
    private readonly SavedRecommendationStore _store;
    private readonly IAuthorizationContext _authContext;

    public SavedRecommendationsController(
        SavedRecommendationStore store,
        IAuthorizationContext authContext)
    {
        _store = store;
        _authContext = authContext;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSavedRecommendations>> GetSaved([FromQuery] int? limit)
    {
        var userId = await GetUserIdAsync().ConfigureAwait(false);
        var data = await _store.LoadAsync(userId).ConfigureAwait(false);

        // Return most recent first
        data.Items = data.Items.OrderByDescending(x => x.SavedAt).ToList();

        if (limit.HasValue && limit.Value > 0)
        {
            data.Items = data.Items.Take(limit.Value).ToList();
        }

        return Ok(data);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveRecommendation([FromBody] SaveRecommendationRequest request)
    {
        var userId = await GetUserIdAsync().ConfigureAwait(false);
        var data = await _store.LoadAsync(userId).ConfigureAwait(false);

        // Deduplicate by (Name, ArtistName, Type)
        var exists = data.Items.Any(x =>
            string.Equals(x.Name, request.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ArtistName, request.ArtistName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Type, request.Type, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            data.Items.Add(new SavedRecommendation
            {
                Name = request.Name,
                ArtistName = request.ArtistName,
                ImageUrl = request.ImageUrl,
                MatchScore = request.MatchScore,
                Tags = request.Tags,
                Type = request.Type,
                LastFmUrl = request.LastFmUrl,
                SavedAt = DateTime.UtcNow
            });
            await _store.SaveAsync(userId, data).ConfigureAwait(false);
        }

        return Ok();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteRecommendation([FromBody] DeleteRecommendationRequest request)
    {
        var userId = await GetUserIdAsync().ConfigureAwait(false);
        var data = await _store.LoadAsync(userId).ConfigureAwait(false);

        var item = data.Items.FirstOrDefault(x =>
            string.Equals(x.Name, request.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ArtistName, request.ArtistName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Type, request.Type, StringComparison.OrdinalIgnoreCase));

        if (item == null) return NotFound();

        data.Items.Remove(item);
        await _store.SaveAsync(userId, data).ConfigureAwait(false);
        return Ok();
    }

    [HttpPost("Check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SavedCheckResult>>> CheckSaved(
        [FromBody] CheckRecommendationsRequest request)
    {
        var userId = await GetUserIdAsync().ConfigureAwait(false);
        var data = await _store.LoadAsync(userId).ConfigureAwait(false);

        var results = new List<SavedCheckResult>();
        var count = Math.Min(request.Names.Count, Math.Min(request.Artists.Count, request.Types.Count));

        for (int i = 0; i < count; i++)
        {
            var n = request.Names[i];
            var a = request.Artists[i];
            var t = request.Types[i];

            var match = data.Items.Any(x =>
                string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ArtistName, a, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Type, t, StringComparison.OrdinalIgnoreCase));

            if (match)
            {
                results.Add(new SavedCheckResult { Name = n, ArtistName = a, Type = t });
            }
        }

        return Ok(results);
    }

    private async Task<Guid> GetUserIdAsync()
    {
        var auth = await _authContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);
        return auth.UserId;
    }
}
