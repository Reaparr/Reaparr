using System.Text.RegularExpressions;

namespace Reaparr.Application;

public static class MediaSpecMapper
{
    private static readonly ILogger _log = LogFactory.Create(typeof(MediaSpecMapper));

    public static string MapVideoCodec(this string codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
            return "Unknown";

        switch (codec.ToLowerInvariant())
        {
            case "av1":
                return "AV1";
            case "h264":
                return "x264";
            case "hevc":
                return "x265";
            case "mpeg4":
            case "msmpeg4v1":
            case "msmpeg4v2":
            case "msmpeg4v3":
                return "MPEG-4";
            case "mpeg2video":
            case "mpeg2":
                return "MPEG-2";
            case "mpeg1video":
            case "mpeg1":
                return "MPEG-1";
            case "vc1":
                return "VC-1";
            case "vp8":
                return "VP8";
            case "vp9":
                return "VP9";
            case "wmv1":
                return "WMV1";
            case "wmv2":
                return "WMV2";
            case "wmv3":
                return "WMV3";
            default:
                _log.Here().Warning("Unrecognized video codec: {Codec}", codec);
                return codec.CapitalizeFirst();
        }
    }

    public static string MapAudioCodec(this string codec, string? profile)
    {
        if (string.IsNullOrEmpty(codec))
        {
            return "Unknown";
        }

        var lowerCodec = codec.ToLowerInvariant();
        var lowerProfile = profile?.ToLowerInvariant() ?? string.Empty;

        // Check for DTS-HD MA variants
        if (lowerCodec == "dca" && lowerProfile.Contains("ma", StringComparison.OrdinalIgnoreCase))
        {
            return "DTS-HD.MA";
        }

        if (lowerCodec is "dts-hd ma" or "dts-hd.ma" or "dca-ma")
        {
            return "DTS-HD.MA";
        }

        if (lowerCodec is "dts-hd" or "dtshd")
        {
            return "DTS-HD";
        }

        switch (lowerCodec)
        {
            case "truehd":
                return "TrueHD";
            case "eac3":
                return "DDP";
            case "ac3":
                return "AC3";
            case "aac":
                return "AAC";
            case "dca":
            case "dts":
                return "DTS";
            case "mp3":
                return "MP3";
            case "flac":
                return "FLAC";
            case "opus":
                return "Opus";
            case "wmapro":
                return "WMA Pro";
            case "wmalossless":
                return "WMA Lossless";
            case "wmav1":
                return "WMA";
            case "wmav2":
                return "WMA";
            case "pcm":
                return "PCM";
            default:
                _log.Here().Warning("Unrecognized audio codec: {Codec}", codec);
                return codec.CapitalizeFirst();
        }
    }

    public static string MapToFormattedChannels(this int channels)
    {
        if (channels is < 0 or > 12)
            _log.Here().Warning("Unrecognized audio channel count: {Channels}", channels);

        return channels switch
        {
            0 => "",
            1 => "1.0",
            2 => "2.0",
            3 => "2.1",
            4 => "4.0",
            5 => "5.0",
            6 => "5.1",
            7 => "6.1",
            8 => "7.1",
            9 => "9.1",
            10 => "9.1",
            11 => "11.1",
            12 => "11.1",
            _ => $"{channels}.0",
        };
    }

    /// <summary>
    /// Maps a <see cref="ReleaseSource"/> to the source token used in generated release filenames
    /// (e.g. "Blu-ray", "WEB-DL", "WEBRip").
    ///
    /// Note that encoding characteristics such as REMUX are intentionally not included here;
    /// they are expressed as separate filename tokens to match Radarr/Sonarr parsing rules.
    /// </summary>
    public static string? ToFileNameSpec(this ReleaseSource source)
    {
        switch (source)
        {
            case ReleaseSource.BluRay:
            case ReleaseSource.BluRayRemux:
                return "BluRay";
            case ReleaseSource.WebDl:
                return "WEB-DL";
            case ReleaseSource.WebRip:
                return "WEBRip";
            case ReleaseSource.DVD:
                return "DVD";
            case ReleaseSource.HDTV:
                return "HDTV";
            default:
                _log.Here().Warning("Unrecognized release source: {Source}", source);
                return null;
        }
    }

    /// <summary>
    /// Format language (omit English, use MULTI if multiple non-English languages)
    /// </summary>
    /// <param name="streams"></param>
    public static string FormatLanguage(this List<LibraryMediaItemStreamDTO> streams)
    {
        var audioStreams = streams.Where(s => s.StreamType == StreamType.Audio).ToList();

        if (audioStreams.Count == 0)
            return string.Empty;

        // Extract all non-English language codes
        var nonEnglishLanguages = audioStreams
            .Select(a => a.LanguageCode)
            .Where(lang => !IsEnglish(lang))
            .Select(lang =>
            {
                var lowerLang = lang.ToLowerInvariant();
                var match = Regex.Match(lowerLang, @"^([a-z]{2,3})");
                return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
            })
            .Where(lang => !string.IsNullOrEmpty(lang))
            .Distinct()
            .ToList();

        if (nonEnglishLanguages.Count == 0)
            return string.Empty;

        // If multiple non-English languages exist, return MULTI
        if (nonEnglishLanguages.Count > 1)
            return "MULTI";

        // Single non-English language
        return nonEnglishLanguages[0];
    }

    private static bool IsEnglish(string lang)
    {
        if (string.IsNullOrEmpty(lang))
            return false;

        var lowerLang = lang.ToLowerInvariant();
        return lowerLang == "en" || lowerLang == "eng" || lowerLang == "english";
    }
}
