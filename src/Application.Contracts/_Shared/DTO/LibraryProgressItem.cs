namespace Reaparr.Application.Contracts;

public record LibraryProgressItem
{
    public required PlexMediaType MediaType { get; init; }

    public required int Received { get; init; }

    public required int Total { get; init; }

    public required TimeSpan TimeRemaining { get; init; }

    public decimal Percentage =>
        Total < 0 ? 0 : Total == 0 ? 100 : Math.Min(Math.Max(DataFormat.GetPercentage(Received, Total), 0), 100);

    public bool IsComplete => Total >= 0 && Received >= Total;
}
