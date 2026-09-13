namespace Reaparr.Application.UnitTests;

public class AddOrUpdatePlexAccountServersCommandHandlerUnitTests
    : BaseUnitTest<AddOrUpdatePlexAccountServersCommandHandler>
{
    [Test]
    public async Task ShouldAddPlexAccountServerAssociations_WhenNoneExistsYet()
    {
        // Arrange
        var seed = await SetupDatabase(
            583,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 5;
            }
        );

        var plexAccount = IDbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServers = IDbContext.PlexServers.ToList();
        var serverAccessTokens = FakeData.GetServerAccessTokenDTO(seed, plexAccount, plexServers);

        // Remove all associations
        await IDbContext.PlexAccountServers.ExecuteDeleteAsync(CancellationToken);

        Mock.Mock<IMediaOverviewReadStore>()
            .Setup(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var request = new AddOrUpdatePlexAccountServersCommand(plexAccount.Id, serverAccessTokens);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IMediaOverviewReadStore>().Verify(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Once());
        var plexAccountServers = IDbContext.PlexAccountServers.Include(x => x.PlexServer).ToList();
        plexAccountServers.Count.ShouldBe(serverAccessTokens.Count);

        foreach (var serverAccessToken in serverAccessTokens)
            plexAccountServers
                .Any(x =>
                    x.PlexServer?.MachineIdentifier == serverAccessToken.MachineIdentifier
                    && x.AuthToken == serverAccessToken.AccessToken
                    && x.PlexAccountId == plexAccount.Id
                )
                .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldUpdateAndDeletePlexAccountServerAssociations_WhenTheyAreNotGiven()
    {
        // Arrange
        var seed = await SetupDatabase(
            194732,
            config =>
            {
                config.PlexServerCount = 5;
                config.PlexAccountCount = 1;
            }
        );

        var plexAccount = IDbContext.PlexAccounts.FirstOrDefault();
        var plexServers = IDbContext.PlexServers.ToList();

        plexAccount.ShouldNotBeNull();

        var serverAccessTokens = FakeData.GetServerAccessTokenDTO(seed, plexAccount, plexServers);

        serverAccessTokens.RemoveRange(1, 2);
        serverAccessTokens.ForEach(x => x.AccessToken = "######");

        // Remove all associations
        await IDbContext.PlexAccountServers.ExecuteDeleteAsync(CancellationToken);

        Mock.Mock<IMediaOverviewReadStore>()
            .Setup(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var request = new AddOrUpdatePlexAccountServersCommand(plexAccount.Id, serverAccessTokens);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IMediaOverviewReadStore>().Verify(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Once());
        var plexAccountServers = IDbContext
            .PlexAccountServers.Include(x => x.PlexServer)
            .Include(x => x.PlexAccount)
            .ToList();
        plexAccountServers.Count.ShouldBe(serverAccessTokens.Count);

        foreach (var serverAccessToken in serverAccessTokens)
            plexAccountServers
                .Any(x =>
                    x.PlexServer?.MachineIdentifier == serverAccessToken.MachineIdentifier
                    && x.AuthToken == "######"
                    && x.PlexAccountId == plexAccount.Id
                )
                .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldInvalidateServerLibraries_WhenServerAccessChanges()
    {
        // Arrange
        var seed = await SetupDatabase(
            33383,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var plexAccount = IDbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServers = IDbContext.PlexServers.IgnoreIsEnabledFilter().OrderBy(x => x.Id).ToList();
        var serverAccessTokens = FakeData.GetServerAccessTokenDTO(seed, plexAccount, plexServers);
        var expectedLibraryIds = IDbContext
            .PlexLibraries.IgnoreQueryFilters()
            .Where(x => plexServers.Select(y => y.Id).Contains(x.PlexServerId))
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToList();

        Mock.Mock<IMediaOverviewReadStore>()
            .Setup(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var request = new AddOrUpdatePlexAccountServersCommand(plexAccount.Id, serverAccessTokens);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IMediaOverviewReadStore>().Verify(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldNotAddPlexAccountServerAssociations_WhenAuthTokenIsEmpty()
    {
        // Arrange
        var seed = await SetupDatabase(
            33382,
            config =>
            {
                config.PlexServerCount = 5;
                config.PlexAccountCount = 1;
            }
        );

        var plexAccount = IDbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServers = IDbContext.PlexServers.ToList();
        var serverAccessTokens = FakeData.GetServerAccessTokenDTO(seed, plexAccount, plexServers);

        serverAccessTokens[0].AccessToken = string.Empty;
        serverAccessTokens[1].AccessToken = string.Empty;

        // Remove all associations
        await IDbContext.PlexAccountServers.ExecuteDeleteAsync(CancellationToken);

        Mock.Mock<IMediaOverviewReadStore>()
            .Setup(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var request = new AddOrUpdatePlexAccountServersCommand(plexAccount.Id, serverAccessTokens);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IMediaOverviewReadStore>().Verify(x => x.RebuildAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Once());
        var plexAccountServers = IDbContext.PlexAccountServers.Include(x => x.PlexServer).ToList();
        plexAccountServers.Count.ShouldBe(3);

        foreach (var serverAccessToken in serverAccessTokens)
            plexAccountServers
                .Any(x =>
                    x.PlexServer?.MachineIdentifier == serverAccessToken.MachineIdentifier
                    && x.PlexAccountId == plexAccount.Id
                )
                .ShouldBeTrue();
    }
}
