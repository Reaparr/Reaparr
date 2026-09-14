namespace Reaparr.Application.UnitTests;

public class RefreshPlexAccountAccessCommandUnitTests : BaseUnitTest<RefreshPlexAccountAccessCommandHandler>
{
    [Test]
    public async Task ShouldValidateAndRefreshServerAndLibraryAccess_WhenTokenIsValid()
    {
        // Arrange
        await SetupDatabase(77601, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.FirstAsync(CancellationToken);
        var serverRapport = new RefreshPlexServerAccessRapport(plexAccount.Id, plexAccount.DisplayName);
        serverRapport.Access.Add(new RefreshPlexServerAccessRapportRow(PlexAccessState.Updated, 1, "Server"));

        Mock.SetupCommand(It.IsAny<ValidatePlexTokenCommand>)
            .ReturnsAsync(
                Result.Ok(
                    new ValidatePlexTokenCommandResult
                    {
                        ClientId = plexAccount.ClientId,
                        Username = plexAccount.Username,
                        Email = plexAccount.Email,
                        Title = plexAccount.Title,
                        PlexId = plexAccount.PlexId,
                        Uuid = plexAccount.Uuid,
                        AuthenticationToken = plexAccount.GetAuthToken,
                        IsValidated = true,
                        ValidatedAt = DateTime.UtcNow,
                        Is2Fa = plexAccount.Is2Fa,
                    }
                )
            );
        Mock.SetupCommand(It.IsAny<RefreshPlexServerAccessCommand>).ReturnsAsync(Result.Ok(serverRapport));
        Mock.SetupCommand(It.IsAny<RefreshLibraryAccessCommand>)
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] }));
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new RefreshPlexAccountAccessCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ValidatePlexTokenCommand>(), It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<RefreshPlexServerAccessCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryAccessCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldRemoveLibraryAccessForRevokedServer_WhenOtherServerAccessRemains()
    {
        // Arrange
        await SetupDatabase(
            77603,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexAccount = await IDbContext.PlexAccounts.FirstAsync(CancellationToken);
        var plexServers = await IDbContext.PlexServers.OrderBy(x => x.Id).Take(2).ToListAsync(CancellationToken);
        var retainedServer = plexServers[0];
        var revokedServer = plexServers[1];
        var revokedLibrary = await IDbContext
            .PlexLibraries.IgnoreQueryFilters()
            .FirstAsync(x => x.PlexServerId == revokedServer.Id, CancellationToken);

        IDbContext.PlexAccountServers.AddRange(
            new PlexAccountServer
            {
                PlexAccountId = plexAccount.Id,
                PlexServerId = retainedServer.Id,
                AuthToken = "retained-token",
                AuthTokenCreationDate = DateTime.UtcNow,
                IsServerOwned = false,
            },
            new PlexAccountServer
            {
                PlexAccountId = plexAccount.Id,
                PlexServerId = revokedServer.Id,
                AuthToken = "revoked-token",
                AuthTokenCreationDate = DateTime.UtcNow,
                IsServerOwned = false,
            }
        );
        IDbContext.PlexAccountLibraries.Add(
            new PlexAccountLibrary
            {
                PlexAccountId = plexAccount.Id,
                PlexServerId = revokedServer.Id,
                PlexLibraryId = revokedLibrary.Id,
                IsLibraryOwned = false,
            }
        );
        await IDbContext.SaveChangesAsync(CancellationToken);

        var serverRapport = new RefreshPlexServerAccessRapport(plexAccount.Id, plexAccount.DisplayName);
        serverRapport.AddUpdated(retainedServer.Id, retainedServer.Name);
        serverRapport.AddRevoked(revokedServer.Id, revokedServer.Name);

        Mock.SetupCommand<Result<ValidatePlexTokenCommandResult>>(x => x is ValidatePlexTokenCommand)
            .ReturnsAsync(
                Result.Ok(
                    new ValidatePlexTokenCommandResult
                    {
                        ClientId = plexAccount.ClientId,
                        Username = plexAccount.Username,
                        Email = plexAccount.Email,
                        Title = plexAccount.Title,
                        PlexId = plexAccount.PlexId,
                        Uuid = plexAccount.Uuid,
                        AuthenticationToken = plexAccount.GetAuthToken,
                        IsValidated = true,
                        ValidatedAt = DateTime.UtcNow,
                        Is2Fa = plexAccount.Is2Fa,
                    }
                )
            );
        Mock.SetupCommand<Result<RefreshPlexServerAccessRapport>>(x => x is RefreshPlexServerAccessCommand)
            .ReturnsAsync(Result.Ok(serverRapport));
        Mock.SetupCommand<Result<PlexLibraryAccessRefreshResponse>>(x => x is RefreshLibraryAccessCommand)
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] }));
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new RefreshPlexAccountAccessCommand(plexAccount.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (
            await IDbContext.PlexAccountLibraries.AnyAsync(
                x => x.PlexAccountId == plexAccount.Id && x.PlexServerId == revokedServer.Id,
                CancellationToken
            )
        ).ShouldBeFalse();
        result
            .Value.Single()
            .Access.Single(x => x.PlexServerId == revokedServer.Id)
            .State.ShouldBe(PlexAccessState.Revoked);
    }

    [Test]
    public async Task ShouldMarkAccountInvalidAndKeepEnabled_WhenTokenIsUnauthorized()
    {
        // Arrange
        await SetupDatabase(77602, config => config.PlexAccountCount = 1);
        var plexAccount = await IDbContext.PlexAccounts.AsTracking().FirstAsync(CancellationToken);
        plexAccount.IsValidated = true;
        plexAccount.ValidatedAt = DateTime.UtcNow;
        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<ValidatePlexTokenCommand>)
            .ReturnsAsync(Result.Fail<ValidatePlexTokenCommandResult>("Unauthorized").AddPlex401UnauthorizedError());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new RefreshPlexAccountAccessCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        IDbContext.ClearChangeTracker();
        var updatedAccount = await IDbContext.PlexAccounts.AsNoTracking().FirstAsync(CancellationToken);
        updatedAccount.IsValidated.ShouldBeFalse();
        updatedAccount.ValidatedAt.ShouldBeNull();
        updatedAccount.IsEnabled.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<RefreshPlexServerAccessCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryAccessCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
