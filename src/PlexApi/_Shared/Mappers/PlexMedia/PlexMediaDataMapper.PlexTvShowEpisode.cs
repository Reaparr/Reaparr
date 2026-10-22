namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexTvShowEpisode> ToPlexTvShowEpisodes(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexTvShowEpisode()).ToList();

    public static PlexTvShowEpisode ToPlexTvShowEpisode(this LibraryMediaItemDTO source) =>
        new()
        {
            Id = 0,
            Title = source.Title,
            FullTitle = $"{source.GrandparentTitle}/{source.ParentTitle}/{source.Title}",
            Year = source.Year,

            SortIndex = source.SortIndex,
            EpisodeNumber = source.Index,
            ExpectedSeasonNumber = source.ParentIndex,

            SearchTitle = source.SearchTitle,
            Guid = source.Guid,
            ParentGuid = source.ParentGuid,

            Guid_IMDB = source.Guids.GetImdbId(),
            Guid_TMDB = source.Guids.GetTmdbId(),
            Guid_TVDB = source.Guids.GetTvdbId(),

            Duration = source.Duration,
            MediaSize = source.Media.Sum(y => y.Parts.Sum(z => z.Size)),
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            MediaDataList = source.Media.ToEpisodeMediaDataList(source),
            ParentKey = source.GetParentKey(),

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
        };

    public static ICollection<PlexTvShowEpisodeMediaData> ToEpisodeMediaDataList(
        this List<LibraryMediaItemMediaDTO> source,
        LibraryMediaItemDTO root
    ) => source.SelectMany(x => x.ToEpisodeMediaDataList(root)).ToList();

    public static ICollection<PlexTvShowEpisodeMediaData> ToEpisodeMediaDataList(
        this LibraryMediaItemMediaDTO source,
        LibraryMediaItemDTO root
    ) => source.Parts.Select(part => part.ToPlexTvShowEpisodeModel(source, root)).ToList();

    public static PlexTvShowEpisodeMediaData ToPlexTvShowEpisodeModel(
        this LibraryMediaItemPartDTO source,
        LibraryMediaItemMediaDTO mediaItem,
        LibraryMediaItemDTO root
    )
    {
        var fileName = source.File.GetFileName();

        return new PlexTvShowEpisodeMediaData
        {
            Id = 0,
            PlexApiMediaId = mediaItem.Id,
            PlexApiPartId = source.Id,
            Key = source.Key,
            Duration = source.Duration,
            OriginalFilename = fileName,
            Size = source.Size,
            Container = source.Container,
            PlexApiRatingKey = root.RatingKey,
            VideoQuality = mediaItem.VideoResolution,
            Source = mediaItem.DetermineReleaseSource(),
            VideoCodec = mediaItem.VideoCodec,
            AudioCodec = mediaItem.AudioCodec,
            NeedsGeneratedName = !fileName.IsValidMediaFileName(),
            Streams = source.ToPlexTvShowEpisodeDataStreams(),

            // Ignore the following
            PlexLibraryId = 0,
            PlexServerId = 0,
            PlexTvShowEpisodeId = 0,
        };
    }
}
