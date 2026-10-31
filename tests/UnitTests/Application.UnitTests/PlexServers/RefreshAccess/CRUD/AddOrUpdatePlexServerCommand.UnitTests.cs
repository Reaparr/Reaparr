namespace Reaparr.Application.UnitTests;

public class AddOrUpdatePlexServerCommandUnitTests : BaseUnitTest<AddOrUpdatePlexServersCommandHandler>
{
    [Test]
    public async Task ShouldPreserveCustomOwnership_WhenPlexAdvertisesThenOmitsSameConnection()
    {
        // Arrange
        await SetupDatabase(
            23725,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 2;
            }
        );
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.Include(x => x.PlexServerConnections).SingleAsync(CancellationToken);
        var connections = server.PlexServerConnections.OrderBy(x => x.Id).ToArray();
        var custom = connections[0];
        var managed = connections[1];
        await dbContext
            .PlexServerConnections.Where(x => x.Id == custom.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(c => c.IsCustom, true), CancellationToken);
        var advertisedCustom = new PlexServerConnection
        {
            Protocol = custom.Protocol,
            Address = custom.Address,
            Port = custom.Port,
            Url = custom.Url,
            Local = custom.Local,
            Relay = custom.Relay,
            IPv4 = custom.IPv4,
            IPv6 = custom.IPv6,
            PlexServerId = server.Id,
            IsCustom = false,
            LatestConnectionStatus = null,
        };
        server.PlexServerConnections = [advertisedCustom, managed];
        var before = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.IsCustom })
            .ToListAsync(CancellationToken);
        before.ShouldBe([new { custom.Id, IsCustom = true }, new { managed.Id, IsCustom = false }]);
        var validator = new AddOrUpdatePlexServersCommandValidator();
        var advertisedRequest = new AddOrUpdatePlexServersCommand([server]);
        (await validator.ValidateAsync(advertisedRequest, CancellationToken)).IsValid.ShouldBeTrue();

        // Act
        var advertisedResult = await Sut.ExecuteAsync(advertisedRequest, CancellationToken);
        dbContext.ClearChangeTracker();
        var afterMatch = await dbContext
            .PlexServerConnections.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.IsCustom })
            .ToListAsync(CancellationToken);
        server.PlexServerConnections = [managed];
        var omittedResult = await Sut.ExecuteAsync(new AddOrUpdatePlexServersCommand([server]), CancellationToken);
        dbContext.ClearChangeTracker();

        // Assert
        advertisedResult.IsSuccess.ShouldBeTrue();
        advertisedResult.Errors.Count.ShouldBe(0);
        advertisedResult.Value.Created.ShouldBeEmpty();
        advertisedResult.Value.Updated.ShouldBe([server.Id]);
        afterMatch.ShouldBe(before);
        omittedResult.IsSuccess.ShouldBeTrue();
        omittedResult.Errors.Count.ShouldBe(0);
        omittedResult.Value.Created.ShouldBeEmpty();
        omittedResult.Value.Updated.ShouldBe([server.Id]);
        (
            await dbContext
                .PlexServerConnections.OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.IsCustom })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
        (
            await dbContext
                .PlexServerConnections.Where(x => x.Id == custom.Id)
                .Select(x => x.Url)
                .SingleAsync(CancellationToken)
        ).ShouldBe(custom.Url);
    }

    [Test]
    public async Task ShouldAddAllServers_WhenNoneExistInTheDatabase()
    {
        // Arrange
        var seed = await SetupDatabase(45832543, config => config.PlexServerCount = 0);
        var expectedPlexServers = FakeData.GetPlexServer(seed).Generate(5);

        // Act
        var request = new AddOrUpdatePlexServersCommand(expectedPlexServers);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexServersDbs = IDbContext.PlexServers.Include(x => x.PlexServerConnections).ToList();
        plexServersDbs.Count.ShouldBe(5);

        foreach (var expectedPlexServer in expectedPlexServers)
        {
            var plexServerDb = plexServersDbs.Find(x => x.MachineIdentifier == expectedPlexServer.MachineIdentifier);
            plexServerDb.ShouldNotBeNull();
            ShouldMatchPlexServer(plexServerDb, expectedPlexServer);
            plexServerDb.PlexServerConnections.Count.ShouldBe(expectedPlexServer.PlexServerConnections.Count);
            plexServerDb.PlexServerConnections.ShouldBe(expectedPlexServer.PlexServerConnections, true);
        }
    }

    [Test]
    public async Task ShouldKeepTheSameServerConnectionIds_WhenOnlyTheConnectionPropertiesHaveChanged()
    {
        // Arrange
        var seed = await SetupDatabase(23724, config => config.PlexServerCount = 5);
        var plexServers = IDbContext.PlexServers.Include(x => x.PlexServerConnections).ToList();
        plexServers.Count.ShouldBe(5);

        // Update data setup
        var updatedServers = plexServers.Take(2).ToList();

        // Simulate the server being unchanged, and only the connections having changed except the connection
        // address which it is matched on during updating
        foreach (var updatedServer in updatedServers)
        {
            var connectionCount = updatedServer.PlexServerConnections.Count;
            var updatedConnections = FakeData.GetPlexServerConnections(seed).Generate(connectionCount);
            for (var i = 0; i < connectionCount; i++)
                updatedConnections[i] = new PlexServerConnection
                {
                    Id = updatedConnections[i].Id,
                    Protocol = updatedConnections[i].Protocol,
                    Address = updatedServer.PlexServerConnections.ElementAt(i).Address,
                    Port = updatedConnections[i].Port,
                    Url = updatedConnections[i].Url,
                    Local = updatedConnections[i].Local,
                    Relay = updatedConnections[i].Relay,
                    IPv4 = updatedConnections[i].IPv4,
                    IPv6 = updatedConnections[i].IPv6,
                    PlexServer = updatedConnections[i].PlexServer,
                    PlexServerId = updatedConnections[i].PlexServerId,
                    LatestConnectionStatus = updatedConnections[i].LatestConnectionStatus,
                    IsCustom = updatedConnections[i].IsCustom,
                };

            updatedServer.PlexServerConnections = updatedConnections;
        }

        // Act
        // Now update
        var request = new AddOrUpdatePlexServersCommand(updatedServers);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexServersDbs = IDbContext.PlexServers.Include(x => x.PlexServerConnections).ToList();
        plexServersDbs.Count.ShouldBe(5);

        foreach (var expectedServer in updatedServers)
        {
            var plexServerDb = plexServersDbs.Find(x => x.MachineIdentifier == expectedServer.MachineIdentifier);
            plexServerDb.ShouldNotBeNull();

            foreach (var plexServerConnectionDb in plexServerDb.PlexServerConnections)
            {
                var expectedConnection = expectedServer.PlexServerConnections.FirstOrDefault(x =>
                    x.Address == plexServerConnectionDb.Address
                );
                expectedConnection.ShouldNotBeNull();
                plexServerConnectionDb.Id.ShouldBe(expectedConnection.Id);
                plexServerConnectionDb.ShouldBe(expectedConnection);
            }
        }
    }

    [Test]
    public async Task ShouldUpdateSomeAndSyncServersWithConnections_WhenSomeServerConnectionsHaveChangedAndSomeExistInTheDatabase()
    {
        // Arrange
        var seed = await SetupDatabase(23724, config => config.PlexServerCount = 5);
        var plexServers = IDbContext.PlexServers.Include(x => x.PlexServerConnections).ToList();
        var changedPlexServers = FakeData.GetPlexServer(seed).Generate(3);

        var expectedPlexServers = new List<PlexServer>
        {
            changedPlexServers[0],
            changedPlexServers[1],
            changedPlexServers[2],
            plexServers[3],
            plexServers[4],
        };

        // Create updated servers with the same machineId while preserving persisted ownership semantics.
        for (var i = 0; i < changedPlexServers.Count; i++)
        {
            changedPlexServers[i].MachineIdentifier = plexServers[i].MachineIdentifier;

            // Handler preserves persisted OwnedOverride for existing servers;
            // incoming value is intentionally ignored during update.
            changedPlexServers[i].OwnedOverride = plexServers[i].OwnedOverride;
        }

        // Act
        // First add the 5 servers
        var request = new AddOrUpdatePlexServersCommand(plexServers);
        var addResult = await Sut.ExecuteAsync(request, CancellationToken);

        // Now update
        request = new AddOrUpdatePlexServersCommand(changedPlexServers);
        var updateResult = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        addResult.IsSuccess.ShouldBeTrue();
        updateResult.IsSuccess.ShouldBeTrue();
        var plexServersDbs = IDbContext.PlexServers.Include(x => x.PlexServerConnections).ToList();
        plexServersDbs.Count.ShouldBe(5);

        foreach (var expectedPlexServer in expectedPlexServers)
        {
            var plexServerDb = plexServersDbs.Find(x => x.MachineIdentifier == expectedPlexServer.MachineIdentifier);
            plexServerDb.ShouldNotBeNull();
            ShouldMatchPlexServer(plexServerDb, expectedPlexServer);
            plexServerDb.PlexServerConnections.Count.ShouldBe(expectedPlexServer.PlexServerConnections.Count);
            plexServerDb.PlexServerConnections.ShouldBe(expectedPlexServer.PlexServerConnections, true);
        }
    }

    [Test]
    public async Task ShouldSyncConnectionsAndKeepTheSameServerConnectionIds_WhenSomeHaveConnectionHaveChanged()
    {
        // Arrange
        var seed = await SetupDatabase(
            23724,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 5;
            }
        );

        var plexServer = IDbContext.PlexServers.Include(x => x.PlexServerConnections).FirstOrDefault();
        plexServer.ShouldNotBeNull();
        plexServer.PlexServerConnections.Count.ShouldBe(5);

        // Update data setup
        foreach (var connection in plexServer.PlexServerConnections.Take(4).ToList())
            plexServer.PlexServerConnections.Remove(connection);

        var newConnections = FakeData.GetPlexServerConnections(seed).Generate(5);
        plexServer.PlexServerConnections.AddRange(newConnections);

        // Act
        // Now update
        var request = new AddOrUpdatePlexServersCommand([plexServer]);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexServersDb = IDbContext.PlexServers.Include(x => x.PlexServerConnections).FirstOrDefault();
        plexServersDb.ShouldNotBeNull();
        plexServersDb.PlexServerConnections.Count.ShouldBe(6);

        foreach (var plexServerConnection in plexServersDb.PlexServerConnections)
        {
            var expectedConnection = plexServer.PlexServerConnections.FirstOrDefault(x =>
                x.Equals(plexServerConnection)
            );
            expectedConnection.ShouldNotBeNull();
            plexServerConnection.Id.ShouldBe(expectedConnection.Id);
            plexServerConnection.ShouldBe(expectedConnection);
        }
    }

    [Test]
    public async Task ShouldKeepCustomConnections_WhenSomeConnectionHaveChanged()
    {
        // Arrange
        var seed = await SetupDatabase(
            233324,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 5;
            }
        );

        var dbContext = IDbContext;
        var plexServer = IDbContext.PlexServers.Include(x => x.PlexServerConnections).FirstOrDefault();
        plexServer.ShouldNotBeNull();
        plexServer.PlexServerConnections.Count.ShouldBe(5);

        // Add custom connections
        var customConnections = FakeData
            .GetPlexServerConnections(seed, isCustom: true, plexServerId: plexServer.Id)
            .Generate(3);
        await dbContext.PlexServerConnections.AddRangeAsync(customConnections, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Update data setup
        foreach (var conn in plexServer.PlexServerConnections.Take(4).ToList())
            plexServer.PlexServerConnections.Remove(conn);

        var newConnections = FakeData.GetPlexServerConnections(seed).Generate(5);
        plexServer.PlexServerConnections.AddRange(newConnections);

        // Act
        // Now update
        var request = new AddOrUpdatePlexServersCommand([plexServer]);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        var checkConnections = new List<PlexServerConnection>();
        checkConnections.AddRange(plexServer.PlexServerConnections);
        checkConnections.AddRange(customConnections);

        result.IsSuccess.ShouldBeTrue();
        var plexServersDb = IDbContext.PlexServers.Include(x => x.PlexServerConnections).FirstOrDefault();
        plexServersDb.ShouldNotBeNull();
        plexServersDb.PlexServerConnections.Count.ShouldBe(checkConnections.Count);

        foreach (var plexServerConnection in plexServersDb.PlexServerConnections)
        {
            var expectedConnection = checkConnections.Find(x => x.Equals(plexServerConnection));
            expectedConnection.ShouldNotBeNull();
            plexServerConnection.Id.ShouldBe(expectedConnection.Id);
            plexServerConnection.ShouldBe(expectedConnection);
        }
    }

    private static void ShouldMatchPlexServer(PlexServer actual, PlexServer expected)
    {
        actual.Name.ShouldBe(expected.Name);
        actual.OwnerId.ShouldBe(expected.OwnerId);
        actual.PlexServerOwnerUsername.ShouldBe(expected.PlexServerOwnerUsername);
        actual.Device.ShouldBe(expected.Device);
        actual.Platform.ShouldBe(expected.Platform);
        actual.PlatformVersion.ShouldBe(expected.PlatformVersion);
        actual.Product.ShouldBe(expected.Product);
        actual.ProductVersion.ShouldBe(expected.ProductVersion);
        actual.Provides.ShouldBe(expected.Provides);
        actual.CreatedAt.ShouldBe(expected.CreatedAt.ToUniversalTime());
        actual.LastSeenAt.ShouldBe(expected.LastSeenAt.ToUniversalTime());
        actual.MachineIdentifier.ShouldBe(expected.MachineIdentifier);
        actual.PublicAddress.ShouldBe(expected.PublicAddress);
        actual.PreferredConnectionId.ShouldBe(expected.PreferredConnectionId);
        actual.OwnedOverride.ShouldBe(expected.OwnedOverride);
        actual.Synced.ShouldBe(expected.Synced);
        actual.Relay.ShouldBe(expected.Relay);
        actual.Presence.ShouldBe(expected.Presence);
        actual.HttpsRequired.ShouldBe(expected.HttpsRequired);
        actual.PublicAddressMatches.ShouldBe(expected.PublicAddressMatches);
        actual.DnsRebindingProtection.ShouldBe(expected.DnsRebindingProtection);
        actual.NatLoopbackSupported.ShouldBe(expected.NatLoopbackSupported);
    }
}
