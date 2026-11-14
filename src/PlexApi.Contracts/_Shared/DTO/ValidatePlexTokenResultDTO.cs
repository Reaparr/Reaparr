namespace Reaparr.PlexApi.Contracts;

public record ValidatePlexTokenCommandResult
{
    public required string ClientId { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    public required string Title { get; init; }

    public required long PlexId { get; init; }

    public required string Uuid { get; init; }

    public required string AuthenticationToken { get; init; }

    public required bool IsValidated { get; init; }

    public required DateTime? ValidatedAt { get; init; }

    public required bool Is2Fa { get; init; }
}
