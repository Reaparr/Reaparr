using System.Text.RegularExpressions;

namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    [GeneratedRegex("[^0-9]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MyRegex();

    private static string? GetImdbId(this List<MetaDataGuidsDTO> guids) =>
        guids.Find(x => x.Id.Contains("imdb"))?.Id.Replace("imdb://", "");

    private static int? GetTmdbId(this List<MetaDataGuidsDTO> guids)
    {
        var id = guids.Find(x => x.Id.Contains("tmdb"))?.Id.Replace("tmdb://", "");
        return int.TryParse(id, out var result) ? result : null;
    }

    private static int? GetTvdbId(this List<MetaDataGuidsDTO> guids)
    {
        var id = guids.Find(x => x.Id.Contains("tvdb"))?.Id.Replace("tvdb://", "");
        return int.TryParse(id, out var result) ? result : null;
    }

    private static string? GetMusicBrainzId(LibraryMediaItemDTO source)
    {
        if (TryParseMusicBrainzId(source.Guid, out var musicBrainzId))
            return musicBrainzId.ToString();

        foreach (var guid in source.Guids)
        {
            if (TryParseMusicBrainzId(guid.Id, out musicBrainzId))
                return musicBrainzId.ToString();
        }

        return null;
    }

    private static bool TryParseMusicBrainzId(string? value, out Guid musicBrainzId)
    {
        musicBrainzId = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        const string prefix = "mbid://";
        var normalized = value.Trim();
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(normalized.AsSpan(prefix.Length), out musicBrainzId);
    }

    /// <summary>
    /// Retrieves the MetaDataKey from either the ThumbUrl,BannerUrl, ArtUrl or ThemeUrl.
    /// It is assumed that all MetaDataKeys are the same, returns 0 if nothing is found.
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    private static int RetrieveMetaDataKey(LibraryMediaItemDTO metadata)
    {
        List<string> list = [metadata.Thumb, metadata.Art, metadata.Theme];

        foreach (var entry in list)
            if (!string.IsNullOrEmpty(entry))
            {
                // We want the last number
                // Example: /library/metadata/457047/thumb/1587006741
                var splitStrings = entry.Split('/').ToList();
                if (splitStrings.Count > 2)
                {
                    if (int.TryParse(splitStrings.Last(), out var result))
                        return result;
                }
            }

        return 0;
    }

    /// <summary>
    /// The PlexAPI is sometimes missing the ParentKey, this method will attempt to get the ParentKey from the ParentGuid.
    /// </summary>
    /// <param name="originalSource"> The original source to get the ParentKey from.</param>
    /// <returns>The ParentKey if found and otherwise -1.</returns>
    private static int GetParentKey(this LibraryMediaItemDTO originalSource)
    {
        if (int.TryParse(originalSource.ParentRatingKey, out var parentKey))
            return parentKey;

        var parentGuid = originalSource.ParentGuid;
        if (string.IsNullOrEmpty(parentGuid) || !parentGuid.Contains("local"))
            return -1;

        // Replace all non-numeric characters
        var result = MyRegex().Replace(parentGuid, "");
        return int.TryParse(result, out parentKey) ? parentKey : -1;
    }

    private static ReleaseSource DetermineReleaseSource(this LibraryMediaItemMediaDTO mediaData)
    {
        var container = mediaData.Container.ToLowerInvariant();
        var audioCodec = mediaData.AudioCodec.ToLowerInvariant();
        var audioProfile = mediaData.AudioProfile.ToLowerInvariant();

        // DVD
        if (container is "vob" or "mpg" or "mpeg")
            return ReleaseSource.DVD;

        // Blu-ray (container-level)
        if (container is "m2ts" or "bdmv")
        {
            var isLosslessAudio =
                audioCodec is "truehd" or "dts-hd ma" or "dts-hd.ma"
                || (audioCodec == "dca" && audioProfile.Contains("ma"));

            return isLosslessAudio ? ReleaseSource.BluRayRemux : ReleaseSource.BluRay;
        }

        // HDTV / broadcast
        if (container is "ts" or "mpegts")
            return ReleaseSource.HDTV;

        // Web-based containers
        if (container is "mp4" or "mov" or "webm" or "mkv")
        {
            return mediaData.OptimizedForStreaming ? ReleaseSource.WebDl : ReleaseSource.WebRip;
        }

        // Fallback
        return ReleaseSource.WebRip;
    }
}
