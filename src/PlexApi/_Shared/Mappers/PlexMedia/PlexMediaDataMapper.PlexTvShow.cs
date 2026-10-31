namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexTvShow> ToPlexTvShows(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexTvShow()).ToList();

    public static PlexTvShow ToPlexTvShow(this LibraryMediaItemDTO source) =>
        new()
        {
            Id = 0,
            Title = source.Title,
            Year = source.Year,

            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Guid = source.Guid,

            Guid_IMDB = source.Guids.GetImdbId(),
            Guid_TMDB = source.Guids.GetTmdbId(),
            Guid_TVDB = source.Guids.GetTvdbId(),

            Duration = source.Duration,
            MediaSize = source.Media.Sum(y => y.Parts.Sum(z => z.Size)),
            VideoQuality = VideoQuality.Unknown,
            ChildCount = source.ChildCount,
            GrandChildCount = 0, // This is set later on in BuildTvShowTree
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,

            FullTitle = $"{source.Title} ({source.Year})",

            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            HasTheme = !string.IsNullOrEmpty(source.Theme),

            Countries = source.Country.ToPlexCountry(),
            Actors = source.Role.ToPlexActor(),
            Genres = source.Genre.ToPlexGenreVideo(),

            // Ignore the following
            PlexLibrary = default,
            PlexServer = default,
            PlexLibraryId = default,
            PlexServerId = default,
            FullBannerUrl = string.Empty,
            Qualities = [],
        };
}
