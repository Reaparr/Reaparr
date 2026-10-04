namespace Reaparr.Application.Contracts;

public static class PlexMediaSlimDTOMapper
{
    #region PlexMovie

    public static PlexMediaSlimDTO ToSlimDTO(this PlexMovie source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            SortIndex = source.SortIndex,
            Year = source.Year,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = source.MediaSize,
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = source.Type,
            HasThumb = source.HasThumb,
            GrandChildCount = 0,
            Qualities = source
                .MediaDataList.SortByQuality()
                .Select(x => new PlexMediaQualityDTO
                {
                    Quality = x.Quality,
                    MediaDataType = x.Type,
                    DataId = x.Id,
                    MediaId = source.Id,
                })
                .ToList(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
        };

    public static IQueryable<PlexMediaSlimDTO> ProjectToMediaSlimDTO(this IQueryable<PlexMovie> source) =>
        source.Select(x => x.ToSlimDTO());

    #endregion

    #region PlexTvShow

    public static IQueryable<PlexMediaSlimDTO> ProjectToMediaSlimDTO(this IQueryable<PlexTvShow> source) =>
        source.Select(x => new PlexMediaSlimDTO
        {
            Id = x.Id,
            Title = x.Title,
            SearchTitle = x.SearchTitle,
            SortIndex = x.SortIndex,
            Year = x.Year,
            Duration = x.Duration,
            MediaSize = x.MediaSize,
            ChildCount = x.ChildCount,
            GrandChildCount = x.GrandChildCount,
            AddedAt = x.AddedAt,
            UpdatedAt = x.UpdatedAt,
            PlexLibraryId = x.PlexLibraryId,
            PlexServerId = x.PlexServerId,
            Type = x.Type,
            HasThumb = x.HasThumb,
            PlexApiRatingKey = x.PlexApiRatingKey,
            PlexApiMetaDataKey = x.PlexApiMetaDataKey,
            Qualities = x
                .Qualities.Select(q => new PlexMediaQualityDTO
                {
                    Quality = q.Quality,
                    MediaDataType = q.Type,
                    DataId = q.Id,
                    MediaId = x.Id,
                })
                .ToList(),
        });

    public static PlexMediaSlimDTO ToSlimDTOMapper(this PlexTvShow source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = source.SortIndex,
            Year = source.Year,
            Duration = source.Duration,
            MediaSize = source.MediaSize,
            ChildCount = source.ChildCount,
            GrandChildCount = source.GrandChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = source.Type,
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            HasThumb = source.HasThumb,
            Qualities = source.Qualities.ToDTO(),
        };

    #endregion

    #region PlexEpisode

    public static PlexMediaSlimDTO ToSlimDTO(this PlexTvShowEpisode source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = source.SortIndex,
            Year = source.Year,
            Duration = source.Duration,
            MediaSize = source.MediaSize,
            ChildCount = source.ChildCount,
            GrandChildCount = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = source.Type,
            HasThumb = source.HasThumb,
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            Qualities = source.MediaDataList.ToPlexMediaQuality(),
        };

    #endregion

    public static PlexMediaSlimDTO ToSlimDTOMapper(this PlexMusicArtist source) =>
        Map(source, null, source.Albums.Sum(x => x.ChildCount), []);

    public static PlexMediaSlimDTO ToSlimDTOMapper(this PlexMusicAlbum source) =>
        Map(source, source.PlexArtistId, 0, []);

    public static PlexMediaSlimDTO ToSlimDTOMapper(this PlexMusicTrack source) =>
        Map(source, source.PlexAlbumId, 0, []);

    public static PlexMediaSlimDTO ToSlimDTOMapper(this PlexPhoto source) =>
        Map(source, source.PlexPhotoAlbumId, 0, []);

    public static PlexMediaSlimDTO ToSlimDTOMapper(this PlexOtherVideo source) =>
        Map(
            source,
            null,
            0,
            source.MediaDataList.SortByQuality()
                .Select(x => new PlexMediaQualityDTO
                {
                    Quality = x.Quality,
                    MediaDataType = x.Type,
                    DataId = x.Id,
                    MediaId = source.Id,
                })
                .ToList()
        );

    public static PlexMediaSlimDTO ToSlimDTOMapper(this PlexPhotoAlbum source) =>
        Map(source, null, 0, []);

    private static PlexMediaSlimDTO Map(
        BasePlexMedia source,
        int? parentId,
        int grandChildCount,
        List<PlexMediaQualityDTO> qualities
    ) =>
        new()
        {
            Id = source.Id,
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = source.SortIndex,
            Year = source.Year,
            Duration = source.Duration,
            MediaSize = source.MediaSize,
            ChildCount = source.ChildCount,
            GrandChildCount = grandChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = source.Type,
            ParentId = parentId,
            HasThumb = source.HasThumb,
            Qualities = qualities,
        };
}
