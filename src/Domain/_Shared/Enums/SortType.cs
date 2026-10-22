namespace Reaparr.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortType
{
    [JsonStringEnumMemberName(nameof(Title))]
    Title,

    [JsonStringEnumMemberName(nameof(Year))]
    Year,

    [JsonStringEnumMemberName(nameof(AddedAt))]
    AddedAt,

    [JsonStringEnumMemberName(nameof(UpdatedAt))]
    UpdatedAt,

    [JsonStringEnumMemberName(nameof(Duration))]
    Duration,

    [JsonStringEnumMemberName(nameof(MediaSize))]
    MediaSize,

    [JsonStringEnumMemberName(nameof(VideoQuality))]
    VideoQuality,

    [JsonStringEnumMemberName(nameof(ComparisonState))]
    ComparisonState,
}
