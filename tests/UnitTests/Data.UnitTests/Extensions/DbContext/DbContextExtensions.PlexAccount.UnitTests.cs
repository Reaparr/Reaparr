namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsPlexAccountUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldGetAccessibleServers_WhenAccountHasServers()
    {
        // Arrange
        await SetupDatabase(12200, cfg =>
        {
            cfg.PlexServerCount = 2;
            cfg.PlexAccountCount = 1;
        });
        var account = IDbContext.PlexAccounts.First();

        // Act
        var result = await IDbContext.GetAccessiblePlexServers(account.Id, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
    }

    [Test]
    public async Task ShouldFail_GetAccessibleServers_WhenAccountMissing()
    {
        // Arrange
        await SetupDatabase(12201);

        // Act
        var result = await IDbContext.GetAccessiblePlexServers(9999, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldChooseNonMainAccount_WhenMultipleEnabledAccounts()
    {
        // Arrange
        var seed = await SetupDatabase(12202, cfg =>
        {
            cfg.PlexServerCount = 1;
            cfg.PlexAccountCount = 1;
        });
        var db = IDbContext;
        var server = db.PlexServers.First();

        var nonMain = FakeData.GetPlexAccount(seed).Generate();
        nonMain.UpdateInitProperty(nameof(PlexAccount.IsMain), false);
        db.PlexAccounts.Add(nonMain);
        await db.SaveChangesAsync(CancellationToken);

        db.PlexAccountServers.Add(
            new PlexAccountServer
            {
                PlexAccountId = nonMain.Id,
                PlexServerId = server.Id,
                AuthToken = "TOKEN",
                AuthTokenCreationDate = DateTime.UtcNow,
                IsServerOwned = false,
            }
        );
        await db.SaveChangesAsync(CancellationToken);

        // Act
        var result = await db.ChoosePlexAccountToConnect(server.Id, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(nonMain.Id);
    }

    [Test]
    public async Task ShouldReturnDisplayName_WhenAccountExists()
    {
        // Arrange
        await SetupDatabase(12203, cfg =>
        {
            cfg.PlexAccountCount = 1;
        });
        var account = IDbContext.PlexAccounts.First();

        // Act
        var displayName = await IDbContext.GetPlexAccountDisplayName(account.Id, CancellationToken);

        // Assert
        displayName.ShouldBe(account.DisplayName);
    }

    [Test]
    public async Task ShouldDetectTakenUsername_WhenAccountExists()
    {
        // Arrange
        await SetupDatabase(12204, cfg =>
        {
            cfg.PlexAccountCount = 1;
        });
        var account = IDbContext.PlexAccounts.First();

        // Act
        var available = await IDbContext.IsUsernameAvailable(account.Username, CancellationToken);

        // Assert
        available.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnAccountsWithAccess_WhenServerHasAssociations()
    {
        // Arrange
        await SetupDatabase(12205, cfg =>
        {
            cfg.PlexServerCount = 1;
            cfg.PlexAccountCount = 1;
        });
        var server = IDbContext.PlexServers.First();

        // Act
        var result = await IDbContext.GetPlexAccountsWithAccessAsync(server.Id, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBeGreaterThan(0);
    }
}
