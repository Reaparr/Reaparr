namespace Reaparr.Application;

public static partial class PlexMediaDTOMapper
{
    public static PlexMediaDTO ToDTO(this PlexPhotoAlbum source) =>
        new()
        {
            Id = source.Id,
            ParentId = null,
            TvShowId = default,
            TvShowSeasonId = default,
            MediaData = [],
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = 0,
            Year = 0,
            Duration = source.Memberships.Sum(x => x.PlexPhotoMediaData?.Duration ?? 0),
            MediaSize = source.Memberships.Sum(x => x.PlexPhotoMediaData?.Size ?? 0),
            ChildCount = source.Memberships.Count,
            GrandChildCount = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = PlexMediaType.PhotoAlbum,
            HasThumb = source.HasThumb,
            Qualities = [],
            ComparisonId = PlexMediaComparisonState.NotCompared.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiRatingKey,
            HasArt = false,
            HasTheme = false,
            Studio = string.Empty,
            Summary = source.Summary ?? string.Empty,
            ContentRating = null,
            Rating = 0,
            OriginallyAvailableAt = null,
            RecordLabel = null,
            Genres = [],
            Children = source
                .Memberships.Select(x => x.PlexPhotoMediaData)
                .OfType<PlexPhotoMediaData>()
                .OrderBy(x => x.Id)
                .OrderByNatural(x => x.Title)
                .Select(x => x.ToPhotoDTO())
                .ToList(),
            DiscNumber = null,
            TrackNumber = null,
        };

    public static PlexMediaDTO ToPhotoDTO(this PlexPhotoMediaData source) =>
        new()
        {
            Id = source.Id,
            ParentId = null,
            TvShowId = default,
            TvShowSeasonId = default,
            MediaData = [],
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = 0,
            Year = 0,
            Duration = source.Duration ?? 0,
            MediaSize = source.Size,
            ChildCount = 0,
            GrandChildCount = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = PlexMediaType.PhotoImage,
            HasThumb = source.HasThumb,
            Qualities = [],
            ComparisonId = PlexMediaComparisonState.NotCompared.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiRatingKey,
            HasArt = false,
            HasTheme = false,
            Studio = string.Empty,
            Summary = source.Summary ?? string.Empty,
            ContentRating = null,
            Rating = 0,
            OriginallyAvailableAt = source.OriginallyAvailableAt,
            RecordLabel = null,
            Genres = [],
            Children = [],
            DiscNumber = null,
            TrackNumber = null,
        };
}
