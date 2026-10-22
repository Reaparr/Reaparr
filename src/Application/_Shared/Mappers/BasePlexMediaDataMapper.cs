using System.Text;
using System.Text.RegularExpressions;

namespace Reaparr.Application;

public static class BasePlexMediaDataMapper
{
    private static readonly ILogger _log = LogFactory.Create(typeof(BasePlexMediaDataMapper));

    /// <summary>
    /// Adds missing stream data to movie and TV media files and generates a Sonarr/Radarr-compatible filename.
    /// </summary>
    /// <returns><see langword="true"/> when metadata was generated and applied; otherwise <see langword="false"/> when a video stream was unavailable.</returns>
    public static bool UpdateStreamMetadata(
        this PlexMovieMediaData mediaDataDb,
        LibraryMediaItemDTO metaDataItem,
        LibraryMediaItemMediaDTO mediaItem,
        LibraryMediaItemPartDTO partItem
    )
    {
        var filename = GenerateStreamFilename(mediaDataDb, metaDataItem, mediaItem, partItem);
        if (filename is null)
            return false;

        mediaDataDb.GeneratedFilename = filename;
        mediaDataDb.NeedsGeneratedName = false;
        mediaDataDb.GeneratedNameSyncedAt = DateTime.UtcNow;
        return true;
    }

    public static bool UpdateStreamMetadata(
        this PlexTvShowEpisodeMediaData mediaDataDb,
        LibraryMediaItemDTO metaDataItem,
        LibraryMediaItemMediaDTO mediaItem,
        LibraryMediaItemPartDTO partItem
    )
    {
        var filename = GenerateStreamFilename(mediaDataDb, metaDataItem, mediaItem, partItem);
        if (filename is null)
            return false;

        mediaDataDb.GeneratedFilename = filename;
        mediaDataDb.NeedsGeneratedName = false;
        mediaDataDb.GeneratedNameSyncedAt = DateTime.UtcNow;
        return true;
    }

    private static string? GenerateStreamFilename(
        BasePlexMediaData mediaDataDb,
        LibraryMediaItemDTO metaDataItem,
        LibraryMediaItemMediaDTO mediaItem,
        LibraryMediaItemPartDTO partItem
    )
    {
        // Extract video stream info. Plex may mark an audio stream as default, so only a
        // default video stream may be selected; otherwise preserve the first-video fallback.
        var videoStream =
            partItem.Stream.FirstOrDefault(s => s is { StreamType: StreamType.Video, Default: true })
            ?? partItem.Stream.FirstOrDefault(s => s.StreamType == StreamType.Video);
        if (videoStream is null)
        {
            _log.Here()
                .Warning(
                    "Unable to find video stream for partID: {PartId} with filename: {MediaFileName}",
                    mediaDataDb.Id,
                    mediaDataDb.GetFileName
                );
            return null;
        }

        // Map codecs
        var videoCodec = mediaItem.VideoCodec.MapVideoCodec();
        var audioCodec = mediaItem.AudioCodec.MapAudioCodec(mediaItem.AudioProfile);
        var audioLayout = mediaItem.AudioChannels.MapToFormattedChannels();
        var languageFormat = partItem.Stream.FormatLanguage();

        // Generate the release name; the concrete file entity owns enrichment state.
        return GenerateReleaseName(
            title: metaDataItem.Title,
            year: metaDataItem.Year,
            videoResolution: mediaItem.VideoResolution,
            videoCodec: videoCodec,
            audioCodec: audioCodec,
            videoStream: videoStream,
            audioLayout: audioLayout,
            languageFormat: languageFormat,
            source: mediaDataDb.Source,
            isRemux: mediaDataDb.Source == ReleaseSource.BluRayRemux
        );

    }

    /// <summary>
    /// Generates a Radarr/Sonarr-compatible scene-style release name from Plex movie metadata.
    /// Format: Title.Year.Resolution.Source.[REMUX].VideoCodec.AudioCodec.Channels[.Language]
    /// </summary>
    public static string GenerateReleaseName(
        string title,
        int year,
        VideoQuality videoResolution,
        string videoCodec,
        string audioCodec,
        LibraryMediaItemStreamDTO videoStream,
        string audioLayout,
        string languageFormat,
        ReleaseSource source,
        bool isRemux
    )
    {
        // Detect HDR tokens from video stream
        var hdrTokens = DetectHdrTokens(videoStream);

        // Sanitize title
        var sanitizedTitle = SanitizeTitle(title);

        // Build release name: Title.Year.Resolution.Source.[REMUX].[HDR/DV].[VideoCodec].AudioCodec.Channels[.Language]
        // Note: VideoCodec is omitted for REMUX releases as remuxes don't imply re-encoding
        var releaseName = new StringBuilder();
        releaseName.Append(sanitizedTitle);
        releaseName.Append('.').Append(year);

        // Only append resolution label if it's not empty
        var resolutionLabel = videoResolution.ToResolutionLabel();
        if (!string.IsNullOrWhiteSpace(resolutionLabel))
        {
            releaseName.Append('.').Append(resolutionLabel);
        }

        if (source.ToFileNameSpec() is not null)
            releaseName.Append('.').Append(source.ToFileNameSpec());

        if (isRemux)
            releaseName.Append('.').Append("REMUX");

        // Add HDR tokens after REMUX (if present) and before video codec
        if (hdrTokens.Count > 0)
        {
            foreach (var token in hdrTokens)
                releaseName.Append('.').Append(token);
        }

        // Only include video codec for non-REMUX releases
        if (!isRemux)
            releaseName.Append('.').Append(videoCodec);

        releaseName.Append('.').Append(audioCodec);
        releaseName.Append('.').Append(audioLayout);

        if (!string.IsNullOrEmpty(languageFormat))
            releaseName.Append('.').Append(languageFormat);

        return releaseName.ToString();
    }

    private static string SanitizeTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(title.Length);

        foreach (var c in title)
        {
            // Replace invalid filename characters and spaces with dots
            if (invalidChars.Contains(c) || char.IsWhiteSpace(c))
            {
                sanitized.Append('.');
            }
            else
            {
                sanitized.Append(c);
            }
        }

        // Replace multiple consecutive dots with a single dot
        var result = Regex.Replace(sanitized.ToString(), @"\.+", ".");

        // Remove dots from start and end
        result = result.Trim('.');

        return result;
    }

    /// <summary>
    /// Detects HDR tokens from video stream metadata.
    /// Only returns tokens when explicitly detected - never infers from bit depth alone.
    /// Returns the list of tokens in order: DV (if present), then HDR/HDR10+ (if present).
    /// </summary>
    private static List<string> DetectHdrTokens(LibraryMediaItemStreamDTO? videoStream)
    {
        var tokens = new List<string>();

        if (videoStream == null)
        {
            return tokens;
        }

        var colorPrimaries = videoStream.ColorPrimaries?.ToLowerInvariant() ?? string.Empty;
        var colorTrc = videoStream.ColorTrc?.ToLowerInvariant() ?? string.Empty;
        var profile = videoStream.Profile?.ToLowerInvariant() ?? string.Empty;
        var hasDolbyVision = videoStream.DOVIPresent == true;

        // Detect Dolby Vision
        var hasDv = hasDolbyVision || profile.Contains("dv", StringComparison.OrdinalIgnoreCase);

        // Detect HDR10/HDR10+
        // Require BT.2020 color primaries and PQ transfer function (SMPTE 2084) for reliable HDR detection
        var hasBt2020 =
            colorPrimaries == "bt2020" || colorPrimaries.Contains("bt.2020", StringComparison.OrdinalIgnoreCase);
        var hasPq = colorTrc == "smpte2084" || colorTrc.Contains("pq", StringComparison.OrdinalIgnoreCase);
        var hasHdrProfile = profile.Contains("hdr", StringComparison.OrdinalIgnoreCase);
        var hasHdr10Plus = profile.Contains("hdr10+", StringComparison.OrdinalIgnoreCase);

        // HDR detection: require explicit signals (BT.2020 + PQ) or HDR in profile
        // Do NOT infer from bit depth alone
        var hasHdr = (hasBt2020 && hasPq) || hasHdrProfile;

        // Build tokens in the correct order
        if (hasDv)
        {
            tokens.Add("DV");

            // DV.HDR if both Dolby Vision and HDR10 fallback are present
            if (hasHdr && !hasHdr10Plus)
            {
                tokens.Add("HDR");
            }
        }
        else if (hasHdr)
        {
            // HDR10+ takes precedence over HDR10
            if (hasHdr10Plus)
            {
                tokens.Add("HDR10+");
            }
            else
            {
                tokens.Add("HDR");
            }
        }

        return tokens;
    }
}
