namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsPlexServerUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldReturnServerName_WhenPlexServerExists()
    {
        // Arrange
        await SetupDatabase(
            12001,
            cfg =>
            {
                cfg.PlexServerCount = 1;
            }
        );

        var server = IDbContext.PlexServers.FirstOrDefault();
        server.ShouldNotBeNull();

        // Act
        var name = await IDbContext.GetPlexServerNameById(server.Id);

        // Assert
        name.ShouldBe(server.Name);
    }

    [Test]
    public async Task ShouldReturnServerNameNotFound_WhenPlexServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(12002);

        // Act
        var name = await IDbContext.GetPlexServerNameById(9999);

        // Assert
        name.ShouldBe("Server Name Not Found");
    }

    [Test]
    public async Task ShouldReturnMachineIdentifier_WhenPlexServerExists()
    {
        // Arrange
        await SetupDatabase(
            12003,
            cfg =>
            {
                cfg.PlexServerCount = 1;
            }
        );

        var server = IDbContext.PlexServers.FirstOrDefault();
        server.ShouldNotBeNull();

        // Act
        var machineId = await IDbContext.GetPlexServerMachineIdentifierById(server.Id);

        // Assert
        machineId.ShouldBe(server.MachineIdentifier);
    }

    [Test]
    public async Task ShouldReturnEmptyMachineIdentifier_WhenPlexServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(12004);

        // Act
        var machineId = await IDbContext.GetPlexServerMachineIdentifierById(9999);

        // Assert
        machineId.ShouldBe(string.Empty);
    }

    [Test]
    public async Task ShouldReturnTrueForIsServerOnline_WhenThereIsASuccessfulStatus()
    {
        // Arrange
        await SetupDatabase(
            12005,
            cfg =>
            {
                cfg.PlexServerCount = 1;
            }
        );
        var server = IDbContext.PlexServers.FirstOrDefault();
        server.ShouldNotBeNull();

        // Act
        var isOnline = await IDbContext.IsServerOnline(server.Id);

        // Assert
        isOnline.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnFalseForIsServerOnline_WhenThereAreNoStatuses()
    {
        // Arrange
        await SetupDatabase(
            12006,
            cfg =>
            {
                cfg.PlexServerCount = 1;
            }
        );
        var server = IDbContext.PlexServers.FirstOrDefault();
        server.ShouldNotBeNull();

        await IDbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        // Act
        var isOnline = await IDbContext.IsServerOnline(server.Id);

        // Assert
        isOnline.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnOnlyEnabledUnpausedServersWithSuccessfulStatuses_WhenGettingDownloadableServerIds()
    {
        // Arrange
        await SetupDatabase(
            12010,
            cfg =>
            {
                cfg.PlexServerCount = 4;
            }
        );

        var dbContext = IDbContext;
        var servers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var activeServer = servers[0];
        var pausedServer = servers[1];
        var disabledServer = servers[2];
        var offlineServer = servers[3];

        await dbContext
            .PlexServers.Where(x => x.Id == pausedServer.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDownloadsPausedByUser, true), CancellationToken);
        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == disabledServer.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        await dbContext
            .PlexServerStatuses.Where(x => x.PlexServerId == offlineServer.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, false), CancellationToken);

        // Act
        var result = await dbContext.GetDownloadableServerIds();

        // Assert
        result.ShouldBe([activeServer.Id]);
    }

    [Test]
    public async Task ShouldReturnTrueForIsServerDisabled_WhenServerExistsAndIsDisabled()
    {
        // Arrange
        await SetupDatabase(12007, cfg => cfg.PlexServerCount = 1);

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        // Act
        var result = await dbContext.IsServerDisabled(server.Id);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnFalseForIsServerDisabled_WhenServerExistsAndIsEnabled()
    {
        // Arrange
        await SetupDatabase(12008, cfg => cfg.PlexServerCount = 1);

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        // Act
        var result = await dbContext.IsServerDisabled(server.Id);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnFalseForIsServerDisabled_WhenServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(12009);

        // Act
        var result = await IDbContext.IsServerDisabled(99999);

        // Assert
        result.ShouldBeFalse();
    }
}
