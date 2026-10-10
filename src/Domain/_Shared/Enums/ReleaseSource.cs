// ReSharper disable InconsistentNaming

namespace Reaparr.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleaseSource
{
    /// <summary>
    /// Unknown or unspecified release source.
    /// </summary>
    [JsonStringEnumMemberName(nameof(None))]
    None = 0,

    /// <summary>
    /// Release sourced from a Blu-ray disc, typically re-encoded from the original
    /// Blu-ray video and audio streams.
    /// </summary>
    [JsonStringEnumMemberName("BluRay")]
    BluRay = 1,

    /// <summary>
    /// Bit-exact remux of a Blu-ray disc, where the original video and audio streams
    /// are extracted and repackaged into a different container without re-encoding.
    /// </summary>
    [JsonStringEnumMemberName("BluRay Remux")]
    BluRayRemux = 2,

    /// <summary>
    /// Direct download from a streaming service, using the provider’s original
    /// encoded files without capture or re-encoding.
    /// </summary>
    [JsonStringEnumMemberName("WEB-DL")]
    WebDl = 3,

    /// <summary>
    /// Re-encoded capture of a streaming service playback, typically involving
    /// quality loss compared to a WEB-DL source.
    /// </summary>
    [JsonStringEnumMemberName("WEBRip")]
    WebRip = 4,

    /// <summary>
    /// Release sourced from a DVD, usually re-encoded from MPEG-2 disc video and
    /// audio streams.
    /// </summary>
    [JsonStringEnumMemberName("DVD")]
    DVD = 5,

    /// <summary>
    /// Capture from a television broadcast source, commonly involving re-encoding
    /// and potential edits such as commercials removal.
    /// </summary>
    [JsonStringEnumMemberName("HDTV")]
    HDTV = 6,
}
