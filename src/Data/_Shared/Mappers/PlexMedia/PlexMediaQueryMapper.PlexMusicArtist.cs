using Reaparr.Application.Contracts;

namespace Reaparr.Data;

public static partial class PlexMediaQueryMapper
{
    public static IQueryable<PlexMediaSlimDTO> ProjectMusicArtistComparisonCandidates(
        this IQueryable<PlexMusicArtist> source
    ) => source.ProjectMusicArtistPage();

    public static IQueryable<PlexMediaSlimDTO> ProjectMusicArtistPage(this IQueryable<PlexMusicArtist> source) =>
        source.Select(x => new PlexMediaSlimDTO
        {
            Id = x.Id,
            PlexApiRatingKey = x.PlexApiRatingKey,
            PlexApiMetaDataKey = x.PlexApiMetaDataKey,
            Title = x.Title,
            SearchTitle = x.SearchTitle,
            SortIndex = x.SortIndex,
            Year = 0,
            Duration = x.Duration,
            MediaSize = x.MediaSize,
            ChildCount = x.ChildCount,
            GrandChildCount = x.Albums.Sum(album => album.ChildCount),
            AddedAt = x.AddedAt,
            UpdatedAt = x.UpdatedAt,
            PlexLibraryId = x.PlexLibraryId,
            PlexServerId = x.PlexServerId,
            Type = PlexMediaType.MusicArtist,
            HasThumb = x.HasThumb,
            Qualities = x
                .Qualities.OrderBy(quality => quality.Id)
                .Select(quality => new PlexMediaQualityDTO
                {
                    VideoQuality = VideoQuality.None,
                    AudioQuality = quality.AudioQuality,
                    MediaDataType = PlexMediaType.MusicArtist,
                    DataId = -1,
                    MediaId = quality.PlexMusicArtistId,
                })
                .ToList(),
        });

    internal static IQueryable<MediaMetadataProjectionRow> ProjectMusicArtistMetadataRows(
        this IQueryable<PlexMusicArtist> source
    ) => source.Select(
        x => new MediaMetadataProjectionRow
        {
            Roles = x.Actors.Select(actor => actor.Id).ToList(),
            Countries = x.Countries.Select(country => country.Id).ToList(),
            Genres = x.Genres.Select(genre => genre.Id).ToList(),
        }
    );
}
