namespace Reaparr.Application.UnitTests;

public class AddOrUpdatePlexAccountServersCommandHandlerUnitTests
    : BaseCommandUnitTest<AddOrUpdatePlexAccountServersCommand>
{
    [Test]
    public async Task ShouldRejectCommand_WhenPlexAccountIdIsInvalid()
    {
        // Arrange
        var command = new AddOrUpdatePlexAccountServersCommand(0, []);

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldReturnNotFoundAndPreserveAccess_WhenPlexAccountDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            71001,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
            }
        );
        var plexServers = await IDbContext.PlexServers.OrderBy(x => x.Id).ToListAsync();
        var missingPlexAccountId = 999;
        var before = await IDbContext
            .PlexAccountServers.OrderBy(x => x.PlexAccountId)
            .ThenBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        var command = new AddOrUpdatePlexAccountServersCommand(
            missingPlexAccountId,
            [
                new ServerAccessTokenDTO
                {
                    PlexAccountId = missingPlexAccountId,
                    MachineIdentifier = plexServers[0].MachineIdentifier,
                    AccessToken = "missing-account-token",
                    IsServerOwned = true,
                },
            ]
        );

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
        var after = await IDbContext
            .PlexAccountServers.OrderBy(x => x.PlexAccountId)
            .ThenBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        after.ShouldBe(before);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldAddExactAssociations_WhenAccountHasNoServerAccess()
    {
        // Arrange
        await SetupDatabase(
            71002,
            config =>
            {
                config.PlexAccountCount = 2;
                config.PlexServerCount = 3;
            }
        );
        var plexAccounts = await IDbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync();
        var plexServers = await IDbContext.PlexServers.OrderBy(x => x.Id).ToListAsync();
        var targetAccount = plexAccounts[0];
        var controlAccount = plexAccounts[1];
        await IDbContext.PlexAccountServers.Where(x => x.PlexAccountId == targetAccount.Id).ExecuteDeleteAsync();
        var targetBefore = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == targetAccount.Id)
            .ToListAsync();
        targetBefore.ShouldBeEmpty();
        var controlBefore = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == controlAccount.Id)
            .OrderBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        controlBefore.Count.ShouldBe(plexServers.Count);
        var serverAccessTokens = plexServers
            .Select(
                (server, index) =>
                    new ServerAccessTokenDTO
                    {
                        PlexAccountId = targetAccount.Id,
                        MachineIdentifier = server.MachineIdentifier,
                        AccessToken = $"new-token-{server.Id}",
                        IsServerOwned = index % 2 == 0,
                    }
            )
            .ToList();
        var cancellationToken = CancellationToken;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueMediaOverviewRebuildCommand>(command =>
                        command == new QueueMediaOverviewRebuildCommand()
                    ),
                    It.Is<CancellationToken>(token => token == cancellationToken)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var startedAt = DateTime.UtcNow;

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new AddOrUpdatePlexAccountServersCommand(targetAccount.Id, serverAccessTokens)
        );
        var completedAt = DateTime.UtcNow;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.PlexAccountId.ShouldBe(targetAccount.Id);
        result.Value.PlexAccountName.ShouldBe(targetAccount.DisplayName);
        var expectedRapport = plexServers
            .Select(server => new RefreshPlexServerAccessRapportRow(PlexAccessState.Granted, server.Id, server.Name))
            .OrderBy(x => x.PlexServerId)
            .ToList();
        result.Value.Access.OrderBy(x => x.PlexServerId).ToList().ShouldBe(expectedRapport);

        var targetAfter = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == targetAccount.Id)
            .OrderBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.AuthTokenCreationDate,
                x.IsServerOwned,
            })
            .ToListAsync();
        var expectedTargetAfter = serverAccessTokens
            .Join(
                plexServers,
                token => token.MachineIdentifier,
                server => server.MachineIdentifier,
                (token, server) =>
                    new
                    {
                        PlexAccountId = targetAccount.Id,
                        PlexServerId = server.Id,
                        AuthToken = token.AccessToken,
                        token.IsServerOwned,
                    }
            )
            .OrderBy(x => x.PlexServerId)
            .ToList();
        targetAfter
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToList()
            .ShouldBe(expectedTargetAfter);
        targetAfter.ShouldAllBe(x => x.AuthTokenCreationDate >= startedAt && x.AuthTokenCreationDate <= completedAt);
        var controlAfter = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == controlAccount.Id)
            .OrderBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        controlAfter.ShouldBe(controlBefore);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldUpdateRetainedAndRevokeMissingAssociations_WhenAccessSetChanges()
    {
        // Arrange
        await SetupDatabase(
            71003,
            config =>
            {
                config.PlexAccountCount = 2;
                config.PlexServerCount = 3;
            }
        );
        var plexAccounts = await IDbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync();
        var plexServers = await IDbContext.PlexServers.OrderBy(x => x.Id).ToListAsync();
        var targetAccount = plexAccounts[0];
        var controlAccount = plexAccounts[1];
        var targetLinks = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == targetAccount.Id)
            .OrderBy(x => x.PlexServerId)
            .ToListAsync();
        targetLinks.Count.ShouldBe(plexServers.Count);
        foreach (var link in targetLinks)
        {
            link.AuthToken = $"old-token-{link.PlexServerId}";
            link.AuthTokenCreationDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            link.IsServerOwned = true;
        }
        await IDbContext.SaveChangesAsync();
        var targetBefore = targetLinks
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToList();
        targetBefore.Select(x => x.PlexServerId).ShouldBe(plexServers.Select(x => x.Id));
        var controlBefore = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == controlAccount.Id)
            .OrderBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        controlBefore.Count.ShouldBe(plexServers.Count);
        var retainedServers = new[] { plexServers[0], plexServers[2] };
        var serverAccessTokens = retainedServers
            .Select(
                (server, index) =>
                    new ServerAccessTokenDTO
                    {
                        PlexAccountId = targetAccount.Id,
                        MachineIdentifier = server.MachineIdentifier,
                        AccessToken = $"updated-token-{server.Id}",
                        IsServerOwned = index == 1,
                    }
            )
            .ToList();
        var cancellationToken = CancellationToken;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueMediaOverviewRebuildCommand>(command =>
                        command == new QueueMediaOverviewRebuildCommand()
                    ),
                    It.Is<CancellationToken>(token => token == cancellationToken)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var startedAt = DateTime.UtcNow;

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new AddOrUpdatePlexAccountServersCommand(targetAccount.Id, serverAccessTokens)
        );
        var completedAt = DateTime.UtcNow;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.PlexAccountId.ShouldBe(targetAccount.Id);
        result.Value.PlexAccountName.ShouldBe(targetAccount.DisplayName);
        var expectedRapport = new[]
        {
            new RefreshPlexServerAccessRapportRow(PlexAccessState.Updated, plexServers[0].Id, plexServers[0].Name),
            new RefreshPlexServerAccessRapportRow(PlexAccessState.Revoked, plexServers[1].Id, plexServers[1].Name),
            new RefreshPlexServerAccessRapportRow(PlexAccessState.Updated, plexServers[2].Id, plexServers[2].Name),
        };
        result.Value.Access.OrderBy(x => x.PlexServerId).ToList().ShouldBe(expectedRapport);

        var targetAfter = await IDbContext
            .PlexAccountServers.AsNoTracking()
            .Where(x => x.PlexAccountId == targetAccount.Id)
            .OrderBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.AuthTokenCreationDate,
                x.IsServerOwned,
            })
            .ToListAsync();
        var expectedTargetAfter = serverAccessTokens
            .Join(
                retainedServers,
                token => token.MachineIdentifier,
                server => server.MachineIdentifier,
                (token, server) =>
                    new
                    {
                        PlexAccountId = targetAccount.Id,
                        PlexServerId = server.Id,
                        AuthToken = token.AccessToken,
                        token.IsServerOwned,
                    }
            )
            .OrderBy(x => x.PlexServerId)
            .ToList();
        targetAfter
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToList()
            .ShouldBe(expectedTargetAfter);
        targetAfter.ShouldAllBe(x => x.AuthTokenCreationDate >= startedAt && x.AuthTokenCreationDate <= completedAt);
        targetAfter.ShouldNotContain(x => x.PlexServerId == plexServers[1].Id);
        var controlAfter = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == controlAccount.Id)
            .OrderBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        controlAfter.ShouldBe(controlBefore);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldIgnoreInvalidAndUnknownTokens_WhenMixedWithValidToken()
    {
        // Arrange
        await SetupDatabase(
            71004,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
            }
        );
        var plexAccount = await IDbContext.PlexAccounts.SingleAsync();
        var plexServers = await IDbContext.PlexServers.OrderBy(x => x.Id).ToListAsync();
        await IDbContext.PlexAccountServers.Where(x => x.PlexAccountId == plexAccount.Id).ExecuteDeleteAsync();
        var serverAccessTokens = new List<ServerAccessTokenDTO>
        {
            new()
            {
                PlexAccountId = plexAccount.Id,
                MachineIdentifier = plexServers[0].MachineIdentifier,
                AccessToken = "valid-token",
                IsServerOwned = true,
            },
            new()
            {
                PlexAccountId = plexAccount.Id,
                MachineIdentifier = plexServers[1].MachineIdentifier,
                AccessToken = " ",
                IsServerOwned = false,
            },
            new()
            {
                PlexAccountId = plexAccount.Id,
                MachineIdentifier = "unknown-machine-identifier",
                AccessToken = "unknown-server-token",
                IsServerOwned = true,
            },
        };
        var cancellationToken = CancellationToken;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueMediaOverviewRebuildCommand>(command =>
                        command == new QueueMediaOverviewRebuildCommand()
                    ),
                    It.Is<CancellationToken>(token => token == cancellationToken)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new AddOrUpdatePlexAccountServersCommand(plexAccount.Id, serverAccessTokens)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Access.ShouldBe([
            new RefreshPlexServerAccessRapportRow(PlexAccessState.Granted, plexServers[0].Id, plexServers[0].Name),
        ]);
        var associations = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == plexAccount.Id)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        associations.ShouldBe([
            new
            {
                PlexAccountId = plexAccount.Id,
                PlexServerId = plexServers[0].Id,
                AuthToken = "valid-token",
                IsServerOwned = true,
            },
        ]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldPersistAccessAndReturnFailure_WhenMediaOverviewRebuildFails()
    {
        // Arrange
        await SetupDatabase(
            71005,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
            }
        );
        var plexAccount = await IDbContext.PlexAccounts.SingleAsync();
        var plexServer = await IDbContext.PlexServers.SingleAsync();
        await IDbContext.PlexAccountServers.Where(x => x.PlexAccountId == plexAccount.Id).ExecuteDeleteAsync();
        var serverAccessToken = new ServerAccessTokenDTO
        {
            PlexAccountId = plexAccount.Id,
            MachineIdentifier = plexServer.MachineIdentifier,
            AccessToken = "persisted-before-rebuild-failure",
            IsServerOwned = false,
        };
        var cancellationToken = CancellationToken;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueMediaOverviewRebuildCommand>(command =>
                        command == new QueueMediaOverviewRebuildCommand()
                    ),
                    It.Is<CancellationToken>(token => token == cancellationToken)
                )
            )
            .ReturnsAsync(Result.Fail("Rebuild failed"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<RefreshPlexServerAccessRapport>(
            new AddOrUpdatePlexAccountServersCommand(plexAccount.Id, [serverAccessToken])
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var associations = await IDbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == plexAccount.Id)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.AuthToken,
                x.IsServerOwned,
            })
            .ToListAsync();
        associations.ShouldBe([
            new
            {
                PlexAccountId = plexAccount.Id,
                PlexServerId = plexServer.Id,
                AuthToken = serverAccessToken.AccessToken,
                serverAccessToken.IsServerOwned,
            },
        ]);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
