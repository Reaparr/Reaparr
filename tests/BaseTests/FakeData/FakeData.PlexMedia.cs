namespace Reaparr.BaseTests;

public static partial class FakeData
{
    #region Base

    private static Faker<T> ApplyBasePlexMedia<T>(this Faker<T> faker)
        where T : BasePlexMedia
    {
        return faker
            .StrictMode(true)
            .Ignore(x => x.Id)
            .RuleFor(x => x.PlexApiRatingKey, _ => GetUniqueNumber())
            .Ignore(x => x.Title)
            .Ignore(x => x.SortIndex)
            .RuleFor(x => x.SearchTitle, (_, x) => x.Title.ToSearchTitle())
            .RuleFor(x => x.Duration, f => f.Random.Int(1000, 3000000))
            .RuleFor(x => x.MediaSize, f => f.Random.Long(1000, 30000000))
            .RuleFor(x => x.VideoQuality, _ => VideoQuality.Unknown)
            .RuleFor(x => x.ComparisonState, _ => PlexMediaComparisonState.NotCompared)
            .RuleFor(x => x.PlexApiMetaDataKey, f => f.Random.Int(1, 10000))
            .RuleFor(x => x.HasThumb, f => f.Random.Bool())
            .RuleFor(x => x.HasArt, f => f.Random.Bool())
            .RuleFor(x => x.ChildCount, f => f.Random.Int(1, 10))
            .RuleFor(x => x.AddedAt, f => f.Date.Recent(30))
            .RuleFor(x => x.UpdatedAt, f => f.Date.Recent(30))
            .Ignore(x => x.PlexServerId)
            .Ignore(x => x.PlexServer)
            .Ignore(x => x.PlexLibraryId)
            .Ignore(x => x.PlexLibrary)
            .Ignore(x => x.FullBannerUrl);
    }

    #endregion

    #region PlexMovies

    private static readonly Faker<PlexMovie> _plexMovieFaker =
        new Faker<PlexMovie>()
            .ApplyBasePlexMedia()
            .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
            .RuleFor(x => x.Studio, f => f.Company.CompanyName())
            .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
            .RuleFor(x => x.ContentRating, f => f.Lorem.Word())
            .RuleFor(x => x.Rating, f => f.Random.Double(0.1))
            .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Recent(30))
            .RuleFor(x => x.HasTheme, f => f.Random.Bool())
            .RuleFor(x => x.FullTitle, (_, x) => $"{x.Title} ({x.Year})")
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.None))
            .RuleFor(x => x.Guid_IMDB, f => "tt" + f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TMDB, f => f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TVDB, f => f.Random.Int(10000, 99999))
            .Ignore(x => x.Actors)
            .Ignore(x => x.Genres)
            .Ignore(x => x.Countries)
            .RuleFor(x => x.Title, f => f.PlexMedia().MediaTitle(PlexMediaType.Movie))
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.Movie))
            .FinishWith(
                (_, movie) =>
                {
                    movie.FullTitle = $"{movie.Title} ({movie.Year})";

                    movie.VideoQuality =
                        movie.MediaDataList.Count == 0
                            ? VideoQuality.Unknown
                            : movie.MediaDataList.Max(x => x.VideoQuality);
                    movie.MediaSize = movie.MediaDataList.Sum(x => x.Size);
                }
            )
            .FinishWith(
                (_, movie) =>
                {
                    var sharedPlexMediaId = GetUniqueNumber();
                    foreach (var mediaData in movie.MediaDataList)
                    {
                        mediaData.UpdateInitProperty(nameof(PlexMovieMediaData.PlexApiMediaId), sharedPlexMediaId);
                        mediaData.UpdateInitProperty(nameof(PlexMovieMediaData.PlexApiRatingKey), movie.PlexApiRatingKey);
                    }
                }
            );

    public static Faker<PlexMovie> GetPlexMovies(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);

        return _plexMovieFaker
            .RuleFor(
                x => x.MediaDataList,
                _ => GetPlexMovieMediaData(seed, options).Generate(config.IncludeMultiPartMovies ? 2 : 1)
            )
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexTvShow> _plexTvShowFaker =
        new Faker<PlexTvShow>()
            .ApplyBasePlexMedia()
            .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
            .RuleFor(x => x.Studio, f => f.Company.CompanyName())
            .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
            .RuleFor(x => x.ContentRating, f => f.Lorem.Word())
            .RuleFor(x => x.Rating, f => f.Random.Double(0.1))
            .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Recent(30))
            .RuleFor(x => x.HasTheme, f => f.Random.Bool())
            .RuleFor(x => x.FullTitle, (_, x) => $"{x.Title} ({x.Year})")
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.None))
            .RuleFor(x => x.Guid_IMDB, f => "tt" + f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TMDB, f => f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TVDB, f => f.Random.Int(10000, 99999))
            .Ignore(x => x.Actors)
            .Ignore(x => x.Genres)
            .Ignore(x => x.Countries)
            .Ignore(x => x.Qualities)
            .RuleFor(x => x.Title, f => f.PlexMedia().MediaTitle(PlexMediaType.TvShow))
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.TvShow))
            .FinishWith(
                (_, tvShow) =>
                {
                    foreach (var (season, seasonIndex) in tvShow.Seasons.Select((season, index) => (season, index)))
                    {
                        season.Title = $"{tvShow.Title} {seasonIndex + 1:D2}";
                        season.ParentKey = tvShow.PlexApiRatingKey;
                        season.ParentGuid = tvShow.Guid;
                        season.FullTitle = $"{tvShow.Title}/{season.Title}";
                        season.SeasonNumber = seasonIndex + 1;

                        foreach (
                            var (episode, episodeIndex) in season.Episodes.Select((episode, index) => (episode, index))
                        )
                        {
                            var originalTitle = episode.Title;
                            episode.Title = $"S{seasonIndex + 1:D2}E{episodeIndex + 1:D2} - {originalTitle}";
                            episode.ParentKey = season.PlexApiRatingKey;
                            episode.ParentGuid = season.Guid;
                            episode.FullTitle = $"{tvShow.Title}/{season.Title}/{episode.Title}";
                            episode.EpisodeNumber = episodeIndex + 1;
                        }
                    }

                    tvShow.ChildCount = tvShow.Seasons.Count;
                    tvShow.GrandChildCount = tvShow.Seasons.Sum(season => season.Episodes.Count);
                    tvShow.VideoQuality =
                        tvShow
                            .Seasons.SelectMany(season => season.Episodes)
                            .SelectMany(episode => episode.MediaDataList)
                            .Select(mediaData => (VideoQuality?)mediaData.VideoQuality)
                            .Max()
                        ?? VideoQuality.Unknown;
                    tvShow.MediaSize = tvShow.Seasons.Sum(season => season.MediaSize);
                }
            );

    public static Faker<PlexTvShow> GetPlexTvShows(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);

        return _plexTvShowFaker
            .RuleFor(x => x.Seasons, _ => GetPlexTvShowSeason(seed, options).Generate(config.TvShowSeasonCount))
            .RuleFor(x => x.ChildCount, _ => config.TvShowSeasonCount)
            .RuleFor(x => x.GrandChildCount, _ => config.TvShowSeasonCount * config.TvShowEpisodeCount)
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexTvShowSeason> _plexTvShowSeasonFaker =
        new Faker<PlexTvShowSeason>()
            .ApplyBasePlexMedia()
            .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
            .RuleFor(x => x.Studio, f => f.Company.CompanyName())
            .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
            .RuleFor(x => x.ContentRating, f => f.Lorem.Word())
            .RuleFor(x => x.Rating, f => f.Random.Double(0.1))
            .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Recent(30))
            .RuleFor(x => x.HasTheme, f => f.Random.Bool())
            .RuleFor(x => x.FullTitle, (_, x) => $"{x.Title} ({x.Year})")
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.None))
            .RuleFor(x => x.Guid_IMDB, f => "tt" + f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TMDB, f => f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TVDB, f => f.Random.Int(10000, 99999))
            .RuleFor(x => x.ParentKey, _ => GetUniqueNumber())
            .RuleFor(x => x.Title, _ => "Season")
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.Season))
            .Ignore(x => x.SeasonNumber)
            .Ignore(x => x.Qualities)
            .Ignore(x => x.TvShowId)
            .Ignore(x => x.TvShow)
            .Ignore(x => x.ParentGuid)
            .FinishWith(
                (_, tvShowSeason) =>
                {
                    tvShowSeason.MediaSize = tvShowSeason.Episodes.Select(x => x.MediaSize).Sum();
                    foreach (var episode in tvShowSeason.Episodes)
                    {
                        episode.ParentGuid = tvShowSeason.Guid;
                    }
                }
            );

    public static Faker<PlexTvShowSeason> GetPlexTvShowSeason(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);
        return _plexTvShowSeasonFaker
            .RuleFor(x => x.Episodes, _ => GetPlexTvShowEpisode(seed, options).Generate(config.TvShowEpisodeCount))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexTvShowEpisode> _plexTvShowEpisodeFaker =
        new Faker<PlexTvShowEpisode>()
            .ApplyBasePlexMedia()
            .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
            .RuleFor(x => x.Studio, f => f.Company.CompanyName())
            .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
            .RuleFor(x => x.ContentRating, f => f.Lorem.Word())
            .RuleFor(x => x.Rating, f => f.Random.Double(0.1))
            .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Recent(30))
            .RuleFor(x => x.HasTheme, f => f.Random.Bool())
            .RuleFor(x => x.FullTitle, (_, x) => $"{x.Title} ({x.Year})")
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.None))
            .RuleFor(x => x.Guid_IMDB, f => "tt" + f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TMDB, f => f.Random.Int(10000, 99999))
            .RuleFor(x => x.Guid_TVDB, f => f.Random.Int(10000, 99999))
            .Ignore(x => x.Id)
            .Ignore(x => x.TvShowId)
            .Ignore(x => x.TvShow)
            .Ignore(x => x.TvShowSeasonId)
            .Ignore(x => x.TvShowSeason)
            .Ignore(x => x.ParentGuid)
            .Ignore(x => x.EpisodeNumber)
            .RuleFor(x => x.ExpectedSeasonNumber, _ => -1)
            .RuleFor(x => x.ParentKey, _ => GetUniqueNumber())
            .RuleFor(x => x.Title, f => f.PlexMedia().MediaTitle(PlexMediaType.Episode))
            .RuleFor(x => x.Guid, f => f.PlexMedia().Guid(PlexMediaType.Episode))
            .FinishWith(
                (_, tvShowEpisode) =>
                {
                    tvShowEpisode.MediaSize = tvShowEpisode.MediaDataList.Select(x => x.Size).Sum();
                    foreach (var mediaData in tvShowEpisode.MediaDataList)
                        mediaData.UpdateInitProperty(nameof(PlexTvShowEpisodeMediaData.PlexApiRatingKey), tvShowEpisode.PlexApiRatingKey);
                }
            );

    public static Faker<PlexTvShowEpisode> GetPlexTvShowEpisode(Seed seed, Action<FakeDataConfig>? options = null) =>
        _plexTvShowEpisodeFaker
            .RuleFor(x => x.MediaDataList, _ => GetPlexTvShowEpisodeMediaData(seed, options).Generate(1))
            .UseSeed(seed.Next());


    #endregion
}
