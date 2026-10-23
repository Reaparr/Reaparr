using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;

namespace Reaparr.Data;

public static partial class PlexMediaQueryMapper
{
    public static IQueryable<PlexMediaSlimDTO> ProjectOtherVideoPage(this IQueryable<PlexOtherVideo> source) =>
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
            Type = PlexMediaType.OtherVideos,
            HasThumb = x.HasThumb,
            Qualities = x
                .MediaDataList.OrderBy(data => data.VideoQuality)
                .Select(data => new PlexMediaQualityDTO
                {
                    VideoQuality = data.VideoQuality,
                    MediaDataType = PlexMediaType.OtherVideos,
                    DataId = data.Id,
                    MediaId = data.PlexOtherVideoId,
                })
                .ToList(),
        });

    public static IQueryable<MediaNavigationIndexRow> ProjectOtherVideoNavigationIndexRows(
        this IQueryable<PlexOtherVideo> source
    ) => source.Select(
        x => new MediaNavigationIndexRow
        {
            SearchTitle = x.SearchTitle,
            Year = x.Year,
            QualityValue = (int?)x.VideoQuality,
            Duration = x.Duration,
            AddedAt = x.AddedAt,
            UpdatedAt = x.UpdatedAt,
            MediaSize = x.MediaSize,
        }
    );

    internal static IQueryable<MediaMetadataProjectionRow> ProjectOtherVideoMetadataRows(
        this IQueryable<PlexOtherVideo> source
    ) => source.Select(
        x => new MediaMetadataProjectionRow
        {
            Roles = x.Actors.Select(actor => actor.Id).ToList(),
            Countries = x.Countries.Select(country => country.Id).ToList(),
            Genres = x.Genres.Select(genre => genre.Id).ToList(),
        }
    );
}
