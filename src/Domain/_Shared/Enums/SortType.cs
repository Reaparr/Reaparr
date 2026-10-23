namespace Reaparr.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortType
{
    [JsonStringEnumMemberName("title")]
    Title,

    [JsonStringEnumMemberName("year")]
    Year,

    [JsonStringEnumMemberName("addedAt")]
    AddedAt,

    [JsonStringEnumMemberName("updatedAt")]
    UpdatedAt,

    [JsonStringEnumMemberName("duration")]
    Duration,

    [JsonStringEnumMemberName("mediaSize")]
    MediaSize,

    [JsonStringEnumMemberName("videoQuality")]
    VideoQuality,

    [JsonStringEnumMemberName("audioQuality")]
    AudioQuality,

    [JsonStringEnumMemberName("comparisonState")]
    ComparisonState,
}
