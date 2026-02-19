using System;

namespace Jellyfin.Plugin.MusicDiscovery.Api;

public static class ExternalLinkBuilder
{
    public static ExternalLinksDto BuildArtistLinks(string artistName, string? mbid, string? lastFmUrl)
    {
        var links = new ExternalLinksDto
        {
            LastFmUrl = lastFmUrl,
            DiscogsSearchUrl = $"https://www.discogs.com/search/?q={Uri.EscapeDataString(artistName)}&type=artist",
            BandcampSearchUrl = $"https://bandcamp.com/search?q={Uri.EscapeDataString(artistName)}&item_type=b"
        };

        if (!string.IsNullOrEmpty(mbid))
            links.MusicBrainzUrl = $"https://musicbrainz.org/artist/{mbid}";

        return links;
    }

    public static ExternalLinksDto BuildAlbumLinks(string artistName, string albumName, string? mbid, string? lastFmUrl)
    {
        var searchQuery = $"{artistName} {albumName}";
        var links = new ExternalLinksDto
        {
            LastFmUrl = lastFmUrl,
            DiscogsSearchUrl = $"https://www.discogs.com/search/?q={Uri.EscapeDataString(searchQuery)}&type=release",
            BandcampSearchUrl = $"https://bandcamp.com/search?q={Uri.EscapeDataString(searchQuery)}&item_type=a"
        };

        if (!string.IsNullOrEmpty(mbid))
            links.MusicBrainzUrl = $"https://musicbrainz.org/release/{mbid}";

        return links;
    }

    public static ExternalLinksDto BuildTrackLinks(string artistName, string trackName, string? mbid, string? lastFmUrl)
    {
        var searchQuery = $"{artistName} {trackName}";
        var links = new ExternalLinksDto
        {
            LastFmUrl = lastFmUrl,
            DiscogsSearchUrl = $"https://www.discogs.com/search/?q={Uri.EscapeDataString(searchQuery)}&type=all",
            BandcampSearchUrl = $"https://bandcamp.com/search?q={Uri.EscapeDataString(searchQuery)}&item_type=t"
        };

        if (!string.IsNullOrEmpty(mbid))
            links.MusicBrainzUrl = $"https://musicbrainz.org/recording/{mbid}";

        return links;
    }
}
