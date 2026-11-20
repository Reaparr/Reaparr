namespace Reaparr.Application;

public class GetPlexLibraryAccessTimelineEndpoint : EndpointWithoutRequest<PlexLibraryAccessTimelineDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GetPlexLibraryAccessTimelineEndpoint(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GetPlexLibraryAccessTimelineEndpoint>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexLibraryController + "/access-timeline");

        Description(x =>
            x.Produces<ResultDTO<PlexLibraryAccessTimelineDTO>>()
                .Produces<BaseResultDTO>(StatusCodes.Status400BadRequest)
                .Produces<BaseResultDTO>(StatusCodes.Status500InternalServerError)
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);

        var events = (
            await _dbContext
                .PlexLibraryAccessHistoryEvents.Select(x => new PlexLibraryAccessTimelineEventDTO
                {
                    Id = x.Id,
                    RefreshRunId = x.RefreshRunId,
                    PlexAccountId = x.PlexAccountId,
                    PlexAccountName = x.PlexAccountNameSnapshot ?? string.Empty,
                    PlexServerId = x.PlexServerId,
                    PlexServerName = x.PlexServerNameSnapshot,
                    PlexLibraryId = x.PlexLibraryId,
                    PlexLibraryName = x.PlexLibraryNameSnapshot,
                    State = x.State,
                    CreatedAt = x.CreatedAt,
                })
                .OrderBy(x => x.PlexServerName)
                .ThenBy(x => x.PlexLibraryName)
                .ToListAsync(ct)
        );

        var response = new PlexLibraryAccessTimelineDTO { Events = events, CurrentState = ToCurrentState(events) };

        await Send.FluentResult(Result.Ok(response), x => x, ct);
    }

    private static List<PlexLibraryAccessCurrentStateDTO> ToCurrentState(List<PlexLibraryAccessTimelineEventDTO> events)
    {
        List<PlexLibraryAccessCurrentStateLibraryDTO> accessibleLibraries = [];
        foreach (
            var eventGroup in events.GroupBy(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.PlexLibraryId,
            })
        )
        {
            var accessibleLibrary = ToAccessibleLibraryOrDefault(
                eventGroup.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToList()
            );
            if (accessibleLibrary is not null)
                accessibleLibraries.Add(accessibleLibrary);
        }

        return accessibleLibraries
            .GroupBy(x => new { x.PlexServerId, x.PlexServerName })
            .Select(x => new PlexLibraryAccessCurrentStateDTO
            {
                PlexServerId = x.Key.PlexServerId,
                PlexServerName = x.Key.PlexServerName,
                Libraries = x.OrderBy(y => y.PlexLibraryName, StringComparer.Ordinal).ThenBy(y => y.PlexAccountName, StringComparer.Ordinal).ToList(),
            })
            .OrderBy(x => x.PlexServerName, StringComparer.Ordinal)
            .ToList();
    }

    private static PlexLibraryAccessCurrentStateLibraryDTO? ToAccessibleLibraryOrDefault(
        List<PlexLibraryAccessTimelineEventDTO> events
    )
    {
        var latestEvent = events.Last();
        if (latestEvent.State != PlexAccessState.Granted)
            return null;

        return new PlexLibraryAccessCurrentStateLibraryDTO
        {
            PlexServerId = latestEvent.PlexServerId,
            PlexServerName = latestEvent.PlexServerName,
            PlexLibraryId = latestEvent.PlexLibraryId,
            PlexLibraryName = latestEvent.PlexLibraryName,
            PlexAccountId = latestEvent.PlexAccountId,
            PlexAccountName = latestEvent.PlexAccountName,
            GrantedAt = latestEvent.CreatedAt,
            LastChangedAt = latestEvent.CreatedAt,
        };
    }
}
