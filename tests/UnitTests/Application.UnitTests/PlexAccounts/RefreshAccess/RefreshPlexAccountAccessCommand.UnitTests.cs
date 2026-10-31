namespace Reaparr.Application.UnitTests;

public class RefreshPlexAccountAccessCommandUnitTests : BaseUnitTest<RefreshPlexAccountAccessCommandHandler>
{
    [Test]
    public async Task ShouldReportFailureAndPreserveAccess_WhenRevocationTransactionFails()
    {
        // Arrange
        await SetupDatabase(
            77606,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var account = await dbContext.PlexAccounts.SingleAsync(CancellationToken);
        var beforeServers = await dbContext
            .PlexAccountServers.Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
            })
            .ToListAsync(CancellationToken);
        var beforeLibraries = await dbContext
            .PlexAccountLibraries.Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.PlexLibraryId,
            })
            .ToListAsync(CancellationToken);
        beforeServers.Single().PlexAccountId.ShouldBe(account.Id);
        beforeLibraries.Single().PlexAccountId.ShouldBe(account.Id);
        await ((DbContext)dbContext).Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectAccessRevocation BEFORE DELETE ON PlexAccountServers "
                + "BEGIN SELECT RAISE(ABORT, 'Injected revocation failure'); END;",
            CancellationToken
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ValidatePlexTokenCommand>(c => c.AuthenticationToken == account.GetAuthToken),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Fail<ValidatePlexTokenCommandResult>("Unauthorized").AddPlex401UnauthorizedError())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new RefreshPlexAccountAccessCommand(account.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<ExceptionalError>();
        (await dbContext.PlexAccounts.SingleAsync(CancellationToken)).IsValidated.ShouldBeFalse();
        (
            await dbContext
                .PlexAccountServers.Select(x => new
                {
                    x.PlexAccountId,
                    x.PlexServerId,
                    x.AuthToken,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(beforeServers);
        (
            await dbContext
                .PlexAccountLibraries.Select(x => new
                {
                    x.PlexAccountId,
                    x.PlexServerId,
                    x.PlexLibraryId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(beforeLibraries);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<RefreshPlexServerAccessCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryAccessCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments("validation", false, false)]
    [Arguments("server", false, false)]
    [Arguments("library", false, false)]
    [Arguments("validation", true, false)]
    [Arguments("server", true, false)]
    [Arguments("library", true, false)]
    [Arguments("validation", false, true)]
    [Arguments("server", false, true)]
    [Arguments("library", false, true)]
    public async Task ShouldPreserveStageFailureAndSuccessfulAccountWork_WhenRefreshCannotFullyComplete(
        string failingStage,
        bool includeSuccessfulAccount,
        bool cancelled
    )
    {
        // Arrange
        await SetupDatabase(77604, config => config.PlexAccountCount = includeSuccessfulAccount ? 2 : 1);
        var dbContext = IDbContext;
        await dbContext.PlexAccounts.ExecuteUpdateAsync(
            x => x.SetProperty(a => a.IsValidated, false).SetProperty(a => a.ValidatedAt, (DateTime?)null),
            CancellationToken
        );
        var accounts = await dbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var targetId = accounts[0].Id;
        accounts.Select(x => x.GetAuthToken).Distinct().Count().ShouldBe(accounts.Count);
        var before = await dbContext
            .PlexAccountServers.OrderBy(x => x.PlexAccountId)
            .ThenBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
            })
            .ToListAsync(CancellationToken);
        var failureResult = cancelled
            ? ResultExtensions.TaskIsCancelled("refresh-stage")
            : Result.Fail(new Error("Refresh stage failed").WithMetadata("Stage", failingStage));
        var failure = failureResult.Errors.Single();
        foreach (var account in accounts)
        {
            var isTarget = account.Id == targetId;
            var validation = new ValidatePlexTokenCommandResult
            {
                ClientId = account.ClientId,
                Username = account.Username,
                Email = account.Email,
                Title = account.Title,
                PlexId = account.PlexId,
                Uuid = account.Uuid,
                AuthenticationToken = account.GetAuthToken,
                IsValidated = true,
                ValidatedAt = DateTime.UtcNow,
                Is2Fa = account.Is2Fa,
            };
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<ValidatePlexTokenCommand>(c => c.AuthenticationToken == account.GetAuthToken),
                        CancellationToken
                    )
                )
                .ReturnsAsync(
                    isTarget && failingStage == "validation"
                        ? failureResult.ToResult<ValidatePlexTokenCommandResult>()
                        : Result.Ok(validation)
                )
                .Verifiable(Times.Once());
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(It.Is<RefreshPlexServerAccessCommand>(c => c.PlexAccountId == account.Id), CancellationToken)
                )
                .ReturnsAsync(
                    isTarget && failingStage == "server"
                        ? failureResult.ToResult<RefreshPlexServerAccessRapport>()
                        : Result.Ok(new RefreshPlexServerAccessRapport(account.Id, account.DisplayName))
                )
                .Verifiable(isTarget && failingStage == "validation" ? Times.Never() : Times.Once());
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(It.Is<RefreshLibraryAccessCommand>(c => c.PlexAccountId == account.Id), CancellationToken)
                )
                .ReturnsAsync(
                    isTarget && failingStage == "library"
                        ? failureResult.ToResult<PlexLibraryAccessRefreshResponse>()
                        : Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] })
                )
                .Verifiable(isTarget && failingStage != "library" ? Times.Never() : Times.Once());
        }
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[]
                            {
                                RefreshDataType.PlexAccount,
                                RefreshDataType.PlexServer,
                                RefreshDataType.PlexServerConnection,
                                RefreshDataType.PlexLibrary,
                            }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(cancelled ? Times.Never() : Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(cancelled ? Times.Never() : Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new RefreshPlexAccountAccessCommand(), CancellationToken);

        // Assert
        result.IsCancelled.ShouldBe(cancelled);
        if (!cancelled)
            result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldBe([failure]);
        var after = await dbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        after.Select(x => x.Id).ShouldBe(accounts.Select(x => x.Id));
        after.Single(x => x.Id == targetId).IsValidated.ShouldBe(failingStage != "validation");
        if (includeSuccessfulAccount)
        {
            after.Single(x => x.Id != targetId).IsValidated.ShouldBeTrue();
            after.Single(x => x.Id != targetId).ValidatedAt.ShouldNotBeNull();
        }
        (
            await dbContext
                .PlexAccountServers.OrderBy(x => x.PlexAccountId)
                .ThenBy(x => x.PlexServerId)
                .Select(x => new
                {
                    x.PlexAccountId,
                    x.PlexServerId,
                    x.AuthToken,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

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
