namespace Reaparr.Application;

public class GetAllPlexServerConnectionsEndpoint : EndpointWithoutRequest<List<PlexServerConnectionDTO>>
{
    private readonly IReaparrDbContext _dbContext;

    public GetAllPlexServerConnectionsEndpoint(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexServerConnectionController + "/");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<List<PlexServerConnectionDTO>>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var plexServers = await _dbContext
            .PlexServers.Include(x => x.PlexServerConnections)
                .ThenInclude(x => x.LatestConnectionStatus)
            .ToListAsync(ct);

        // Decide for the frontend which connection to use
        var chosenHash = new HashSet<int>();

        foreach (var plexServer in plexServers)
        {
            var chosenConnectionResult = plexServer.ChoosePlexServerConnection();

            if (chosenConnectionResult.IsFailed)
                continue;

            chosenHash.Add(chosenConnectionResult.Value.Id);
        }

        var plexServerConnections = plexServers.SelectMany(x => x.PlexServerConnections).ToList().ToDTO();
        foreach (var connection in plexServerConnections)
            connection.ChosenConnection = chosenHash.Contains(connection.Id);

        await Send.FluentResult(Result.Ok(plexServerConnections), x => x, ct);
    }
}
