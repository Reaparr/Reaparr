namespace Reaparr.Application.Contracts;

public record LibraryProgress
{
    public required int PlexLibraryId { get; init; }

    public required PlexMediaType PlexLibraryType { get; init; }

    public required IReadOnlyList<LibraryProgressItem> Items { get; init; }

    public IReadOnlyList<IError> Errors { get; init; } = [];

    public TimeSpan TimeRemaining => Items.Aggregate(TimeSpan.Zero, (acc, i) => acc + i.TimeRemaining);

    public int Received => Items.Sum(i => i.Received);

    public int Total => Items.Sum(i => Math.Max(i.Total, 0));

    public decimal Percentage => IsComplete ? 100 : Total == 0 ? 0 : DataFormat.GetPercentage(Received, Total);

    public DateTime TimeStamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets a value indicating whether the <see cref="LibraryProgress"/> has finished refreshing.
    /// All items must be complete for the overall progress to be considered complete.
    /// </summary>
    public bool IsComplete => Items.Any() && Items.All(i => i.IsComplete);
}
