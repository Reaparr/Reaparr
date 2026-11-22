using System.Text.RegularExpressions;

namespace Reaparr.Application;

public static partial class DashOutputFileNameCleaner
{
    private static readonly HashSet<string> _noiseTokens =
    [
        "webdl",
        "web-dl",
        "webrip",
        "web-rip",
        "bluray",
        "blu-ray",
        "bdrip",
        "brrip",
        "hdrip",
        "hdtv",
        "dvdrip",
        "dvd",
        "remux",
        "uhd",
        "x264",
        "x265",
        "h264",
        "h265",
        "hevc",
        "avc",
        "av1",
        "vp9",
        "xvid",
        "divx",
        "dts",
        "dts-hd",
        "truehd",
        "aac",
        "ac3",
        "eac3",
        "dd",
        "ddp",
        "flac",
        "mp3",
        "atmos",
        "proper",
        "repack",
        "rerip",
        "retail",
        "multi",
        "unrated",
        "remastered",
        "hybrid",
        "subs",
        "hdr",
        "hdr10",
        "hdr10+",
        "dv",
        "sbs",
        "ou",
    ];

    public static string NormalizeForDashOutput(string originalFileName, VideoQuality quality)
    {
        var input = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(input))
            return $"download.WEB-DL.{GetQualityToken(quality)}.mkv";

        var idTokens = ExtractIdTokens(input);
        var withoutIds = IdTokenRegex().Replace(input, " ");
        withoutIds = EmbeddedChannelTokenRegex().Replace(withoutIds, " ");
        withoutIds = ChannelTokenRegex().Replace(withoutIds, " ");
        var tokenCandidates = SplitTokenRegex()
            .Split(withoutIds)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();

        var identityTokens = BuildIdentityTokens(tokenCandidates);
        if (identityTokens.Count == 0)
            identityTokens.Add("download");

        identityTokens.AddRange(idTokens);

        var qualityToken = GetQualityToken(quality);
        var resultTokens = identityTokens
            .Concat(["WEB-DL", qualityToken])
            .Select(SanitizeToken)
            .Where(token => !string.IsNullOrWhiteSpace(token));

        return string.Join('.', resultTokens) + ".mkv";
    }

    private static List<string> BuildIdentityTokens(List<string> tokenCandidates)
    {
        var identity = new List<string>();

        for (var index = 0; index < tokenCandidates.Count; index++)
        {
            var token = tokenCandidates[index];

            if (ShouldDropToken(token))
                continue;

            if (index < tokenCandidates.Count - 1 && ShouldDropTokenPair(token, tokenCandidates[index + 1]))
            {
                index++;
                continue;
            }

            identity.Add(token);
        }

        var firstYearIndex = identity.FindIndex(IsYearToken);
        if (firstYearIndex >= 0)
        {
            var lastEpisodeIndex = -1;
            for (var index = identity.Count - 1; index > firstYearIndex; index--)
            {
                if (!IsEpisodeToken(identity[index]))
                    continue;

                lastEpisodeIndex = index;
                break;
            }

            if (lastEpisodeIndex >= 0)
                return identity.Take(lastEpisodeIndex + 1).ToList();

            return identity.Take(firstYearIndex + 1).ToList();
        }

        return identity;
    }

    private static bool ShouldDropToken(string token)
    {
        var normalized = token.ToLowerInvariant();

        if (_noiseTokens.Contains(normalized))
            return true;

        if (QualityTokenRegex().IsMatch(normalized))
            return true;

        if (ChannelTokenRegex().IsMatch(normalized))
            return true;

        return false;
    }

    private static bool ShouldDropTokenPair(string firstToken, string secondToken)
    {
        var combined = string.Concat(firstToken, secondToken).ToLowerInvariant();
        return _noiseTokens.Contains(combined);
    }

    private static bool IsYearToken(string token) =>
        token.Length == 4 && int.TryParse(token, out var year) && year is >= 1900 and <= 2099;

    private static bool IsEpisodeToken(string token) => EpisodeTokenRegex().IsMatch(token);

    private static string GetQualityToken(VideoQuality quality)
    {
        var qualityToken = quality.ToResolutionLabel();
        return string.IsNullOrWhiteSpace(qualityToken) ? "1080p" : qualityToken;
    }

    private static List<string> ExtractIdTokens(string value)
    {
        var idTokens = new List<string>();

        foreach (Match match in IdTokenRegex().Matches(value))
        {
            var token = match.Value.Trim();
            token = token.Trim('[', ']', '(', ')', '{', '}');
            token = token.Replace(' ', '-');
            token = token.Replace(':', '-');

            if (!string.IsNullOrWhiteSpace(token))
                idTokens.Add(token);
        }

        return idTokens;
    }

    private static string SanitizeToken(string token)
    {
        var cleaned = token.Trim();
        cleaned = InvalidFileNameCharsRegex().Replace(cleaned, string.Empty);
        cleaned = cleaned.Replace(' ', '.');
        cleaned = Regex.Replace(cleaned, "\\.+", ".", RegexOptions.None, TimeSpan.FromSeconds(1));
        return cleaned.Trim('.');
    }

    [GeneratedRegex(
        @"(?:\[|\(|\{)?(?:imdb\s*[-:]\s*tt\d+|tmdb\s*[-:]\s*\d+|tvdb\s*[-:]\s*\d+)(?:\]|\)|\})?",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex IdTokenRegex();

    [GeneratedRegex(@"[\s._\-\[\]\(\)\{\}]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SplitTokenRegex();

    [GeneratedRegex(@"^(?:\d{3,4}p|4k|8k)$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QualityTokenRegex();

    [GeneratedRegex(
        @"^(?:s\d{1,2}e\d{1,3}(?:e\d{1,3})*|\d{1,2}x\d{1,3}|e\d{1,3})$",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex EpisodeTokenRegex();

    [GeneratedRegex("[<>:\"/\\\\|?*]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InvalidFileNameCharsRegex();

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?:(?:ddp?|eac3|ac3|aac|flac|truehd|dts(?:-hd)?)\s*\d\.\d(?:ch)?|\d\.\d(?:ch)?)(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex EmbeddedChannelTokenRegex();

    [GeneratedRegex(
        @"^(?:\d\.\d(?:ch)?|(?:ddp?|eac3|ac3|aac|flac|truehd|dts(?:-hd)?)\d\.\d(?:ch)?)$",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ChannelTokenRegex();
}
