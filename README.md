# Jellyfin Music Discovery Plugin

A Jellyfin plugin that adds a "Similar Music" discovery panel to artist, album, and track detail pages. Recommendations come from the Last.fm API with links to MusicBrainz, Discogs, Bandcamp, and Last.fm.

## Installation

1. Build the plugin: `dotnet build -c Release`
2. Copy `Jellyfin.Plugin.MusicDiscovery.dll` and `meta.json` from `bin/Release/net8.0/` to your Jellyfin plugins directory:
   - Linux: `~/.local/share/jellyfin/plugins/MusicDiscovery/`
   - Docker: `/config/plugins/MusicDiscovery/`
   - Windows: `%LOCALAPPDATA%\jellyfin\plugins\MusicDiscovery\`
3. Restart Jellyfin server

## Configuration

1. Navigate to **Dashboard > Plugins > Music Discovery**
2. Enter your Last.fm API key (get one free at [last.fm/api](https://www.last.fm/api/account/create))
3. Adjust max recommendations (5, 8, or 10)
4. Set cache duration (default 30 minutes)
5. Enable/disable the panel for artists, albums, and/or tracks
6. Click Save

## Usage

Browse your music library and navigate to any artist, album, or track page. A "Similar Music" panel will appear below the existing content showing recommendation cards with:

- Cover art (or placeholder icon)
- Name and artist
- Genre tags
- Hover overlay with links to Last.fm, MusicBrainz, Discogs, and Bandcamp

## Known Limitations

- **Script loading**: The discovery panel script loads after visiting the plugin settings page once per browser session. This is a limitation of the Jellyfin plugin architecture.
- **Web client only**: Mobile apps are not supported.
- **No library filtering**: Recommendations may include music already in your library.
- **Last.fm coverage**: Recommendation quality depends on Last.fm's database coverage for your music.

## Supported Versions

- Jellyfin 10.9.x / 10.10.x
- .NET 8.0

## License

This project is provided as-is for personal use with Jellyfin.
