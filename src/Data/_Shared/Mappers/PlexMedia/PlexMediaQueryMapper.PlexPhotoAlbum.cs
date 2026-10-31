using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;

namespace Reaparr.Data;

public static partial class PlexMediaQueryMapper
{
    public static IQueryable<MediaNavigationIndexRow> ProjectNavigationIndexRows(
        this IQueryable<PlexMediaSlimDTO> source
    ) =>
        source.Select(x => new MediaNavigationIndexRow
        {
            SearchTitle = x.SearchTitle,
            Year = x.Year,
            QualityValue = null,
            Duration = x.Duration,
            AddedAt = x.AddedAt,
            UpdatedAt = x.UpdatedAt,
            MediaSize = x.MediaSize,
        });
}
