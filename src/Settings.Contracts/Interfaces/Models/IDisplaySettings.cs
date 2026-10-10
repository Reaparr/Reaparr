namespace Reaparr.Settings.Contracts;

public interface IDisplaySettings
{
    ViewMode TvShowViewMode { get; set; }

    ViewMode MovieViewMode { get; set; }

    ViewMode MusicArtistViewMode { get; set; }

    ViewMode PhotoAlbumViewMode { get; set; }

    ViewMode OtherVideosViewMode { get; set; }

    PlexMediaType AllOverviewViewMode { get; set; }
}
