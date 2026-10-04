namespace Reaparr.BaseTests;

public class PlexApiDataConfig : BaseConfig<PlexApiDataConfig>
{
    public Seed Seed { get; set; } = new(9999);

    public int MovieLibraryCount { get; set; } = 0;

    public int TvShowLibraryCount { get; set; } = 0;
    public int MusicLibraryCount { get; set; } = 0;

    public int PhotoLibraryCount { get; set; } = 0;

    public int OtherVideoLibraryCount { get; set; } = 0;

    public int MoviesPerLibraryCount { get; set; } = 0;

    public int TvShowsPerLibraryCount { get; set; } = 0;

    public int SeasonsPerTvShowCount { get; set; } = 0;

    public int EpisodesPerSeasonCount { get; set; } = 0;

    public int ArtistsPerLibraryCount { get; set; } = 0;

    public int AlbumsPerArtistCount { get; set; } = 0;

    public int TracksPerAlbumCount { get; set; } = 0;

    public int PhotoAlbumsPerLibraryCount { get; set; } = 0;

    public int PhotosPerAlbumCount { get; set; } = 0;

    public int PhotoClipsPerAlbumCount { get; set; } = 0;

    public int OtherVideosPerLibraryCount { get; set; } = 0;

    public PlexMediaType FailMediaType { get; set; } = PlexMediaType.None;

    public PlexMediaType IncompleteMediaType { get; set; } = PlexMediaType.None;

    public int PlexServerAccessCount { get; set; } = 5;

    public int RolePerMediaItemCount { get; set; } = 5;

    public int CountriesPerMediaItemCount { get; set; } = 2;

    public int GenrePerMediaItemCount { get; set; } = 2;

    public int PlexServerAccessConnectionsCount { get; set; } = 5;

    public HttpStatusCode SetLibrarySectionsResponse { get; set; } = HttpStatusCode.OK;

    public bool PlexServerAccessConnectionsIncludeHttps { get; set; } = false;

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
