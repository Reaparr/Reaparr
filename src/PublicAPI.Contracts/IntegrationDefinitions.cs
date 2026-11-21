namespace Reaparr.PublicAPI.Contracts;

public static class IntegrationDefinitions
{
    // ReSharper disable once InconsistentNaming
    public const string RADARR_DEFAULT_CATEGORY = "reaparr-radarr";

    // ReSharper disable once InconsistentNaming
    public const string SONARR_DEFAULT_CATEGORY = "reaparr-sonarr";

    public const string IntegrationIdentityItemKey = "IntegrationIdentity";

    // ReSharper disable once InconsistentNaming
    public const string INDEXER_API_KEY = "apikey";

    public static IReadOnlyList<TorznabCategoryDefinition> SupportedTorznabCategories { get; } =
    [
        // Movies
        new(TorznabCategoryId.Movies, "Movies"),
        new(TorznabCategoryId.Movies_Foreign, "Movies/Foreign"),
        new(TorznabCategoryId.Movies_SD, "Movies/SD"),
        new(TorznabCategoryId.Movies_HD, "Movies/HD"),
        new(TorznabCategoryId.Movies_UHD, "Movies/UHD"),
        new(TorznabCategoryId.Movies_BluRay, "Movies/BluRay"),
        new(TorznabCategoryId.Movies_WEBDL, "Movies/WEBDL"),
        // TV
        new(TorznabCategoryId.TV, "TV"),
        new(TorznabCategoryId.TV_Foreign, "TV/Foreign"),
        new(TorznabCategoryId.TV_SD, "TV/SD"),
        new(TorznabCategoryId.TV_HD, "TV/HD"),
        new(TorznabCategoryId.TV_UHD, "TV/UHD"),
        new(TorznabCategoryId.TV_Sport, "TV/Sport"),
        new(TorznabCategoryId.TV_Anime, "TV/Anime"),
        new(TorznabCategoryId.TV_Documentary, "TV/Documentary"),
    ];

    public static IReadOnlyList<TorznabCategoryDefinition> MovieCategories { get; } =
    [.. SupportedTorznabCategories.Where(x => (int)x.Id is >= 2000 and < 3000)];

    public static IReadOnlyList<TorznabCategoryDefinition> TvCategories { get; } =
    [.. SupportedTorznabCategories.Where(x => (int)x.Id is >= 5000 and < 6000)];
}

public sealed record TorznabCategoryDefinition(TorznabCategoryId Id, string Name);
