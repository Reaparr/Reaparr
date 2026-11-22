namespace Reaparr.BaseTests;

public class PlexApiDataConfig : BaseConfig<PlexApiDataConfig>
{
    public Seed Seed { get; } = new(9999);

    public int MovieLibraryCount { get; set; }

    public int TvShowLibraryCount { get; set; }
    public int MusicLibraryCount { get; set; }

    public int PhotoLibraryCount { get; set; }

    public int OtherVideoLibraryCount { get; set; }

    public int MoviesPerLibraryCount { get; set; }

    public int TvShowsPerLibraryCount { get; set; }

    public int SeasonsPerTvShowCount { get; set; }

    public int EpisodesPerSeasonCount { get; set; }

    public int ArtistsPerLibraryCount { get; set; }

    public int AlbumsPerArtistCount { get; set; }

    public int TracksPerAlbumCount { get; set; }

    public int PhotoAlbumsPerLibraryCount { get; set; }

    public int PhotosPerAlbumCount { get; set; }

    public int PhotoClipsPerAlbumCount { get; set; }

    /// <summary>When enabled, the first generated file is a member of every generated album.</summary>
    public bool PhotoAlbumsOverlap { get; set; }

    public int OtherVideosPerLibraryCount { get; set; }

    public PlexMediaType FailMediaType { get; set; } = PlexMediaType.None;

    public PlexMediaType IncompleteMediaType { get; set; } = PlexMediaType.None;

    public int PlexServerAccessCount { get; set; } = 5;

    public int RolePerMediaItemCount { get; set; } = 5;

    public int CountriesPerMediaItemCount { get; set; } = 2;

    public int GenrePerMediaItemCount { get; set; } = 2;

    public int PlexServerAccessConnectionsCount { get; set; } = 5;

    public HttpStatusCode SetLibrarySectionsResponse { get; set; } = HttpStatusCode.OK;

    public bool PlexServerAccessConnectionsIncludeHttps { get; set; }

    public HttpStatusCode SetServerResourcesResponse { get; set; } = HttpStatusCode.OK;
    public bool GenerateFromDatabase { get; set; }

    public int LibraryCount(PlexMediaType type = PlexMediaType.Unknown)
    {
        return type switch
        {
            PlexMediaType.Movie => MovieLibraryCount,
            PlexMediaType.TvShow => TvShowLibraryCount,
            PlexMediaType.MusicArtist => MusicLibraryCount,
            PlexMediaType.PhotoAlbum => PhotoLibraryCount,
            PlexMediaType.OtherVideos => OtherVideoLibraryCount,
            _ => new[]
            {
                MovieLibraryCount,
                TvShowLibraryCount,
                MusicLibraryCount,
                PhotoLibraryCount,
                OtherVideoLibraryCount,
            }.Max(),
        };
    }
}
