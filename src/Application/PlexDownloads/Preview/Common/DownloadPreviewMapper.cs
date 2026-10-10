namespace Reaparr.Application;

public static class DownloadPreviewMapper
{
    #region ToDTO

    private static int _currentId = 1;
    private const int NO_MEDIA_DATA_ID = -1;

    public static DownloadPreviewDTO ToDTO(this DownloadPreview source) =>
        new()
        {
            // Media Id's can overlap, so we use a static counter to generate unique keys
            Key = Interlocked.Increment(ref _currentId).ToString(),
            Title = source.Title,
            Size = source.Size,
            Type = source.MediaType,
            Children = source.Children.ConvertAll(ToDTO),
            Qualities = source
                .Qualities.Select(x => new PlexMediaQualityDTO
                {
                    Quality = x.Quality,
                    MediaDataType = x.MediaDataType,
                    DataId = x.DataId,
                    MediaId = x.MediaId,
                })
                .ToList(),
        };

    public static DownloadPreviewContainerDTO ToDTO(this List<DownloadPreview> source)
    {
        var previews = source.ConvertAll(ToDTO);

        Dictionary<string, bool> FlattenKeysToDictionary(List<DownloadPreviewDTO> list)
        {
            var result = new Dictionary<string, bool>();

            void Traverse(DownloadPreviewDTO item, int depth)
            {
                result.Add(item.Key, true);

                // Don't need all the episode keys as well to be fully expanded
                if (depth >= 0)
                    return;

                foreach (var child in item.Children)
                    Traverse(child, depth + 1);
            }

            foreach (var item in list)
                Traverse(item, 0);

            return result;
        }

        return new DownloadPreviewContainerDTO
        {
            TotalSize = previews.Sum(x => x.Size),
            Expanded = FlattenKeysToDictionary(previews),
            Previews = previews,
        };
    }

    #endregion

    #region PlexMovie

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexMovie source)
    {
        var totalMediaSize = source.MediaDataList.Sum(x => x.Size);

        return new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = totalMediaSize == 0 ? source.MediaSize : totalMediaSize,
            ChildCount = source.ChildCount,
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = source
                .MediaDataList.SortByQuality()
                .Select(x => new PlexMediaQuality
                {
                    Quality = x.Quality,
                    MediaDataType = x.Type,
                    DataId = x.Id,
                    MediaId = source.Id,
                })
                .ToList(),
        };
    }

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexMovie> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexTvShow

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexTvShow source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = source.MediaSize,
            ChildCount = source.ChildCount,
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = source
                .Qualities.SortByQuality()
                .Select(x => new PlexMediaQuality
                {
                    Quality = x.Quality,
                    MediaDataType = x.Type,
                    DataId = NO_MEDIA_DATA_ID,
                    MediaId = source.Id,
                })
                .ToList(),
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexTvShow> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexSeason

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexTvShowSeason source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = source.MediaSize,
            ChildCount = source.ChildCount,
            MediaType = source.Type,
            TvShowId = source.TvShowId,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = source
                .Qualities.SortByQuality()
                .Select(x => new PlexMediaQuality
                {
                    Quality = x.Quality,
                    MediaDataType = x.Type,
                    DataId = NO_MEDIA_DATA_ID,
                    MediaId = source.Id,
                })
                .ToList(),
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexTvShowSeason> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexTvShowEpisode

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexTvShowEpisode source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = source.MediaSize,
            ChildCount = source.ChildCount,
            MediaType = source.Type,
            TvShowId = source.TvShowId,
            SeasonId = source.TvShowSeasonId,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = source
                .MediaDataList.SortByQuality()
                .Select(x => new PlexMediaQuality
                {
                    Quality = x.Quality,
                    MediaDataType = x.Type,
                    MediaId = source.Id,
                    DataId = x.Id,
                })
                .ToList(),
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexTvShowEpisode> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    private static TvShowEpisodeKey ProjectToEpisodeKey(this PlexTvShowEpisode source) =>
        new()
        {
            TvShowId = source.TvShowId,
            SeasonId = source.TvShowSeasonId,
            EpisodeId = source.Id,
            MediaDataList = source.MediaDataList,
        };

    public static IQueryable<TvShowEpisodeKey> ProjectToEpisodeKey(this IQueryable<PlexTvShowEpisode> source) =>
        source.Select(x => ProjectToEpisodeKey(x));

    #endregion

    #region PlexMusicArtist

    internal static DownloadPreview ProjectToDownloadPreviewMapper(this PlexMusicArtist source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = 0,
            ChildCount = 0,
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = [],
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexMusicArtist> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexMusicAlbum

    internal static DownloadPreview ProjectToDownloadPreviewMapper(this PlexMusicAlbum source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = 0,
            ChildCount = 0,
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = source.PlexArtistId,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = [],
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexMusicAlbum> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexMusicTrack

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexMusicTrack source) =>
        source.ProjectToDownloadPreviewMapper(source.MediaDataList.GroupBy(x => x.PlexApiMediaId).ToList());

    internal static DownloadPreview ProjectToDownloadPreviewMapper(
        this PlexMusicTrack source,
        List<IGrouping<int, PlexMusicTrackMediaData>> groups
    ) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = groups.Sum(x => x.Sum(y => y.Size)),
            ChildCount = groups.Sum(x => x.Count()),
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = source.PlexAlbum!.PlexArtistId,
            AlbumId = source.PlexAlbumId,
            PhotoAlbumId = default,
            Children = [],
            Qualities = groups
                .Select(group => new PlexMediaQuality
                {
                    Quality = VideoQuality.Unknown,
                    MediaDataType = source.Type,
                    DataId = group.Min(x => x.Id),
                    MediaId = source.Id,
                })
                .ToList(),
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexMusicTrack> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexOtherVideo

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexOtherVideo source) =>
        source.ProjectToDownloadPreviewMapper(source.MediaDataList.GroupBy(x => x.PlexApiMediaId).ToList());

    internal static DownloadPreview ProjectToDownloadPreviewMapper(
        this PlexOtherVideo source,
        List<IGrouping<int, PlexOtherVideoMediaData>> groups
    ) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = groups.Sum(x => x.Sum(y => y.Size)),
            ChildCount = groups.Sum(x => x.Count()),
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = groups
                .Select(group => new PlexMediaQuality
                {
                    Quality = group.First().Quality,
                    MediaDataType = source.Type,
                    DataId = group.Min(x => x.Id),
                    MediaId = source.Id,
                })
                .ToList(),
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexOtherVideo> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexPhotoAlbum

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexPhotoAlbum source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = source.MediaSize,
            ChildCount = source.ChildCount,
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = default,
            Children = [],
            Qualities = [],
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexPhotoAlbum> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion

    #region PlexPhotoImage

    private static DownloadPreview ProjectToDownloadPreviewMapper(this PlexPhotoImage source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            Size = source.MediaDataList.Sum(x => x.Size),
            ChildCount = source.MediaDataList.Count,
            MediaType = source.Type,
            TvShowId = default,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            PhotoAlbumId = source.PlexPhotoAlbumId,
            Children = [],
            Qualities = source
                .MediaDataList.GroupBy(x => x.PlexApiMediaId)
                .Select(group => new PlexMediaQuality
                {
                    Quality = VideoQuality.Unknown,
                    MediaDataType = PlexMediaType.PhotoImage,
                    DataId = group.Min(x => x.Id),
                    MediaId = source.Id,
                })
                .ToList(),
        };

    public static IQueryable<DownloadPreview> ProjectToDownloadPreview(this IQueryable<PlexPhotoImage> source) =>
        source.Select(x => ProjectToDownloadPreviewMapper(x));

    #endregion
}
