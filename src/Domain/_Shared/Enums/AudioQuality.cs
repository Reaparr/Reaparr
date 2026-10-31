// ReSharper disable InconsistentNaming

namespace Reaparr.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AudioQuality
{
    [JsonStringEnumMemberName(nameof(None))]
    None = -1,

    [JsonStringEnumMemberName(nameof(Unknown))]
    Unknown = 0,

    [JsonStringEnumMemberName(nameof(MP3_Below96Kbps))]
    MP3_Below96Kbps = 1,

    [JsonStringEnumMemberName(nameof(MP3_96Kbps))]
    MP3_96Kbps = 2,

    [JsonStringEnumMemberName(nameof(MP3_128Kbps))]
    MP3_128Kbps = 3,

    [JsonStringEnumMemberName(nameof(MP3_160Kbps))]
    MP3_160Kbps = 4,

    [JsonStringEnumMemberName(nameof(MP3_192Kbps))]
    MP3_192Kbps = 5,

    [JsonStringEnumMemberName(nameof(MP3_256Kbps))]
    MP3_256Kbps = 6,

    [JsonStringEnumMemberName(nameof(MP3_320KbpsPlus))]
    MP3_320KbpsPlus = 7,

    [JsonStringEnumMemberName(nameof(AAC_Below96Kbps))]
    AAC_Below96Kbps = 8,

    [JsonStringEnumMemberName(nameof(AAC_96Kbps))]
    AAC_96Kbps = 9,

    [JsonStringEnumMemberName(nameof(AAC_128Kbps))]
    AAC_128Kbps = 10,

    [JsonStringEnumMemberName(nameof(AAC_160Kbps))]
    AAC_160Kbps = 11,

    [JsonStringEnumMemberName(nameof(AAC_192Kbps))]
    AAC_192Kbps = 12,

    [JsonStringEnumMemberName(nameof(AAC_256Kbps))]
    AAC_256Kbps = 13,

    [JsonStringEnumMemberName(nameof(AAC_320KbpsPlus))]
    AAC_320KbpsPlus = 14,

    [JsonStringEnumMemberName(nameof(Vorbis_Below96Kbps))]
    Vorbis_Below96Kbps = 15,

    [JsonStringEnumMemberName(nameof(Vorbis_96Kbps))]
    Vorbis_96Kbps = 16,

    [JsonStringEnumMemberName(nameof(Vorbis_128Kbps))]
    Vorbis_128Kbps = 17,

    [JsonStringEnumMemberName(nameof(Vorbis_160Kbps))]
    Vorbis_160Kbps = 18,

    [JsonStringEnumMemberName(nameof(Vorbis_192Kbps))]
    Vorbis_192Kbps = 19,

    [JsonStringEnumMemberName(nameof(Vorbis_256Kbps))]
    Vorbis_256Kbps = 20,

    [JsonStringEnumMemberName(nameof(Vorbis_320KbpsPlus))]
    Vorbis_320KbpsPlus = 21,

    [JsonStringEnumMemberName(nameof(Opus_Below96Kbps))]
    Opus_Below96Kbps = 22,

    [JsonStringEnumMemberName(nameof(Opus_96Kbps))]
    Opus_96Kbps = 23,

    [JsonStringEnumMemberName(nameof(Opus_128Kbps))]
    Opus_128Kbps = 24,

    [JsonStringEnumMemberName(nameof(Opus_160Kbps))]
    Opus_160Kbps = 25,

    [JsonStringEnumMemberName(nameof(Opus_192Kbps))]
    Opus_192Kbps = 26,

    [JsonStringEnumMemberName(nameof(Opus_256Kbps))]
    Opus_256Kbps = 27,

    [JsonStringEnumMemberName(nameof(Opus_320KbpsPlus))]
    Opus_320KbpsPlus = 28,

    [JsonStringEnumMemberName(nameof(FLAC_16Bit_44_1kHz))]
    FLAC_16Bit_44_1kHz = 29,

    [JsonStringEnumMemberName(nameof(FLAC_16Bit_48kHz))]
    FLAC_16Bit_48kHz = 30,

    [JsonStringEnumMemberName(nameof(FLAC_16Bit_88_2kHz))]
    FLAC_16Bit_88_2kHz = 31,

    [JsonStringEnumMemberName(nameof(FLAC_16Bit_96kHz))]
    FLAC_16Bit_96kHz = 32,

    [JsonStringEnumMemberName(nameof(FLAC_16Bit_176_4kHz))]
    FLAC_16Bit_176_4kHz = 33,

    [JsonStringEnumMemberName(nameof(FLAC_16Bit_192kHz))]
    FLAC_16Bit_192kHz = 34,

    [JsonStringEnumMemberName(nameof(FLAC_24Bit_44_1kHz))]
    FLAC_24Bit_44_1kHz = 35,

    [JsonStringEnumMemberName(nameof(FLAC_24Bit_48kHz))]
    FLAC_24Bit_48kHz = 36,

    [JsonStringEnumMemberName(nameof(FLAC_24Bit_88_2kHz))]
    FLAC_24Bit_88_2kHz = 37,

    [JsonStringEnumMemberName(nameof(FLAC_24Bit_96kHz))]
    FLAC_24Bit_96kHz = 38,

    [JsonStringEnumMemberName(nameof(FLAC_24Bit_176_4kHz))]
    FLAC_24Bit_176_4kHz = 39,

    [JsonStringEnumMemberName(nameof(FLAC_24Bit_192kHz))]
    FLAC_24Bit_192kHz = 40,

    [JsonStringEnumMemberName(nameof(ALAC_16Bit_44_1kHz))]
    ALAC_16Bit_44_1kHz = 41,

    [JsonStringEnumMemberName(nameof(ALAC_16Bit_48kHz))]
    ALAC_16Bit_48kHz = 42,

    [JsonStringEnumMemberName(nameof(ALAC_16Bit_88_2kHz))]
    ALAC_16Bit_88_2kHz = 43,

    [JsonStringEnumMemberName(nameof(ALAC_16Bit_96kHz))]
    ALAC_16Bit_96kHz = 44,

    [JsonStringEnumMemberName(nameof(ALAC_16Bit_176_4kHz))]
    ALAC_16Bit_176_4kHz = 45,

    [JsonStringEnumMemberName(nameof(ALAC_16Bit_192kHz))]
    ALAC_16Bit_192kHz = 46,

    [JsonStringEnumMemberName(nameof(ALAC_24Bit_44_1kHz))]
    ALAC_24Bit_44_1kHz = 47,

    [JsonStringEnumMemberName(nameof(ALAC_24Bit_48kHz))]
    ALAC_24Bit_48kHz = 48,

    [JsonStringEnumMemberName(nameof(ALAC_24Bit_88_2kHz))]
    ALAC_24Bit_88_2kHz = 49,

    [JsonStringEnumMemberName(nameof(ALAC_24Bit_96kHz))]
    ALAC_24Bit_96kHz = 50,

    [JsonStringEnumMemberName(nameof(ALAC_24Bit_176_4kHz))]
    ALAC_24Bit_176_4kHz = 51,

    [JsonStringEnumMemberName(nameof(ALAC_24Bit_192kHz))]
    ALAC_24Bit_192kHz = 52,

    [JsonStringEnumMemberName(nameof(PCM_16Bit_44_1kHz))]
    PCM_16Bit_44_1kHz = 53,

    [JsonStringEnumMemberName(nameof(PCM_16Bit_48kHz))]
    PCM_16Bit_48kHz = 54,

    [JsonStringEnumMemberName(nameof(PCM_16Bit_88_2kHz))]
    PCM_16Bit_88_2kHz = 55,

    [JsonStringEnumMemberName(nameof(PCM_16Bit_96kHz))]
    PCM_16Bit_96kHz = 56,

    [JsonStringEnumMemberName(nameof(PCM_16Bit_176_4kHz))]
    PCM_16Bit_176_4kHz = 57,

    [JsonStringEnumMemberName(nameof(PCM_16Bit_192kHz))]
    PCM_16Bit_192kHz = 58,

    [JsonStringEnumMemberName(nameof(PCM_24Bit_44_1kHz))]
    PCM_24Bit_44_1kHz = 59,

    [JsonStringEnumMemberName(nameof(PCM_24Bit_48kHz))]
    PCM_24Bit_48kHz = 60,

    [JsonStringEnumMemberName(nameof(PCM_24Bit_88_2kHz))]
    PCM_24Bit_88_2kHz = 61,

    [JsonStringEnumMemberName(nameof(PCM_24Bit_96kHz))]
    PCM_24Bit_96kHz = 62,

    [JsonStringEnumMemberName(nameof(PCM_24Bit_176_4kHz))]
    PCM_24Bit_176_4kHz = 63,

    [JsonStringEnumMemberName(nameof(PCM_24Bit_192kHz))]
    PCM_24Bit_192kHz = 64,
}
