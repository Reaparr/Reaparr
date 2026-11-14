namespace Reaparr.Application;

public record TestConnectionResult
{
    public required TestConnectionStatus Status { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime TestedAt { get; init; }
}
