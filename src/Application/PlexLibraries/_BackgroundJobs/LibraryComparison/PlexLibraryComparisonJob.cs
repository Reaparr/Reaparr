namespace Reaparr.Application;

public sealed record PlexLibraryComparisonJobPayload(int OwnedPlexLibraryId, int RemotePlexLibraryId);

/// <summary>
/// Compares one remote Plex library with one owned Plex library.
/// </summary>
public class PlexLibraryComparisonJob : IJob
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public PlexLibraryComparisonJob(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<PlexLibraryComparisonJob>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public static JobKey GetJobKey(int ownedPlexLibraryId, int remotePlexLibraryId) =>
        new(
            $"{nameof(JobTypes.LibraryComparisonJob)}_{ownedPlexLibraryId}_{remotePlexLibraryId}",
            nameof(JobTypes.LibraryComparisonJob)
        );

    public async Task Execute(IJobExecutionContext context)
    {
        using var telemetryScope = _telemetry.BeginJob(
            TelemetryActivity.LibraryComparison,
            context.GetTelemetryContext()
        );

        var payloadResult = context.GetRequiredPayload<PlexLibraryComparisonJobPayload>();
        if (payloadResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, payloadResult);
            payloadResult.LogError();
            return;
        }

        var cancellationToken = context.CancellationToken;
        var ownedPlexLibraryId = payloadResult.Value.OwnedPlexLibraryId;
        var remotePlexLibraryId = payloadResult.Value.RemotePlexLibraryId;

        var libraries = await _dbContext
            .PlexLibraries.Where(x => x.Id == remotePlexLibraryId || x.Id == ownedPlexLibraryId)
            .SelectOwnership()
            .ToListAsync(cancellationToken);

        var ownedLibrary = libraries.SingleOrDefault(x => x.Id == ownedPlexLibraryId);
        if (ownedLibrary is null)
        {
            _log.Here().Warning("Owned Plex library {PlexLibraryId} was not found", ownedPlexLibraryId);
            context.SetResult(JobStatus.Failed, $"Owned Plex library {ownedPlexLibraryId} was not found");
            return;
        }

        var remoteLibrary = libraries.SingleOrDefault(x => x.Id == remotePlexLibraryId);
        if (remoteLibrary is null)
        {
            _log.Here().Warning("Remote Plex library {PlexLibraryId} was not found", remotePlexLibraryId);
            context.SetResult(JobStatus.Failed, $"Remote Plex library {remotePlexLibraryId} was not found");
            return;
        }

        if (remoteLibrary.Id == ownedLibrary.Id)
        {
            _log.Here().Warning("A Plex library cannot be compared with itself: {PlexLibraryId}", remoteLibrary.Id);
            context.SetResult(JobStatus.Failed, "A Plex library cannot be compared with itself");
            return;
        }

        if (remoteLibrary.IsOwned)
        {
            _log.Here().Warning("Remote Plex library {PlexLibraryId} is currently marked as owned", remoteLibrary.Id);
            context.SetResult(JobStatus.Failed, $"Remote Plex library {remoteLibrary.Id} is currently marked as owned");
            return;
        }

        if (!ownedLibrary.IsOwned)
        {
            _log.Here().Warning("Owned Plex library {PlexLibraryId} is currently marked as remote", ownedLibrary.Id);
            context.SetResult(JobStatus.Failed, $"Owned Plex library {ownedLibrary.Id} is currently marked as remote");
            return;
        }

        if (
            remoteLibrary.Type != ownedLibrary.Type
            || remoteLibrary.Type is not (PlexMediaType.Movie or PlexMediaType.TvShow or PlexMediaType.MusicArtist)
        )
        {
            _log.Here()
                .Warning(
                    "Cannot compare {RemoteMediaType} library {RemoteLibraryId} with {OwnedMediaType} library {OwnedLibraryId}",
                    remoteLibrary.Type,
                    remoteLibrary.Id,
                    ownedLibrary.Type,
                    ownedLibrary.Id
                );
            context.SetResult(JobStatus.Failed, "Library media types are not supported or do not match");
            return;
        }

        _log.Here()
            .Debug(
                "Comparing remote library {RemoteLibraryId} with owned library {OwnedLibraryId} for {MediaType}",
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteLibrary.Type
            );

        Result result;
        switch (remoteLibrary.Type)
        {
            case PlexMediaType.Movie:
                result = await _commandExecutor.Send(
                    new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id),
                    cancellationToken
                );
                break;
            case PlexMediaType.TvShow:
                result = await _commandExecutor.Send(
                    new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id),
                    cancellationToken
                );
                break;
            case PlexMediaType.MusicArtist:
                result = await _commandExecutor.Send(
                    new CompareMusicPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id),
                    cancellationToken
                );
                break;
            default:
                result = Result.Fail(
                    "Library comparisons are not supported for media type {RemoteLibraryType}",
                    remoteLibrary.Type
                );
                break;
        }

        if (result.IsCancelled)
        {
            context.SetResult(JobStatus.Cancelled, result);
            result.LogWarning();
            _telemetry.LibraryComparisonFinished(ownedLibrary.Id, remoteLibrary.Id, remoteLibrary.Type, result);
            return;
        }

        if (result.IsFailed)
        {
            context.SetResult(JobStatus.Failed, result);
            result.LogError();
            _log.Here()
                .Error(
                    "Comparison of remote library {RemoteLibraryId} with owned library {OwnedLibraryId} failed: {Errors}",
                    remoteLibrary.Id,
                    ownedLibrary.Id,
                    string.Join("; ", result.Errors.Select(x => x.Message))
                );
            _telemetry.LibraryComparisonFinished(ownedLibrary.Id, remoteLibrary.Id, remoteLibrary.Type, result);
            _telemetry.CaptureErrors(result, TelemetryActivity.LibraryComparison);
            return;
        }

        context.Put(
            nameof(LibraryComparisonCompletedDTO),
            new LibraryComparisonCompletedDTO
            {
                AffectedLibraryIds = [ownedLibrary.Id, remoteLibrary.Id],
                MediaType = remoteLibrary.Type,
                CompletedAt = DateTime.UtcNow,
            }
        );
        context.SetResult(JobStatus.Completed);
        _telemetry.LibraryComparisonFinished(ownedLibrary.Id, remoteLibrary.Id, remoteLibrary.Type, result);
    }
}
