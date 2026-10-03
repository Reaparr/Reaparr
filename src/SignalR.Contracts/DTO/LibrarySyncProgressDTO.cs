namespace Reaparr.SignalR.Contracts;

public record LibrarySyncProgressDTO
{
    public required int PlexLibraryId { get; init; }

    public required TimeSpan TimeRemaining { get; init; }

    public required IReadOnlyList<LibrarySyncProgressItemDTO> Items { get; init; }

    public int Received => Items.Sum(i => i.Received);

    public int Total => Items.Sum(i => Math.Max(i.Total, 0));

    public decimal Percentage => IsComplete ? 100 : Total == 0 ? 0 : DataFormat.GetPercentage(Received, Total);

    public DateTime TimeStamp { get; } = DateTime.UtcNow;

    public required IReadOnlyList<IError> Errors { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <see cref="LibrarySyncProgressDTO"/> has finished refreshing.
    /// All items must be complete for the overall progress to be considered complete.
    /// </summary>
    public bool IsComplete => Items.Any() && Items.All(i => i.IsComplete);
}

public record LibrarySyncProgressItemDTO
{
    public required PlexMediaType MediaType { get; init; }

    public required int Received { get; init; }

    public required int Total { get; init; }

    public required TimeSpan TimeRemaining { get; init; }

    public decimal Percentage =>
        Total < 0 ? 0 : Total == 0 ? 100 : Math.Min(Math.Max(DataFormat.GetPercentage(Received, Total), 0), 100);

    public bool IsComplete => Total >= 0 && Received >= Total;
}
