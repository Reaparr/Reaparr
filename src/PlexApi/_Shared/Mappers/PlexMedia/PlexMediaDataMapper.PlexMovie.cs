namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexMovie> ToPlexMovies(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexMovie()).ToList();

    public static PlexMovie ToPlexMovie(this LibraryMediaItemDTO source)
    {
        var mediaDataList = source.Media.ToMovieMediaDataList(source);

        return new PlexMovie
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
            VideoQuality = mediaDataList.Count == 0 ? VideoQuality.Unknown : mediaDataList.Max(x => x.VideoQuality),
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,

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
            MediaDataList = mediaDataList,

            FullTitle = $"{source.Title} ({source.Year})",

            // Ignore the following
            PlexLibraryId = 0,
            PlexServerId = 0,
            FullBannerUrl = string.Empty,
        };
    }

    public static ICollection<PlexMovieMediaData> ToMovieMediaDataList(
        this List<LibraryMediaItemMediaDTO> source,
        LibraryMediaItemDTO root
    ) => source.SelectMany(x => x.ToMovieMediaDataList(root)).ToList();

    public static ICollection<PlexMovieMediaData> ToMovieMediaDataList(
        this LibraryMediaItemMediaDTO source,
        LibraryMediaItemDTO root
    ) => source.Parts.Select(part => part.ToPlexMovieModel(source, root)).ToList();

    public static PlexMovieMediaData ToPlexMovieModel(
        this LibraryMediaItemPartDTO source,
        LibraryMediaItemMediaDTO mediaItem,
        LibraryMediaItemDTO root
    )
    {
        var fileName = source.File.GetFileName();
        return new PlexMovieMediaData
        {
            Id = 0,
            PlexApiMediaId = mediaItem.Id,
            PlexApiPartId = source.Id,
            Key = source.Key,
            Duration = source.Duration,
            OriginalFilename = fileName,
            Source = mediaItem.DetermineReleaseSource(),
            Size = source.Size,
            Container = source.Container,
            PlexApiRatingKey = root.RatingKey,
            VideoQuality = mediaItem.VideoResolution,
            VideoCodec = mediaItem.VideoCodec,
            AudioCodec = mediaItem.AudioCodec,
            NeedsGeneratedName = !fileName.IsValidMediaFileName(),
            Streams = source.ToPlexMovieDataStreams(),

            // Ignore the following
            PlexLibraryId = 0,
            PlexServerId = 0,
            PlexMovieId = 0,
        };
    }
}
