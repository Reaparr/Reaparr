namespace Reaparr.Domain;

public static class AudioQualityExtensions
{
    public static AudioQuality ClassifyAudioQuality(this PlexMusicTrackMediaData file)
    {
        var codec = Normalize(file.AudioCodec);

        if (IsLossyCodec(codec))
        {
            if (file.AudioBitrateKbps is not > 0)
                return AudioQuality.Unknown;

            return ClassifyLossy(codec, file.AudioBitrateKbps.Value);
        }

        if (IsLosslessCodec(codec))
        {
            if (file.AudioBitDepth is not > 0 || file.AudioSampleRateHz is not > 0)
                return AudioQuality.Unknown;

            return ClassifyLossless(codec, file.AudioBitDepth.Value, file.AudioSampleRateHz.Value);
        }

        return AudioQuality.Unknown;
    }

    public static bool IsHigherAudioQualityThan(this PlexMusicTrackMediaData file, PlexMusicTrackMediaData other)
    {
        var fileQuality = file.ClassifyAudioQuality();
        var otherQuality = other.ClassifyAudioQuality();
        if (fileQuality is AudioQuality.None or AudioQuality.Unknown)
            return false;
        if (otherQuality is AudioQuality.None or AudioQuality.Unknown)
            return false;

        var fileCodec = Normalize(file.AudioCodec);
        var otherCodec = Normalize(other.AudioCodec);

        if (IsLossyCodec(fileCodec) && IsLossyCodec(otherCodec))
        {
            return fileCodec.Equals(otherCodec, StringComparison.Ordinal)
                && AreCompatibleEncodingProperties(file, other, fileCodec, otherCodec)
                && file.AudioBitrateKbps > other.AudioBitrateKbps;
        }

        if (IsLosslessCodec(fileCodec) && IsLosslessCodec(otherCodec))
        {
            if (!AreCompatibleEncodingProperties(file, other, fileCodec, otherCodec))
                return false;

            return file.AudioBitDepth >= other.AudioBitDepth
                && file.AudioSampleRateHz >= other.AudioSampleRateHz
                && (file.AudioBitDepth > other.AudioBitDepth || file.AudioSampleRateHz > other.AudioSampleRateHz);
        }

        return IsLosslessCodec(fileCodec)
            && IsLossyCodec(otherCodec)
            && AreCompatibleEncodingProperties(file, other, fileCodec, otherCodec);
    }

    public static bool IsEquivalentAudioQualityTo(this PlexMusicTrackMediaData file, PlexMusicTrackMediaData other)
    {
        var fileQuality = file.ClassifyAudioQuality();
        var otherQuality = other.ClassifyAudioQuality();
        if (fileQuality is AudioQuality.None or AudioQuality.Unknown)
            return false;
        if (otherQuality is AudioQuality.None or AudioQuality.Unknown)
            return false;

        var fileCodec = Normalize(file.AudioCodec);
        var otherCodec = Normalize(other.AudioCodec);
        if (!AreCompatibleEncodingProperties(file, other, fileCodec, otherCodec))
            return false;

        if (IsLossyCodec(fileCodec) && IsLossyCodec(otherCodec))
        {
            return fileCodec.Equals(otherCodec, StringComparison.Ordinal)
                && file.AudioBitrateKbps == other.AudioBitrateKbps;
        }

        if (IsLosslessCodec(fileCodec) && IsLosslessCodec(otherCodec))
        {
            return file.AudioBitDepth == other.AudioBitDepth && file.AudioSampleRateHz == other.AudioSampleRateHz;
        }

        return false;
    }

    public static PlexMusicTrackMediaData? GetBestAudioFile(this IEnumerable<PlexMusicTrackMediaData> files)
    {
        PlexMusicTrackMediaData? best = null;
        foreach (var candidate in files)
        {
            if (
                best is null
                || candidate.IsHigherAudioQualityThan(best)
                || (
                    candidate.IsEquivalentAudioQualityTo(best)
                    && (
                        (int)candidate.Source,
                        candidate.PlexApiMediaId,
                        candidate.PlexApiPartId,
                        candidate.Id
                    ).CompareTo(((int)best.Source, best.PlexApiMediaId, best.PlexApiPartId, best.Id)) < 0
                )
            )
                best = candidate;
        }

        if (best is null)
            return null;

        foreach (var candidate in files)
            if (
                !ReferenceEquals(best, candidate)
                && !best.IsHigherAudioQualityThan(candidate)
                && !best.IsEquivalentAudioQualityTo(candidate)
            )
                return null;

        return best;
    }

    private static bool AreCompatibleEncodingProperties(
        PlexMusicTrackMediaData file,
        PlexMusicTrackMediaData other,
        string fileCodec,
        string otherCodec
    )
    {
        if (file.AudioSampleRateHz is not > 0 || other.AudioSampleRateHz is not > 0)
            return false;
        if (file.AudioChannels is not > 0 || other.AudioChannels is not > 0)
            return false;
        if (
            file.AudioSampleRateHz != other.AudioSampleRateHz
            && !(IsLosslessCodec(fileCodec) && IsLosslessCodec(otherCodec))
        )
            return false;
        if (file.AudioChannels != other.AudioChannels)
            return false;

        var fileLayout = Normalize(file.AudioChannelLayout);
        var otherLayout = Normalize(other.AudioChannelLayout);
        if (fileLayout.Length == 0 || otherLayout.Length == 0 || fileLayout != otherLayout)
            return false;

        if (fileCodec.Equals("aac", StringComparison.Ordinal) || otherCodec.Equals("aac", StringComparison.Ordinal))
        {
            if (fileCodec.Equals("aac", StringComparison.Ordinal) && otherCodec.Equals("aac", StringComparison.Ordinal))
                return ProfilesMatch(file.AudioProfile, other.AudioProfile);

            var aacProfile = fileCodec.Equals("aac", StringComparison.Ordinal) ? file.AudioProfile : other.AudioProfile;
            return Normalize(aacProfile).Length > 0;
        }

        return Normalize(file.AudioProfile) == Normalize(other.AudioProfile);
    }

    private static bool ProfilesMatch(string? fileProfile, string? otherProfile)
    {
        var first = Normalize(fileProfile);
        var second = Normalize(otherProfile);
        return first.Length > 0 && first == second;
    }

    private static AudioQuality ClassifyLossy(string codec, int bitrateKbps) =>
        codec switch
        {
            "mp3" => ClassifyLossyBand(
                bitrateKbps,
                AudioQuality.MP3_Below96Kbps,
                AudioQuality.MP3_96Kbps,
                AudioQuality.MP3_128Kbps,
                AudioQuality.MP3_160Kbps,
                AudioQuality.MP3_192Kbps,
                AudioQuality.MP3_256Kbps,
                AudioQuality.MP3_320KbpsPlus
            ),
            "aac" => ClassifyLossyBand(
                bitrateKbps,
                AudioQuality.AAC_Below96Kbps,
                AudioQuality.AAC_96Kbps,
                AudioQuality.AAC_128Kbps,
                AudioQuality.AAC_160Kbps,
                AudioQuality.AAC_192Kbps,
                AudioQuality.AAC_256Kbps,
                AudioQuality.AAC_320KbpsPlus
            ),
            "vorbis" => ClassifyLossyBand(
                bitrateKbps,
                AudioQuality.Vorbis_Below96Kbps,
                AudioQuality.Vorbis_96Kbps,
                AudioQuality.Vorbis_128Kbps,
                AudioQuality.Vorbis_160Kbps,
                AudioQuality.Vorbis_192Kbps,
                AudioQuality.Vorbis_256Kbps,
                AudioQuality.Vorbis_320KbpsPlus
            ),
            "opus" => ClassifyLossyBand(
                bitrateKbps,
                AudioQuality.Opus_Below96Kbps,
                AudioQuality.Opus_96Kbps,
                AudioQuality.Opus_128Kbps,
                AudioQuality.Opus_160Kbps,
                AudioQuality.Opus_192Kbps,
                AudioQuality.Opus_256Kbps,
                AudioQuality.Opus_320KbpsPlus
            ),
            _ => AudioQuality.Unknown,
        };

    private static AudioQuality ClassifyLossyBand(
        int bitrateKbps,
        AudioQuality below96,
        AudioQuality at96,
        AudioQuality at128,
        AudioQuality at160,
        AudioQuality at192,
        AudioQuality at256,
        AudioQuality at320
    ) =>
        bitrateKbps switch
        {
            < 96 => below96,
            < 128 => at96,
            < 160 => at128,
            < 192 => at160,
            < 256 => at192,
            < 320 => at256,
            _ => at320,
        };

    private static AudioQuality ClassifyLossless(string codec, int bitDepth, int sampleRateHz) =>
        codec switch
        {
            "flac" => ClassifyLosslessPair(
                bitDepth,
                sampleRateHz,
                AudioQuality.FLAC_16Bit_44_1kHz,
                AudioQuality.FLAC_16Bit_48kHz,
                AudioQuality.FLAC_16Bit_88_2kHz,
                AudioQuality.FLAC_16Bit_96kHz,
                AudioQuality.FLAC_16Bit_176_4kHz,
                AudioQuality.FLAC_16Bit_192kHz,
                AudioQuality.FLAC_24Bit_44_1kHz,
                AudioQuality.FLAC_24Bit_48kHz,
                AudioQuality.FLAC_24Bit_88_2kHz,
                AudioQuality.FLAC_24Bit_96kHz,
                AudioQuality.FLAC_24Bit_176_4kHz,
                AudioQuality.FLAC_24Bit_192kHz
            ),
            "alac" => ClassifyLosslessPair(
                bitDepth,
                sampleRateHz,
                AudioQuality.ALAC_16Bit_44_1kHz,
                AudioQuality.ALAC_16Bit_48kHz,
                AudioQuality.ALAC_16Bit_88_2kHz,
                AudioQuality.ALAC_16Bit_96kHz,
                AudioQuality.ALAC_16Bit_176_4kHz,
                AudioQuality.ALAC_16Bit_192kHz,
                AudioQuality.ALAC_24Bit_44_1kHz,
                AudioQuality.ALAC_24Bit_48kHz,
                AudioQuality.ALAC_24Bit_88_2kHz,
                AudioQuality.ALAC_24Bit_96kHz,
                AudioQuality.ALAC_24Bit_176_4kHz,
                AudioQuality.ALAC_24Bit_192kHz
            ),
            "pcm" => ClassifyLosslessPair(
                bitDepth,
                sampleRateHz,
                AudioQuality.PCM_16Bit_44_1kHz,
                AudioQuality.PCM_16Bit_48kHz,
                AudioQuality.PCM_16Bit_88_2kHz,
                AudioQuality.PCM_16Bit_96kHz,
                AudioQuality.PCM_16Bit_176_4kHz,
                AudioQuality.PCM_16Bit_192kHz,
                AudioQuality.PCM_24Bit_44_1kHz,
                AudioQuality.PCM_24Bit_48kHz,
                AudioQuality.PCM_24Bit_88_2kHz,
                AudioQuality.PCM_24Bit_96kHz,
                AudioQuality.PCM_24Bit_176_4kHz,
                AudioQuality.PCM_24Bit_192kHz
            ),
            _ => AudioQuality.Unknown,
        };

    private static AudioQuality ClassifyLosslessPair(
        int bitDepth,
        int sampleRateHz,
        AudioQuality bit16Rate44100,
        AudioQuality bit16Rate48000,
        AudioQuality bit16Rate88200,
        AudioQuality bit16Rate96000,
        AudioQuality bit16Rate176400,
        AudioQuality bit16Rate192000,
        AudioQuality bit24Rate44100,
        AudioQuality bit24Rate48000,
        AudioQuality bit24Rate88200,
        AudioQuality bit24Rate96000,
        AudioQuality bit24Rate176400,
        AudioQuality bit24Rate192000
    ) =>
        (bitDepth, sampleRateHz) switch
        {
            (16, 44100) => bit16Rate44100,
            (16, 48000) => bit16Rate48000,
            (16, 88200) => bit16Rate88200,
            (16, 96000) => bit16Rate96000,
            (16, 176400) => bit16Rate176400,
            (16, 192000) => bit16Rate192000,
            (24, 44100) => bit24Rate44100,
            (24, 48000) => bit24Rate48000,
            (24, 88200) => bit24Rate88200,
            (24, 96000) => bit24Rate96000,
            (24, 176400) => bit24Rate176400,
            (24, 192000) => bit24Rate192000,
            _ => AudioQuality.Unknown,
        };

    private static bool IsLossyCodec(string codec) => codec is "mp3" or "aac" or "vorbis" or "opus";

    private static bool IsLosslessCodec(string codec) => codec is "flac" or "alac" or "pcm";

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
