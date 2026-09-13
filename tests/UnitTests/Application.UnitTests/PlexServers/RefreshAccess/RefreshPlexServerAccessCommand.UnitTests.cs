namespace Reaparr.Application.UnitTests;

public class RefreshPlexServerAccessCommandUnitTests : BaseUnitTest<RefreshPlexServerAccessCommandHandler>
{
    [Test]
    public async Task ShouldReturnOkResult_WhenThereAreNoAccessiblePlexServers()
    {
        // Arrange
        await SetupDatabase(
            66197,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );

        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetAccessiblePlexServersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlexServerAccessDTO>())
            .Verifiable(Times.Once);

        // Act
        var request = new RefreshPlexServerAccessCommand(plexAccount.Id);
        var handler = Mock.Create<RefreshPlexServerAccessCommandHandler>();
        var result = await handler.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldInvalidateAffectedLibraries_WhenAccessibleServersAreRevoked()
    {
        // Arrange
        await SetupDatabase(
            66198,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
                config.PlexTvShowLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = await dbContext.PlexAccounts.FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetAccessiblePlexServersCommand>(command => command.PlexAccountId == plexAccount.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<PlexServerAccessDTO>());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand()).ReturnsAsync(Result.Ok());

        // Act
        var result = await Mock.Create<RefreshPlexServerAccessCommandHandler>()
            .ExecuteAsync(new RefreshPlexServerAccessCommand(plexAccount.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Access.Count.ShouldBe(2);
        (
            await dbContext.PlexAccountServers.AnyAsync(x => x.PlexAccountId == plexAccount.Id, CancellationToken)
        ).ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldReturnOkResult_WhenThereAreAccessiblePlexServers()
    {
        // Arrange
        var seed = await SetupDatabase(
            65148,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );

        var plexAccount = await IDbContext.PlexAccounts.FirstOrDefaultAsync(CancellationToken);
        plexAccount.ShouldNotBeNull();

        var plexServers = FakeData.GetPlexServer(seed).Generate(10);
        var serverAccessTokens = FakeData.GetServerAccessTokenDTO(seed, plexAccount, plexServers);

        var list = plexServers
            .Select(x => new PlexServerAccessDTO
            {
                PlexServer = x,
                AccessToken = serverAccessTokens.FirstOrDefault(y => y.MachineIdentifier == x.MachineIdentifier)!,
            })
            .ToList();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetAccessiblePlexServersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(list));

        Mock.SetupCommand(It.IsAny<AddOrUpdatePlexServersCommand>).ReturnsAsync(Result.Ok());
        Mock.SetupCommand(It.IsAny<AddOrUpdatePlexAccountServersCommand>)
            .ReturnsAsync(Result.Ok(new RefreshPlexServerAccessRapport(plexAccount.Id, plexAccount.DisplayName)));
        Mock.SetupCommand(It.IsAny<RefreshLibraryAccessCommand>)
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { OfflineServers = [], Reports = [] }));

        Mock.SendRefreshNotification();

        // Act

        var request = new RefreshPlexServerAccessCommand(plexAccount.Id);
        var handler = Mock.Create<RefreshPlexServerAccessCommandHandler>();
        var result = await handler.ExecuteAsync(request, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
