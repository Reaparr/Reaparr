namespace Reaparr.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlexMediaType
{
    // Keep persisted numeric identities stable; value 6 was removed with Artist.
    [JsonStringEnumMemberName(nameof(None))]
    None = 0,

    [JsonStringEnumMemberName(nameof(Movie))]
    Movie = 1,

    [JsonStringEnumMemberName(nameof(TvShow))]
    TvShow = 2,

    [JsonStringEnumMemberName(nameof(Season))]
    Season = 3,

    [JsonStringEnumMemberName(nameof(Episode))]
    Episode = 4,

    [JsonStringEnumMemberName(nameof(Music))]
    Music = 5,

    [JsonStringEnumMemberName(nameof(Album))]
    Album = 7,

    [JsonStringEnumMemberName(nameof(Track))]
    Track = 8,

    [JsonStringEnumMemberName(nameof(PhotoAlbum))]
    PhotoAlbum = 9,

    [JsonStringEnumMemberName(nameof(Photos))]
    Photos = 10,

    [JsonStringEnumMemberName(nameof(OtherVideos))]
    OtherVideos = 11,

    [JsonStringEnumMemberName(nameof(Games))]
    Games = 12,

    [JsonStringEnumMemberName(nameof(Unknown))]
    Unknown = 13,
}
