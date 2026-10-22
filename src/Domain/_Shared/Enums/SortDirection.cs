namespace Reaparr.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    [JsonStringEnumMemberName("asc")]
    Asc = 0,

    [JsonStringEnumMemberName("desc")]
    Desc = 1,

    [JsonStringEnumMemberName("no-sort")]
    NoSort = 2,
}
