namespace Reaparr.Data.Contracts;

public sealed record MediaNavigationIndexRow
{
    public required string? SearchTitle { get; init; }
    public required int? Year { get; init; }
    public required int? QualityValue { get; init; }
    public required int? Duration { get; init; }
    public required DateTime AddedAt { get; init; }
    public required DateTime? UpdatedAt { get; init; }
    public required long MediaSize { get; init; }
    public int SortRank { get; init; }
}
