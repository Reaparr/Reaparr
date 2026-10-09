namespace Reaparr.Application.Contracts;

public record CompareMusicPlexLibraryCommand(int OwnedPlexLibraryId, int RemotePlexLibraryId) : ICommand<Result>;
