namespace Reaparr.Application.Contracts;

public record PlexLibraryAccessTimelineDTO
{
    public required IReadOnlyList<PlexLibraryAccessTimelineEventDTO> Events { get; init; } = [];

    public required IReadOnlyList<PlexLibraryAccessCurrentStateDTO> CurrentState { get; init; } = [];
}

public record PlexLibraryAccessTimelineEventDTO
{
    public required int Id { get; init; }

    public required Guid RefreshRunId { get; init; }

    public required int PlexAccountId { get; init; }

    public required string PlexAccountName { get; init; }

    public int? PlexServerId { get; init; }

    public string? PlexServerName { get; init; }

    public int? PlexLibraryId { get; init; }

    public string? PlexLibraryName { get; init; }

    public required PlexAccessState State { get; init; }

    public required DateTime CreatedAt { get; init; }
}

public record PlexLibraryAccessCurrentStateDTO
{
    public int? PlexServerId { get; init; }

    public string? PlexServerName { get; init; }

    public required IReadOnlyList<PlexLibraryAccessCurrentStateLibraryDTO> Libraries { get; init; } = [];
}

public record PlexLibraryAccessCurrentStateLibraryDTO
{
    public int? PlexServerId { get; init; }

    public string? PlexServerName { get; init; }

    public int? PlexLibraryId { get; init; }

    public string? PlexLibraryName { get; init; }

    public required int PlexAccountId { get; init; }

    public required string PlexAccountName { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }

    public required DateTimeOffset LastChangedAt { get; init; }
}
