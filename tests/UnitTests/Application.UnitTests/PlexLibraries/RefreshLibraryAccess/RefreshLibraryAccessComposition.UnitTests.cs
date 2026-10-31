namespace Reaparr.Application.UnitTests;

public class RefreshLibraryAccessCompositionUnitTests : BaseUnitTest<RefreshLibraryAccessHandler>
{
    [Test]
    public async Task ShouldPersistSuccessfulRefreshAndRetainOfflineServerLibraries_WhenOneServerTimesOut()
    {
        // Arrange
        await SetupDatabase(
            5002,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
            }
        );
        using var dbContext = IDbContext;
        var accountId = await dbContext.PlexAccounts.Select(x => x.Id).SingleAsync();
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            setters =>
                setters
                    .SetProperty(x => x.SyncedContentChangedAt, x => x.ContentChangedAt)
                    .SetProperty(x => x.Outdated, false),
            CancellationToken
        );
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        libraries.Select(x => x.PlexServerId).ShouldBe(servers.Select(x => x.Id));
        var beforeAccess = await dbContext
            .PlexAccountLibraries.OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.PlexLibraryId,
                x.IsLibraryOwned,
            })
            .ToListAsync(CancellationToken);
        beforeAccess
            .Select(x => (x.PlexAccountId, x.PlexServerId, x.PlexLibraryId))
            .ShouldBe(libraries.Select(x => (accountId, x.PlexServerId, x.Id)));
        var incoming = new List<PlexLibrary> { libraries[0] }.ToApiLibraries(
            new DateTime(2026, 10, 19, 13, 0, 0, DateTimeKind.Utc)
        );
        incoming[0].Title = "Refreshed through aggregation";
        var reconciliation = Mock.Create<AddOrUpdatePlexLibrariesCommandHandler>();
        var reconcilePipeline = new ValidationPipeline<
            AddOrUpdatePlexLibrariesCommand,
            Result<List<PlexLibraryAccessRapport>>
        >([new AddOrUpdatePlexLibrariesValidator()]);
        var refreshPipeline = new ValidationPipeline<
            RefreshLibraryAccessCommand,
            Result<PlexLibraryAccessRefreshResponse>
        >([new RefreshLibraryAccessValidator()]);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibrarySectionsCommand>(c =>
                        c.PlexServerId == servers[0].Id && c.PlexAccountId == accountId
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(incoming))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibrarySectionsCommand>(c =>
                        c.PlexServerId == servers[1].Id && c.PlexAccountId == accountId
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                ResultExtensions.Create504GatewayTimeoutResult("Offline fixture").ToResult<List<PlexLibrary>>()
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<AddOrUpdatePlexLibrariesCommand>(c => c.PlexAccountId == accountId), CancellationToken)
            )
            .Returns(
                (ICommand<Result<List<PlexLibraryAccessRapport>>> command, CancellationToken token) =>
                {
                    // Compose the real validator and persistence handler; no fake database writes or rapports.
                    var request = (AddOrUpdatePlexLibrariesCommand)command;
                    return reconcilePipeline.ExecuteAsync(
                        request,
                        () => reconciliation.ExecuteAsync(request, token),
                        token
                    );
                }
            )
            .Verifiable(Times.Once());

        // Act
        var request = new RefreshLibraryAccessCommand(accountId);
        var result = await refreshPipeline.ExecuteAsync(
            request,
            () => Sut.ExecuteAsync(request, CancellationToken),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.OfflineServers.ShouldBe([servers[1].Id]);
        var report = result.Value.Reports.Single();
        report.PlexServerId.ShouldBe(servers[0].Id);
        report.PlexServerName.ShouldBe(servers[0].Name);
        report.Data.ShouldBe([
            new PlexLibraryAccessRow(
                PlexAccessState.Updated,
                servers[0].Id,
                libraries[0].Id,
                "Refreshed through aggregation"
            ),
        ]);
        var persisted = await dbContext.PlexLibraries.OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        persisted
            .Select(x => (x.Id, x.PlexServerId, x.Uuid, x.Title))
            .ShouldBe([
                (libraries[0].Id, servers[0].Id, libraries[0].Uuid, "Refreshed through aggregation"),
                (libraries[1].Id, servers[1].Id, libraries[1].Uuid, libraries[1].Title),
            ]);
        (
            await dbContext
                .PlexAccountLibraries.OrderBy(x => x.PlexLibraryId)
                .Select(x => new
                {
                    x.PlexAccountId,
                    x.PlexServerId,
                    x.PlexLibraryId,
                    x.IsLibraryOwned,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(beforeAccess);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<InvalidateLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
