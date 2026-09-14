namespace Reaparr.Data.Contracts;

public sealed record MediaNavigationIndexRow(
    string? SearchTitle,
    int Year,
    int? QualityValue,
    int Duration,
    DateTime AddedAt,
    DateTime? UpdatedAt,
    long MediaSize
);
