namespace Reaparr.Application;

public record DeletePlexServerConnectionByIdRequest(int PlexServerConnectionId);

public class DeletePlexServerConnectionByIdRequestValidator : Validator<DeletePlexServerConnectionByIdRequest>
{
    public DeletePlexServerConnectionByIdRequestValidator()
    {
        RuleFor(x => x.PlexServerConnectionId).GreaterThan(0);
    }
}

public class DeletePlexServerConnectionById : Endpoint<DeletePlexServerConnectionByIdRequest>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public DeletePlexServerConnectionById(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<DeletePlexServerConnectionById>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.PlexServerConnectionController + "/{PlexServerConnectionId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(DeletePlexServerConnectionByIdRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var connection = await _dbContext.PlexServerConnections.FirstOrDefaultAsync(
            x => x.Id == req.PlexServerConnectionId,
            ct
        );

        if (connection is null)
        {
            await Send.FluentResult(
                ResultExtensions.EntityNotFound(nameof(PlexServerConnection), req.PlexServerConnectionId),
                ct
            );
            return;
        }

        if (!connection.IsCustom)
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult("Only custom connections can be deleted"),
                ct
            );
            return;
        }

        var deleteResult = await _dbContext.ExecuteTransactionAsync(
            async (context, token) =>
            {
                var deleted = await context
                    .PlexServerConnections.Where(x => x.Id == connection.Id && x.IsCustom)
                    .ExecuteDeleteAsync(token);
                if (deleted > 0)
                {
                    await context
                        .PlexServers.IgnoreIsEnabledFilter()
                        .Where(x => x.Id == connection.PlexServerId && x.PreferredConnectionId == connection.Id)
                        .ExecuteUpdateAsync(x => x.SetProperty(y => y.PreferredConnectionId, 0), token);
                }

                return deleted;
            },
            ct
        );
        if (deleteResult.IsCancelledOrFailed)
        {
            await Send.FluentResult(deleteResult.LogIfFailed().ToResult(), ct);
            return;
        }

        if (deleteResult.Value == 0)
        {
            await Send.FluentResult(
                ResultExtensions.EntityNotFound(nameof(PlexServerConnection), req.PlexServerConnectionId),
                ct
            );
            return;
        }

        await Send.FluentResult(Result.Ok(), ct);
    }
}
