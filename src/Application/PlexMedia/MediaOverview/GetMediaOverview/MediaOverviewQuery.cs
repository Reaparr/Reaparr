namespace Reaparr.Application;

public sealed record MediaOverviewQuery
{
    public required PlexMediaType MediaType { get; init; }

    public int PlexLibraryId { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; }

    public string? Search { get; init; }

    public int? CountryId { get; init; }

    public int? GenreId { get; init; }

    public int? RoleId { get; init; }

    public int? QualityId { get; init; }

    public PlexMediaComparisonState? ComparisonState { get; init; }

    public string? Sort { get; init; }

    public bool FilterOfflineMedia { get; init; }

    public bool FilterOwnedMedia { get; init; }
}
