using System.Globalization;

namespace Reaparr.PlexApi;

public class GetLibrarySectionsCommandHandler : ICommandHandler<GetLibrarySectionsCommand, Result<List<PlexLibrary>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetLibrarySectionsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IPlexApiClientFactory plexApiClientFactory
    )
    {
        _log = log.ForContext<GetLibrarySectionsCommandHandler>();
        _dbContext = dbContext;
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<List<PlexLibrary>>> ExecuteAsync(GetLibrarySectionsCommand command, CancellationToken ct)
    {
        var plexServerId = command.PlexServerId;
        var plexAccountId = command.PlexAccountId;

        var tokenResult = await _dbContext.GetPlexServerTokenAsync(plexServerId, plexAccountId, ct);
        if (tokenResult.IsFailed)
            return tokenResult.ToResult();

        var plexServerConnectionResult = await _dbContext.ChoosePlexServerConnection(plexServerId, ct);
        if (plexServerConnectionResult.IsFailed)
            return plexServerConnectionResult.ToResult();

        var connection = plexServerConnectionResult.Value;
        var plexServer = connection.PlexServer;
        if (plexServer is null)
            return ResultExtensions.EntityNotFound(nameof(PlexServer), plexServerId);

        var client = _plexApiClientFactory.CreateClient(
            tokenResult.Value,
            new PlexApiClientOptions { ConnectionUrl = connection.Url }
        );

        var response = await client.Library.GetSectionsAsync().ToResponse(ct);
        if (response.IsFailed)
            return response.ToResult();

        if (response.Value.Object?.MediaContainer?.Directory is null)
        {
            _log.Here()
                .Error(
                    "Plex server: {PlexServerName} returned an empty response when libraries were requested",
                    connection.PlexServer?.Name
                );
            return Result.Fail("Plex returned no library section collection").LogError();
        }

        var directories = response.Value.Object.MediaContainer.Directory;
        var mappedLibraries = directories
            .Select(x => new PlexLibrary
            {
                Id = 0,
                Type = x.Type.ToPlexMediaType(x.Agent, x.Scanner),
                Title = x.Title!,
                Key = x.Key!,
                CreatedAt = DateTimeExtensions.FromUnixTime(x.CreatedAt),
                UpdatedAt = DateTimeExtensions.FromUnixTime(x.UpdatedAt),
                ScannedAt = DateTimeExtensions.FromUnixTime(x.ScannedAt),
                ContentChangedAt = x.ContentChangedAt ?? 0,
                SyncedAt = null,
                Outdated = false,
                Uuid = x.Uuid,
                PlexServer = null,
                PlexServerId = plexServerId,
                DefaultDestination = null,
                DefaultDestinationId = null,
                Language = x.Language,
            })
            .ToList();

        foreach (var library in mappedLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum))
        {
            var directory = directories.Single(x => x.Key == library.Key);
            var rootsByIdentity = new Dictionary<string, PlexPhotoFolder>(StringComparer.Ordinal);
            foreach (var location in directory.Location ?? [])
            {
                if (
                    !location.Path.TryCreatePhotoRootIdentity(
                        out var pathIdentity,
                        out var canonicalPath,
                        out var pathError
                    )
                )
                    return Result.Fail<List<PlexLibrary>>(
                        $"Plex photo library '{library.Key}' returned an invalid source root: {pathError}"
                    );

                var identity = location.Id is > 0
                    ? $"id:{location.Id.Value.ToString(CultureInfo.InvariantCulture)}"
                    : pathIdentity;

                if (rootsByIdentity.TryGetValue(identity, out var existing))
                {
                    if (!string.Equals(existing.SourceRootPath, canonicalPath, StringComparison.Ordinal))
                        return Result.Fail<List<PlexLibrary>>(
                            $"Plex photo library '{library.Key}' returned conflicting source roots for '{identity}'"
                        );
                    continue;
                }

                rootsByIdentity.Add(
                    identity,
                    new PlexPhotoFolder
                    {
                        Id = 0,
                        PlexServerId = plexServerId,
                        PlexLibraryId = 0,
                        RelativePath = string.Empty,
                        Title = GetRootName(location.Path),
                        RootIdentityKey = identity,
                        SourceRootPath = canonicalPath,
                        PlexApiLocationId = location.Id is > 0 ? location.Id : null,
                        IsActive = true,
                    }
                );
            }

            foreach (var root in rootsByIdentity.Values)
                library.PhotoFolders.Add(root);
        }

        return Result.Ok(mappedLibraries);
    }

    private static string GetRootName(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        return trimmed.Length == 0 ? path : trimmed.GetSourcePathLeafName();
    }
}
