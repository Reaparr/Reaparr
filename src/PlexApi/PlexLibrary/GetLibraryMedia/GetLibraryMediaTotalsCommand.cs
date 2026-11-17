namespace Reaparr.PlexApi;

public record GetLibraryMediaTotalsCommand(PlexLibrary PlexLibrary)
    : ICommand<Result<IReadOnlyDictionary<PlexMediaType, int>>>;

public class GetLibraryMediaTotalsCommandHandler
    : ICommandHandler<GetLibraryMediaTotalsCommand, Result<IReadOnlyDictionary<PlexMediaType, int>>>
{
    private readonly ILogger _log;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetLibraryMediaTotalsCommandHandler(ILogger log, IPlexApiClientFactory plexApiClientFactory)
    {
        _log = log.ForContext<GetLibraryMediaTotalsCommandHandler>();
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<IReadOnlyDictionary<PlexMediaType, int>>> ExecuteAsync(
        GetLibraryMediaTotalsCommand command,
        CancellationToken ct
    )
    {
        if (!long.TryParse(command.PlexLibrary.Key, out var libraryKey))
        {
            _log.Here()
                .Warning(
                    "Unable to preflight progress totals for library {PlexLibraryId}; Plex key {PlexLibraryKey} is invalid",
                    command.PlexLibrary.Id,
                    command.PlexLibrary.Key
                );
            return Result.Ok<IReadOnlyDictionary<PlexMediaType, int>>(new Dictionary<PlexMediaType, int>());
        }

        var clientResult = await _plexApiClientFactory.CreateClientAsync(
            command.PlexLibrary.PlexServerId,
            cancellationToken: ct
        );
        if (clientResult.IsFailed)
            return clientResult.ToResult().LogIfFailed();

        var client = clientResult.Value;

        var totals = new Dictionary<PlexMediaType, int>();
        foreach (var mediaType in command.PlexLibrary.Type.GetFamilyTypes())
        {
            var totalResult = await client.GetLibraryMediaTotalCountAsync(libraryKey, mediaType, ct);
            if (totalResult.IsCancelled)
                return totalResult.ToResult().LogIfFailed();

            if (totalResult.IsFailed)
            {
                _log.Here()
                    .Warning(
                        "Unable to preflight {PlexMediaType} progress total for library {PlexLibraryId}; retrieval will resolve it when the layer starts",
                        mediaType,
                        command.PlexLibrary.Id
                    );
                continue;
            }

            totals[mediaType] = totalResult.Value;
        }

        return Result.Ok<IReadOnlyDictionary<PlexMediaType, int>>(totals);
    }
}
