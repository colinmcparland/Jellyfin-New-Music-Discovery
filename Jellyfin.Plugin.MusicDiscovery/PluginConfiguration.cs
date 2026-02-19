using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MusicDiscovery;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        LastFmApiKey = string.Empty;
        MaxRecommendations = 12;
        CacheDurationMinutes = 30;
        EnableForArtists = true;
        EnableForAlbums = true;
        EnableForTracks = true;
    }

    public string LastFmApiKey { get; set; }
    public int MaxRecommendations { get; set; }
    public int CacheDurationMinutes { get; set; }
    public bool EnableForArtists { get; set; }
    public bool EnableForAlbums { get; set; }
    public bool EnableForTracks { get; set; }
}
