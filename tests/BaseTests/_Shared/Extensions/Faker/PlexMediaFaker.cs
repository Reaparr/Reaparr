namespace Reaparr.BaseTests;

public static class PlexMediaFaker
{
    // TODO make this a property when migrated to C# 14
    public static PlexMediaDataSet PlexMedia(this Faker faker)
    {
        return ContextHelper.GetOrSet(faker, () => new PlexMediaDataSet(faker));
    }
}

public class PlexMediaDataSet : DataSet
{
    private readonly Faker _faker;

    public PlexMediaDataSet(Faker faker)
    {
        _faker = faker;
    }

    public string MediaGenre() => _faker.PickRandomFromDataset(PlexMediaGenreDataset.PlexMediaGenres.Value);

    public string MediaTitle(PlexMediaType type) =>
        type switch
        {
            PlexMediaType.Movie or PlexMediaType.OtherVideos => _faker.PickRandomFromDataset(
                PlexMovieShowTitlesDataset.PlexMovieTitles.Value
            ),
            PlexMediaType.MusicArtist => _faker.Name.FullName(),
            PlexMediaType.TvShow => _faker.PickRandomFromDataset(PlexTvShowTitlesDataset.PlexTvShowTitles.Value),
            PlexMediaType.Episode => _faker.PickRandomFromDataset(PlexEpisodeShowTitlesDataset.PlexEpisodeTitles.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "PlexMediaType not supported."),
        };

    public string MediaTitle(DownloadTaskType type) =>
        type switch
        {
            DownloadTaskType.Movie =>
                $"Movie - {_faker.PickRandomFromDataset(PlexMovieShowTitlesDataset.PlexMovieTitles.Value)}",
            DownloadTaskType.MovieData =>
                $"MovieData - {_faker.PickRandomFromDataset(PlexMovieShowTitlesDataset.PlexMovieTitles.Value)}",
            DownloadTaskType.TvShow =>
                $"TvShow - {_faker.PickRandomFromDataset(PlexTvShowTitlesDataset.PlexTvShowTitles.Value)}",
            DownloadTaskType.Season => "Season",
            DownloadTaskType.Episode =>
                $"Episode - {_faker.PickRandomFromDataset(PlexEpisodeShowTitlesDataset.PlexEpisodeTitles.Value)}",
            DownloadTaskType.EpisodeData =>
                $"EpisodeData - {_faker.PickRandomFromDataset(PlexEpisodeShowTitlesDataset.PlexEpisodeTitles.Value)}",
            DownloadTaskType.MusicArtist => $"Music Artist - {_faker.Name.FullName()}",
            DownloadTaskType.MusicAlbum => $"Music Album - {_faker.Lorem.Word()}",
            DownloadTaskType.MusicTrack => $"Music Track - {_faker.Lorem.Word()}",
            DownloadTaskType.MusicTrackData => $"MusicTrackData - {_faker.Lorem.Word()}",
            DownloadTaskType.PhotoAlbum => $"Photo Album - {_faker.Lorem.Word()}",
            DownloadTaskType.PhotoImage => $"Photo - {_faker.Lorem.Word()}",
            DownloadTaskType.PhotoData => $"PhotoData - {_faker.Lorem.Word()}",
            DownloadTaskType.OtherVideo =>
                $"OtherVideo - {_faker.PickRandomFromDataset(PlexMovieShowTitlesDataset.PlexMovieTitles.Value)}",
            DownloadTaskType.OtherVideoData =>
                $"OtherVideoData - {_faker.PickRandomFromDataset(PlexMovieShowTitlesDataset.PlexMovieTitles.Value)}",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "PlexMediaType not supported."),
        };

    public string Guid(PlexMediaType type)
    {
        var mediaType = type == PlexMediaType.OtherVideos ? PlexMediaType.Movie : type;
        return $"plex://{mediaType.ToPlexApiString()}/${_faker.Random.Guid().ToString().Replace("-", "")}";
    }
}
