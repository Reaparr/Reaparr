using System.Globalization;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;

namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexPhotoMediaData> ToPlexPhotoMediaDataList(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexPhotoMediaData()).ToList();

    internal static PlexPhotoMediaData ToPlexPhotoMediaData(this Metadata source, PlexMediaType mediaType) =>
        source.ToMediaItemDTO(mediaType).ToPlexPhotoMediaData(source.Summary);

    public static PlexPhotoMediaData ToPlexPhotoMediaData(this LibraryMediaItemDTO source) =>
        source.ToPlexPhotoMediaData(source.Summary);

    private static PlexPhotoMediaData ToPlexPhotoMediaData(
        this LibraryMediaItemDTO source,
        string? summary
    )
    {
        if (source.Media.Count != 1 || source.Media[0].Parts.Count != 1)
            throw new InvalidOperationException(
                "Plex photo assets must contain exactly one Media item with exactly one Part"
            );

        var media = source.Media[0];
        var part = media.Parts[0];
        var sourcePath = string.IsNullOrWhiteSpace(part.File) ? null : part.File;

        return new PlexPhotoMediaData
        {
            Id = 0,
            PlexLibraryId = 0,
            PlexServerId = 0,
            PlexApiRatingKey = source.RatingKey,
            PlexApiMediaId = media.Id,
            PlexApiPartId = part.Id,
            AssetKind = source.Type is PlexMediaType.OtherVideos or PlexMediaType.Movie
                ? PhotoAssetKind.VideoClip
                : PhotoAssetKind.Image,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            Summary = summary,
            HasThumb = !string.IsNullOrWhiteSpace(source.Thumb),
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            OriginallyAvailableAt = ParseOriginalDate(source.OriginallyAvailableAt),
            SourcePath = sourcePath,
            OriginalFilename = sourcePath.GetSourcePathLeafName(),
            Key = part.Key,
            Size = Math.Max(0, part.Size),
            Container = string.IsNullOrWhiteSpace(part.Container) ? media.Container : part.Container,
            Width = media.Width > 0 ? media.Width : null,
            Height = media.Height > 0 ? media.Height : null,
            Duration = (part.Duration, media.Duration) switch
            {
                (> 0, _) => part.Duration,
                (_, > 0) => media.Duration,
                _ => null,
            },
            VideoCodec = string.IsNullOrWhiteSpace(media.VideoCodec) ? null : media.VideoCodec,
            AudioCodec = string.IsNullOrWhiteSpace(media.AudioCodec) ? null : media.AudioCodec,
        };
    }

    private static DateTime? ParseOriginalDate(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed
        )
            ? parsed.UtcDateTime
            : null;
}
