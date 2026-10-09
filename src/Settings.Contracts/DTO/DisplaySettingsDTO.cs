namespace Reaparr.Settings.Contracts;

public class DisplaySettingsDTO : IDisplaySettings
{
    public required ViewMode TvShowViewMode { get; set; }

    public required ViewMode MovieViewMode { get; set; }

    public required ViewMode MusicArtistViewMode { get; set; }

    public required ViewMode PhotoAlbumViewMode { get; set; }

    public required ViewMode OtherVideosViewMode { get; set; }

    public required PlexMediaType AllOverviewViewMode { get; set; }
}
