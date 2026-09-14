namespace Reaparr.Domain;

public enum PlexGenreType
{
    /// <summary>
    /// Represents an unknown or unspecified genre type.
    /// </summary>
    [JsonStringEnumMemberName(nameof(Unknown))]
    Unknown = 0,

    /// <summary>
    /// Represents a group of genres or a general category that encompasses multiple specific genres.
    /// </summary>
    [JsonStringEnumMemberName(nameof(Group))]
    Group = 1,

    [JsonStringEnumMemberName(nameof(Action))]
    Action = 2,

    [JsonStringEnumMemberName(nameof(Adventure))]
    Adventure = 3,

    [JsonStringEnumMemberName(nameof(Anime))]
    Anime = 4,

    [JsonStringEnumMemberName(nameof(Animation))]
    Animation = 5,

    [JsonStringEnumMemberName(nameof(Manga))]
    Manga = 6,

    [JsonStringEnumMemberName(nameof(Comedy))]
    Comedy = 7,

    [JsonStringEnumMemberName(nameof(Crime))]
    Crime = 8,

    [JsonStringEnumMemberName(nameof(Documentary))]
    Documentary = 9,

    [JsonStringEnumMemberName(nameof(Biography))]
    Biography = 10,

    [JsonStringEnumMemberName(nameof(Drama))]
    Drama = 11,

    [JsonStringEnumMemberName(nameof(Soap))]
    Soap = 12,

    [JsonStringEnumMemberName(nameof(Family))]
    Family = 13,

    [JsonStringEnumMemberName(nameof(Fantasy))]
    Fantasy = 14,

    [JsonStringEnumMemberName(nameof(History))]
    History = 15,

    [JsonStringEnumMemberName(nameof(Horror))]
    Horror = 16,

    [JsonStringEnumMemberName(nameof(Suspense))]
    Suspense = 17,

    [JsonStringEnumMemberName(nameof(Music))]
    Music = 18,

    [JsonStringEnumMemberName(nameof(Musical))]
    Musical = 19,

    [JsonStringEnumMemberName(nameof(Mystery))]
    Mystery = 20,

    [JsonStringEnumMemberName(nameof(News))]
    News = 21,

    [JsonStringEnumMemberName(nameof(Reality))]
    Reality = 22,

    [JsonStringEnumMemberName(nameof(Romance))]
    Romance = 23,

    [JsonStringEnumMemberName(nameof(ScienceFiction))]
    ScienceFiction = 24,

    [JsonStringEnumMemberName(nameof(Sport))]
    Sport = 25,

    [JsonStringEnumMemberName(nameof(Thriller))]
    Thriller = 26,

    [JsonStringEnumMemberName(nameof(War))]
    War = 27,

    [JsonStringEnumMemberName(nameof(Western))]
    Western = 28,

    [JsonStringEnumMemberName(nameof(Adult))]
    Adult = 29,

    [JsonStringEnumMemberName(nameof(Children))]
    Children = 30,

    [JsonStringEnumMemberName(nameof(Educational))]
    Educational = 31,

    [JsonStringEnumMemberName(nameof(Entertainment))]
    Entertainment = 32,

    [JsonStringEnumMemberName(nameof(GameShow))]
    GameShow = 33,

    [JsonStringEnumMemberName(nameof(TalkShow))]
    TalkShow = 34,

    [JsonStringEnumMemberName(nameof(Religion))]
    Religion = 35,

    [JsonStringEnumMemberName(nameof(Other))]
    Other = 36,

    [JsonStringEnumMemberName(nameof(Foreign))]
    Foreign = 37,

    [JsonStringEnumMemberName(nameof(Independent))]
    Independent = 38,

    [JsonStringEnumMemberName(nameof(Podcast))]
    Podcast = 39,
}
