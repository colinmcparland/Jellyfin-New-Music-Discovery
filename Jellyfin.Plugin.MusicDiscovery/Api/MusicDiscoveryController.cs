using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MusicDiscovery.LastFm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MusicDiscovery.Api;

[ApiController]
[Route("MusicDiscovery")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize]
public class MusicDiscoveryController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly LastFmApiClient _lastFmClient;
    private readonly ILogger<MusicDiscoveryController> _logger;

    public MusicDiscoveryController(
        ILibraryManager libraryManager,
        LastFmApiClient lastFmClient,
        ILogger<MusicDiscoveryController> logger)
    {
        _libraryManager = libraryManager;
        _lastFmClient = lastFmClient;
        _logger = logger;
    }

    [HttpGet("Similar/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecommendationsResponse>> GetSimilar(
        [FromRoute] Guid itemId, CancellationToken ct)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || string.IsNullOrEmpty(config.LastFmApiKey))
            return BadRequest(new { Error = "Last.fm API key not configured" });

        var item = _libraryManager.GetItemById(itemId);
        if (item == null)
            return NotFound();

        var maxResults = config.MaxRecommendations;

        return item switch
        {
            MusicArtist artist when config.EnableForArtists =>
                Ok(await GetArtistRecommendations(artist.Name, maxResults, ct)),

            MusicAlbum album when config.EnableForAlbums =>
                Ok(await GetAlbumRecommendations(
                    album.AlbumArtists?.FirstOrDefault() ?? album.Artists?.FirstOrDefault() ?? "",
                    album.Name, maxResults, ct)),

            Audio track when config.EnableForTracks =>
                Ok(await GetTrackRecommendations(
                    track.Artists?.FirstOrDefault() ?? "",
                    track.Name, maxResults, ct)),

            _ => Ok(new RecommendationsResponse
            {
                SourceName = item.Name,
                SourceType = item.GetType().Name,
                Recommendations = new()
            })
        };
    }

    private async Task<RecommendationsResponse> GetArtistRecommendations(
        string artistName, int limit, CancellationToken ct)
    {
        // Over-fetch to account for filtering out artists already in the library
        var fetchLimit = Math.Min(limit * 3, 50);
        var similar = await _lastFmClient.GetSimilarArtistsAsync(artistName, fetchLimit, ct);

        // Filter out artists the user already has in their library
        var libraryArtists = GetLibraryArtistNames();
        var filtered = similar
            .Where(a => !libraryArtists.Contains(a.Name))
            .Take(limit)
            .ToList();

        var recommendations = filtered.Select(a => new RecommendationDto
        {
            Name = a.Name,
            ArtistName = a.Name,
            ImageUrl = GetBestImage(a.Images),
            MatchScore = double.TryParse(a.Match, out var m) ? m : 0,
            Tags = new List<string>(),
            Links = ExternalLinkBuilder.BuildArtistLinks(a.Name, NullIfEmpty(a.Mbid), a.Url),
            Type = "artist"
        }).ToList();

        // Enrich with tags and images.
        // Last.fm deprecated artist images entirely, so use iTunes as primary
        // image source, with top album art as fallback.
        var enrichTasks = recommendations.Select(async rec =>
        {
            var info = await _lastFmClient.GetArtistInfoAsync(rec.Name, ct);
            if (info?.Tags.Tags != null)
                rec.Tags = info.Tags.Tags.Select(t => t.Name).Take(3).ToList();

            if (string.IsNullOrEmpty(rec.ImageUrl))
            {
                rec.ImageUrl = await _lastFmClient.GetArtistImageFromITunesAsync(rec.Name, ct);
            }

            if (string.IsNullOrEmpty(rec.ImageUrl))
            {
                var topAlbums = await _lastFmClient.GetArtistTopAlbumsAsync(rec.Name, 1, ct);
                if (topAlbums.Count > 0)
                    rec.ImageUrl = GetBestImage(topAlbums[0].Images);
            }
        });
        await Task.WhenAll(enrichTasks);

        return new RecommendationsResponse
        {
            SourceName = artistName,
            SourceType = "artist",
            Recommendations = recommendations
        };
    }

    private async Task<RecommendationsResponse> GetAlbumRecommendations(
        string artistName, string albumName, int limit, CancellationToken ct)
    {
        // Strategy: get similar artists, then their top albums.
        // Over-fetch similar artists to account for library filtering.
        var artistCount = Math.Min(limit * 2, 30);
        var similarArtists = await _lastFmClient.GetSimilarArtistsAsync(artistName, artistCount, ct);

        var albumTasks = similarArtists.Select(async artist =>
        {
            var topAlbums = await _lastFmClient.GetArtistTopAlbumsAsync(artist.Name, 2, ct);
            return topAlbums.Select(album => new RecommendationDto
            {
                Name = album.Name,
                ArtistName = album.Artist.Name,
                ImageUrl = GetBestImage(album.Images),
                MatchScore = double.TryParse(artist.Match, out var m) ? m : 0,
                Tags = new List<string>(),
                Links = ExternalLinkBuilder.BuildAlbumLinks(
                    album.Artist.Name, album.Name, NullIfEmpty(album.Mbid), album.Url),
                Type = "album"
            });
        });

        var albumResults = await Task.WhenAll(albumTasks);

        // Filter out albums already in the library, then take the desired count
        var libraryAlbums = GetLibraryAlbumKeys();
        var recommendations = albumResults
            .SelectMany(x => x)
            .Where(rec => !libraryAlbums.Contains(
                $"{rec.ArtistName}\0{rec.Name}".ToLowerInvariant()))
            .OrderByDescending(x => x.MatchScore)
            .Take(limit)
            .ToList();

        // Enrich with tags
        var enrichTasks = recommendations.Take(5).Select(async rec =>
        {
            var info = await _lastFmClient.GetAlbumInfoAsync(rec.ArtistName, rec.Name, ct);
            if (info?.Tags.Tags != null)
                rec.Tags = info.Tags.Tags.Select(t => t.Name).Take(3).ToList();
        });
        await Task.WhenAll(enrichTasks);

        return new RecommendationsResponse
        {
            SourceName = albumName,
            SourceType = "album",
            Recommendations = recommendations
        };
    }

    private async Task<RecommendationsResponse> GetTrackRecommendations(
        string artistName, string trackName, int limit, CancellationToken ct)
    {
        // Over-fetch to account for filtering out tracks already in the library
        var fetchLimit = Math.Min(limit * 3, 50);
        var similar = await _lastFmClient.GetSimilarTracksAsync(artistName, trackName, fetchLimit, ct);

        // Filter out tracks already in the library
        var libraryTracks = GetLibraryTrackKeys();
        var filtered = similar
            .Where(t => !libraryTracks.Contains(
                $"{t.Artist.Name}\0{t.Name}".ToLowerInvariant()))
            .Take(limit)
            .ToList();

        var recommendations = filtered.Select(t => new RecommendationDto
        {
            Name = t.Name,
            ArtistName = t.Artist.Name,
            ImageUrl = GetBestImage(t.Images),
            MatchScore = t.MatchScore,
            Tags = new List<string>(),
            Links = ExternalLinkBuilder.BuildTrackLinks(
                t.Artist.Name, t.Name, NullIfEmpty(t.Mbid), t.Url),
            Type = "track"
        }).ToList();

        return new RecommendationsResponse
        {
            SourceName = trackName,
            SourceType = "track",
            Recommendations = recommendations
        };
    }

    // --- Library lookup helpers ---
    // These query Jellyfin's in-memory database (fast) and build HashSets
    // for O(1) lookup when filtering recommendations.

    private HashSet<string> GetLibraryArtistNames()
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
            Recursive = true
        };
        return _libraryManager.GetItemList(query)
            .Select(a => a.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private HashSet<string> GetLibraryAlbumKeys()
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
            Recursive = true
        };
        return _libraryManager.GetItemList(query)
            .OfType<MusicAlbum>()
            .Select(a => $"{a.AlbumArtists?.FirstOrDefault() ?? ""}\0{a.Name}".ToLowerInvariant())
            .ToHashSet();
    }

    private HashSet<string> GetLibraryTrackKeys()
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            Recursive = true
        };
        return _libraryManager.GetItemList(query)
            .OfType<Audio>()
            .Select(t => $"{t.Artists?.FirstOrDefault() ?? ""}\0{t.Name}".ToLowerInvariant())
            .ToHashSet();
    }

    private static string? GetBestImage(List<LastFm.Models.LastFmImage>? images)
    {
        if (images == null || images.Count == 0) return null;

        // Prefer extralarge > large > medium, skip empty URLs and Last.fm placeholder
        var preferred = new[] { "extralarge", "large", "medium", "mega" };
        foreach (var size in preferred)
        {
            var img = images.FirstOrDefault(i =>
                i.Size == size && IsValidImageUrl(i.Url));
            if (img != null) return img.Url;
        }

        return images.FirstOrDefault(i => IsValidImageUrl(i.Url))?.Url;
    }

    private static bool IsValidImageUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;

        // Last.fm deprecated artist images but still returns a default star/placeholder.
        // The placeholder hash is consistent across all sizes.
        return !url.Contains("2a96cbd8b46e442fc41c2b86b821562f", StringComparison.Ordinal);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
