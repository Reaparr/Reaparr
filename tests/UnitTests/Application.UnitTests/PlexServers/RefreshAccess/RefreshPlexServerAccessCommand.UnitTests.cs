namespace Reaparr.Application.UnitTests;

public class RefreshPlexServerAccessCommandUnitTests : BaseCommandUnitTest<RefreshPlexServerAccessCommand>
{
    [Test]
    public async Task ShouldRemoveServerAndLibraryAccess_WhenNoAccessiblePlexServersAreReturned()
    {
        // Arrange
        await SetupDatabase(
            66197,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = await dbContext.PlexAccounts.FirstAsync(CancellationToken);
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        (
            await dbContext.PlexAccountLibraries.AnyAsync(
                x => x.PlexAccountId == plexAccount.Id && x.PlexLibraryId == libraryId,
                CancellationToken
            )
        ).ShouldBeTrue();

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetAccessiblePlexServersCommand>(command => command.PlexAccountId == plexAccount.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new List<PlexServerAccessDTO>()))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new RefreshPlexServerAccessCommand(plexAccount.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Access.Count.ShouldBe(1);
        (
            await dbContext.PlexAccountServers.AnyAsync(x => x.PlexAccountId == plexAccount.Id, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexAccountLibraries.AnyAsync(x => x.PlexAccountId == plexAccount.Id, CancellationToken)
        ).ShouldBeFalse();
        (await dbContext.PlexLibraries.CountAsync(CancellationToken)).ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldRemoveOnlyTargetAccountAccess_WhenAllAccessibleServersAreRevoked()
    {
        // Arrange
        await SetupDatabase(
            66198,
            config =>
            {
                config.PlexAccountCount = 2;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
                config.PlexTvShowLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var plexAccounts = await dbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var revokedAccount = plexAccounts[0];
        var retainedAccount = plexAccounts[1];
        (
            await dbContext.PlexAccountLibraries.CountAsync(
                x => x.PlexAccountId == revokedAccount.Id,
                CancellationToken
            )
        ).ShouldBeGreaterThan(0);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetAccessiblePlexServersCommand>(command => command.PlexAccountId == revokedAccount.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new List<PlexServerAccessDTO>()))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new RefreshPlexServerAccessCommand(revokedAccount.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Access.Count.ShouldBe(2);
        (
            await dbContext.PlexAccountServers.AnyAsync(x => x.PlexAccountId == revokedAccount.Id, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexAccountLibraries.AnyAsync(x => x.PlexAccountId == revokedAccount.Id, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexAccountServers.AnyAsync(x => x.PlexAccountId == retainedAccount.Id, CancellationToken)
        ).ShouldBeTrue();
        (
            await dbContext.PlexAccountLibraries.AnyAsync(x => x.PlexAccountId == retainedAccount.Id, CancellationToken)
        ).ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldRemoveServerAndLibraryAccess_WhenPlexReturnsUnauthorized()
    {
        // Arrange
        await SetupDatabase(
            66199,
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
        (
            await dbContext.PlexAccountLibraries.AnyAsync(x => x.PlexAccountId == plexAccount.Id, CancellationToken)
        ).ShouldBeTrue();

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetAccessiblePlexServersCommand>(command => command.PlexAccountId == plexAccount.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result.Fail<List<PlexServerAccessDTO>>("Unauthorized").AddPlex401UnauthorizedError()
            )
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new RefreshPlexServerAccessCommand(plexAccount.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Access.Count.ShouldBe(2);
        (
            await dbContext.PlexAccountServers.AnyAsync(x => x.PlexAccountId == plexAccount.Id, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexAccountLibraries.AnyAsync(x => x.PlexAccountId == plexAccount.Id, CancellationToken)
        ).ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify();
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
            .ReturnsAsync(Result.Ok(list))
            .Verifiable(Times.Once());
        Mock.SetupCommand<Result<PlexServerRapport>>(x => x is AddOrUpdatePlexServersCommand)
            .ReturnsAsync(Result.Ok(new PlexServerRapport()))
            .Verifiable(Times.Once());
        Mock.SetupCommand<Result<RefreshPlexServerAccessRapport>>(x => x is AddOrUpdatePlexAccountServersCommand)
            .ReturnsAsync(Result.Ok(new RefreshPlexServerAccessRapport(plexAccount.Id, plexAccount.DisplayName)))
            .Verifiable(Times.Once());
        Mock.SendRefreshNotification(isVerifiable: true);

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new RefreshPlexServerAccessCommand(plexAccount.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }
}

