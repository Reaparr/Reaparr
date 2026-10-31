using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;

namespace Reaparr.Data;

public static partial class PlexMediaQueryMapper
{
    public static IQueryable<PlexMediaSlimDTO> ProjectMoviePage(this IQueryable<PlexMovie> source) =>
        source.Select(x => new PlexMediaSlimDTO
        {
            Id = x.Id,
            PlexApiRatingKey = x.PlexApiRatingKey,
            PlexApiMetaDataKey = x.PlexApiMetaDataKey,
            Title = x.Title,
            SearchTitle = x.SearchTitle,
            SortIndex = x.SortIndex,
            Year = x.Year,
            Duration = x.Duration,
            MediaSize = x.MediaSize,
            ChildCount = x.ChildCount,
            GrandChildCount = 0,
            AddedAt = x.AddedAt,
            UpdatedAt = x.UpdatedAt,
            PlexLibraryId = x.PlexLibraryId,
            PlexServerId = x.PlexServerId,
            Type = PlexMediaType.Movie,
            HasThumb = x.HasThumb,
            Qualities = x
                .MediaDataList.OrderBy(data => data.VideoQuality)
                .Select(data => new PlexMediaQualityDTO
                {
                    VideoQuality = data.VideoQuality,
                    MediaDataType = PlexMediaType.Movie,
                    DataId = data.Id,
                    MediaId = data.PlexMovieId,
                })
                .ToList(),
        });

    public static IQueryable<MediaNavigationIndexRow> ProjectMovieNavigationIndexRows(
        this IQueryable<PlexMovie> source
    ) =>
        source.Select(x => new MediaNavigationIndexRow
        {
            SearchTitle = x.SearchTitle,
            Year = x.Year,
            QualityValue = (int?)x.VideoQuality,
            Duration = x.Duration,
            AddedAt = x.AddedAt,
            UpdatedAt = x.UpdatedAt,
            MediaSize = x.MediaSize,
        });

    internal static IQueryable<MediaMetadataProjectionRow> ProjectMovieMetadataRows(
        this IQueryable<PlexMovie> source
    ) =>
        source.Select(x => new MediaMetadataProjectionRow
        {
            Roles = x.Actors.Select(actor => actor.Id).ToList(),
            Countries = x.Countries.Select(country => country.Id).ToList(),
            Genres = x.Genres.Select(genre => genre.Id).ToList(),
        });
}

internal sealed class MediaMetadataProjectionRow
{
    public List<int> Roles { get; init; } = [];

    public List<int> Countries { get; init; } = [];

    public List<int> Genres { get; init; } = [];
}
