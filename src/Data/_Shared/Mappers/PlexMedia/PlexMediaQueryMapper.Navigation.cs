using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;

namespace Reaparr.Data;

public static partial class PlexMediaQueryMapper
{
    public static IQueryable<MediaNavigationIndexRow> ProjectMusicArtistNavigationIndexRows(
        this IQueryable<PlexMusicArtist> source
    ) =>
        source.Select(x => new MediaNavigationIndexRow
        {
            SearchTitle = x.SearchTitle,
            Year = 0,
            QualityValue = x.Qualities.Select(quality => (int?)quality.AudioQuality).Max() ?? 0,
            Duration = x.Duration,
            AddedAt = x.AddedAt,
            UpdatedAt = x.UpdatedAt,
            MediaSize = x.MediaSize,
        });
}
