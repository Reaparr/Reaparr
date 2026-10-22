namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexTvShowSeason> ToPlexTvShowSeasons(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexTvShowSeason()).ToList();

    public static PlexTvShowSeason ToPlexTvShowSeason(this LibraryMediaItemDTO source) =>
        new()
        {
            Id = 0,
            Title = source.Title,
            Year = source.Year,

            // This is set later on
            SortIndex = source.SortIndex,
            SeasonNumber = source.Index,

            SearchTitle = source.SearchTitle,
            Guid = source.Guid,

            Guid_IMDB = source.Guids.GetImdbId(),
            Guid_TMDB = source.Guids.GetTmdbId(),
            Guid_TVDB = source.Guids.GetTvdbId(),

            Duration = source.Duration,
            MediaSize = source.Media.Sum(y => y.Parts.Sum(z => z.Size)),
            ChildCount = source.ChildCount,

            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            ParentKey = source.GetParentKey(),
            ParentGuid = source.ParentGuid,

            FullTitle = $"{source.ParentTitle}/{source.Title}",

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

            // Ignore the following
            PlexLibrary = default,
            PlexServer = default,
            PlexLibraryId = default,
            PlexServerId = default,
            FullBannerUrl = string.Empty,
            Qualities = [],
        };
}
