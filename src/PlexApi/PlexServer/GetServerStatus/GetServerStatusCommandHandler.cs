namespace Reaparr.PlexApi;

public class GetServerStatusCommandHandler : ICommandHandler<GetServerStatusCommand, Result<PlexServerStatus>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetServerStatusCommandHandler(IReaparrDbContext dbContext, IPlexApiClientFactory plexApiClientFactory)
    {
        _dbContext = dbContext;
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<PlexServerStatus>> ExecuteAsync(GetServerStatusCommand command, CancellationToken ct)
    {
        var connection = await _dbContext.PlexServerConnections.GetAsync(command.PlexServerConnectionId, ct);

        if (connection is null)
            return ResultExtensions.EntityNotFound(nameof(PlexServerConnection), command.PlexServerConnectionId);

        var client = _plexApiClientFactory.CreateClient(
            string.Empty, // No need for auth token here
            new PlexApiClientOptions
            {
                ConnectionUrl = connection.Url,
                RetryProgressAction = command.ProgressAction,
                Timeout = command.Timeout,
                RetryCount = 0,
            },
            cancellationToken: ct
        );
        var responseResult = await client.General.GetIdentityAsync().ToResponse(ct);

        if (responseResult.IsCancelled)
            return responseResult.ToResult();

        var statusCode = responseResult.IsSuccess
            ? responseResult.Value.StatusCode
            : responseResult.GetStatusCodeReason()?.GetStatusCode();
        var statusMessage = statusCode switch
        {
            200 => "The Plex server is online!",
            401 => "The Plex token has expired and needs to be refreshed.",
            _ => "The Plex server could not be reached, most likely it's offline.",
        };

        return Result.Ok(
            new PlexServerStatus
            {
                StatusCode = statusCode ?? -1,
                StatusMessage = statusMessage,
                LastChecked = DateTime.UtcNow,
                IsSuccessful = responseResult.IsSuccess,
                PlexServerId = connection.PlexServerId,
                PlexServerConnectionId = connection.Id,
            }
        );
    }
}
