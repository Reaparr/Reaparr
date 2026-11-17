namespace Reaparr.PlexApi;

/// <summary>
/// Used to validate the connection URL to the Plex server.
/// </summary>
public class ValidatePlexConnectionUrlHandler
    : ICommandHandler<ValidatePlexConnectionUrlCommand, Result<ServerIdentityDTO>>
{
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public ValidatePlexConnectionUrlHandler(IPlexApiClientFactory plexApiClientFactory)
    {
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<ServerIdentityDTO>> ExecuteAsync(
        ValidatePlexConnectionUrlCommand command,
        CancellationToken ct
    )
    {
        var client = _plexApiClientFactory.CreateClient(
            new PlexApiClientOptions
            {
                ConnectionUrl = command.PlexConnectionUrl,
                Timeout = 5,
                RetryCount = 0,
            },
            cancellationToken: ct
        );
        var response = await client.General.GetIdentityAsync().ToResponse(ct);
        if (response.IsFailed)
            return response.ToResult();

        var mediaContainer = response.Value.Object?.MediaContainer ?? null;
        if (mediaContainer is null)
        {
            return ResultExtensions.IsNull(nameof(mediaContainer));
        }

        return Result.Ok(
            new ServerIdentityDTO
            {
                Claimed = mediaContainer.Claimed ?? false,
                MachineIdentifier = mediaContainer.MachineIdentifier ?? string.Empty,
                Version = mediaContainer.Version ?? string.Empty,
            }
        );
    }
}
