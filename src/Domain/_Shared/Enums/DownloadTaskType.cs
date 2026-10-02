namespace Reaparr.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DownloadTaskType
{
    // NOTE: Make sure the indexes are correct, 1,2,3,4,5 etc. and that there is no skip in between
    // Otherwise the TypeScript DTO translator in the front-end starts messing up
    [JsonStringEnumMemberName(nameof(None))]
    None = 0,

    /// <summary>
    /// Functions as a wrapper for a MovieData and MoviePart <see cref="DownloadTaskGeneric"/>.
    /// </summary>
    [JsonStringEnumMemberName(nameof(Movie))]
    Movie = 1,

    /// <summary>
    /// A movie of a particular quality or version
    /// Doc: https://support.plex.tv/articles/200381043-multi-version-movies/.
    /// </summary>
    [JsonStringEnumMemberName(nameof(MovieData))]
    MovieData = 2,

    /// <summary>
    /// A movie that consists of multiple file parts where each file part is a movie part.
    /// </summary>
    [JsonStringEnumMemberName(nameof(MoviePart))]
    MoviePart = 3,

    [JsonStringEnumMemberName(nameof(TvShow))]
    TvShow = 4,

    [JsonStringEnumMemberName(nameof(Season))]
    Season = 5,

    [JsonStringEnumMemberName(nameof(Episode))]
    Episode = 6,

    [JsonStringEnumMemberName(nameof(EpisodeData))]
    EpisodeData = 7,

    [JsonStringEnumMemberName(nameof(EpisodePart))]
    EpisodePart = 8,

    [JsonStringEnumMemberName(nameof(Artist))]
    Artist = 9,

    [JsonStringEnumMemberName(nameof(Album))]
    Album = 10,

    [JsonStringEnumMemberName(nameof(Track))]
    Track = 11,

    [JsonStringEnumMemberName(nameof(TrackData))]
    TrackData = 12,

    [JsonStringEnumMemberName(nameof(TrackPart))]
    TrackPart = 13,

    [JsonStringEnumMemberName(nameof(Photo))]
    Photo = 14,

    [JsonStringEnumMemberName(nameof(PhotoData))]
    PhotoData = 15,

    [JsonStringEnumMemberName(nameof(PhotoPart))]
    PhotoPart = 16,

    [JsonStringEnumMemberName(nameof(OtherVideo))]
    OtherVideo = 17,

    [JsonStringEnumMemberName(nameof(OtherVideoData))]
    OtherVideoData = 18,

    [JsonStringEnumMemberName(nameof(OtherVideoPart))]
    OtherVideoPart = 19,
}
