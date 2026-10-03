using System.Diagnostics.CodeAnalysis;

namespace Reaparr.PlexApi.Contracts;

public record LibraryMetadata
{
    [SetsRequiredMembers]
    public LibraryMetadata(PlexLibrary plexLibrary)
    {
        Library = plexLibrary;
    }

    public PlexLibrary Library { get; private set; }

    public required IReadOnlyCollection<PlexCountry> Countries { get; init; } = [];

    public required IReadOnlyCollection<PlexGenre> Genres { get; init; } = [];

    public required IReadOnlyCollection<PlexActor> Actors { get; init; } = [];

    public int PhotoClipCount { get; init; }
}
