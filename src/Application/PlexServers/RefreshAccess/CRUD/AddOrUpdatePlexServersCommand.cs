namespace Reaparr.Application;

public record AddOrUpdatePlexServersCommand(List<PlexServer> PlexServers) : ICommand<Result<PlexServerRapport>>;

public class AddOrUpdatePlexServersCommandValidator : AbstractValidator<AddOrUpdatePlexServersCommand>
{
    public AddOrUpdatePlexServersCommandValidator()
    {
        RuleFor(x => x.PlexServers).NotEmpty().WithMessage("PlexServers list cannot be empty.");

        RuleForEach(x => x.PlexServers)
            .ChildRules(server =>
            {
                server
                    .RuleForEach(s => s.PlexServerConnections)
                    .ChildRules(connection =>
                    {
                        connection.RuleFor(c => c.Protocol).NotEmpty().WithMessage("Protocol is required.");

                        connection.RuleFor(c => c.Address).NotEmpty().WithMessage("Address is required.");

                        connection.RuleFor(c => c.Port).NotEmpty().WithMessage("Port is required.");
                        connection
                            .RuleFor(c => c.LatestConnectionStatus)
                            .Null()
                            .WithMessage("PlexServerStatus should be null.");
                    });
            });
    }
}

public class AddOrUpdatePlexServersCommandHandler
    : ICommandHandler<AddOrUpdatePlexServersCommand, Result<PlexServerRapport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public AddOrUpdatePlexServersCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<AddOrUpdatePlexServersCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<PlexServerRapport>> ExecuteAsync(
        AddOrUpdatePlexServersCommand command,
        CancellationToken cancellationToken
    )
    {
        var incomingPlexServers = command.PlexServers;
        var rapport = new PlexServerRapport();

        // Add or update the PlexServers in the database
        _log.Here().Information("Adding or updating {PlexServersCount} PlexServers now", incomingPlexServers.Count);

        var machineIds = incomingPlexServers.Select(x => x.MachineIdentifier).ToList();
        var plexServerDbList = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Include(x => x.PlexServerConnections)
            .Include(x => x.PlexAccountServers)
            .Where(x => machineIds.Contains(x.MachineIdentifier))
            .AsTracking()
            .ToListAsync(cancellationToken);

        foreach (var incomingPlexServer in incomingPlexServers)
        {
            var existingServer = plexServerDbList.FirstOrDefault(x =>
                x.MachineIdentifier == incomingPlexServer.MachineIdentifier
            );
            if (existingServer != null)
            {
                // PlexServer already exists
                _log.Here().Debug("Updating PlexServer with id: {PlexServerDbId} in the database", existingServer.Id);
                incomingPlexServer.Id = existingServer.Id;

                _dbContext
                    .Entry(existingServer)
                    .CurrentValues.SetValues(
                        new PlexServer
                        {
                            Id = incomingPlexServer.Id,
                            Name = incomingPlexServer.Name,
                            OwnerId = incomingPlexServer.OwnerId,
                            PlexServerOwnerUsername = incomingPlexServer.PlexServerOwnerUsername,
                            Device = incomingPlexServer.Device,
                            Platform = incomingPlexServer.Platform,
                            PlatformVersion = incomingPlexServer.PlatformVersion,
                            Product = incomingPlexServer.Product,
                            ProductVersion = incomingPlexServer.ProductVersion,
                            Provides = incomingPlexServer.Provides,
                            CreatedAt = incomingPlexServer.CreatedAt,
                            LastSeenAt = incomingPlexServer.LastSeenAt,
                            MachineIdentifier = incomingPlexServer.MachineIdentifier,
                            PublicAddress = incomingPlexServer.PublicAddress,
                            PreferredConnectionId = existingServer.PreferredConnectionId,
                            IsEnabled = existingServer.IsEnabled,
                            IsDownloadsPausedByUser = existingServer.IsDownloadsPausedByUser,
                            OwnedOverride = existingServer.OwnedOverride,
                            Synced = incomingPlexServer.Synced,
                            Relay = incomingPlexServer.Relay,
                            Presence = incomingPlexServer.Presence,
                            HttpsRequired = incomingPlexServer.HttpsRequired,
                            PublicAddressMatches = incomingPlexServer.PublicAddressMatches,
                            DnsRebindingProtection = incomingPlexServer.DnsRebindingProtection,
                            NatLoopbackSupported = incomingPlexServer.NatLoopbackSupported,
                            PlexAccountServers = [],
                            PlexLibraries = [],
                            ServerStatus = [],
                            PlexServerConnections = [],
                        }
                    );

                SyncPlexServerConnections(incomingPlexServer, existingServer);
                rapport.Updated.Add(incomingPlexServer.Id);
            }
            else
            {
                // Create plexServer
                _log.Here()
                    .Debug("Adding PlexServer with name: {PlexServerName} to the database", incomingPlexServer.Name);
                foreach (var plexServerConnection in incomingPlexServer.PlexServerConnections)
                    plexServerConnection.PlexServerId = incomingPlexServer.Id;

                _dbContext.PlexServers.Add(incomingPlexServer);
                _dbContext.PlexServerConnections.AddRange(incomingPlexServer.PlexServerConnections);
                rapport.Created.Add(incomingPlexServer.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _log.Here().Information(rapport.ToString());

        return Result.Ok(rapport);
    }

    private void SyncPlexServerConnections(PlexServer plexServer, PlexServer existingServer)
    {
        // Create or Update PlexServerConnections
        foreach (var newConnection in plexServer.PlexServerConnections)
        {
            newConnection.PlexServerId = plexServer.Id;

            var existingConnection = existingServer.PlexServerConnections.FirstOrDefault(x => x.Equals(newConnection));

            if (existingConnection is null)
            {
                _log.Here()
                    .Debug(
                        "Creating connection {PlexServerConnection} from {PlexServerName} in the database",
                        newConnection.ToString(),
                        existingServer.Name
                    );
                _dbContext.PlexServerConnections.Add(newConnection);
            }
            else
            {
                _log.Here()
                    .Debug(
                        "Updating connection {PlexServerConnection} from {PlexServerName} in the database",
                        newConnection.ToString(),
                        existingServer.Name
                    );
                newConnection.Id = existingConnection.Id;
                // Plex may advertise a user-created address, but must not take ownership of it.
                var isCustom = existingConnection.IsCustom;
                _dbContext.Entry(existingConnection).CurrentValues.SetValues(newConnection);
                _dbContext.Entry(existingConnection).Property(nameof(PlexServerConnection.IsCustom)).CurrentValue =
                    isCustom;
            }
        }

        foreach (var existingConnection in existingServer.PlexServerConnections.ToList())
        {
            if (
                !existingConnection.IsCustom && !plexServer.PlexServerConnections.Any(x => x.Equals(existingConnection))
            )
            {
                _log.Here()
                    .Debug(
                        "Removing connection {PlexServerConnection} from {PlexServerName} in the database",
                        existingConnection.ToString(),
                        existingServer.Name
                    );
                _dbContext.Entry(existingConnection).State = EntityState.Deleted;
            }
        }
    }
}

public record PlexServerRapport
{
    public List<int> Created { get; } = [];

    public List<int> Updated { get; } = [];

    public override string ToString() =>
        $@"
        Created {nameof(PlexServer)} Access: {Created}
        Updated {nameof(PlexServer)} Access: {Updated}";
}
