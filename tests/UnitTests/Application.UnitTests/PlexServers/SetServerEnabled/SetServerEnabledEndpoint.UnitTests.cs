namespace Reaparr.Application.UnitTests;

public class SetServerEnabledEndpointUnitTests
    : BaseEndpointUnitTest<SetServerEnabledEndpoint, SetServerEnabledRequest, ResultDTO<PlexServerDTO>>
{
    [Test]
    [Arguments(DownloadStatus.Queued)]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Paused)]
    [Arguments(DownloadStatus.Error)]
    [Arguments(DownloadStatus.DownloadFinished)]
    [Arguments(DownloadStatus.Moving)]
    public async Task ShouldRejectDisablingWithoutSideEffects_WhenDownloadsAreUnfinished(DownloadStatus status)
    {
        // Arrange
        await SetupDatabase(91220, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.DownloadStatus, status),
            CancellationToken
        );

        // Act
        var result = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = file.PlexServerId, IsEnabled = false }
        );

        // Assert
        result.StatusCode.ShouldBe(409);
        result.Response.ShouldNotBeNull();
        result.Response.IsSuccess.ShouldBeFalse();
        result.Response.Errors.Count.ShouldBe(1);
        (await dbContext.PlexServers.IgnoreIsEnabledFilter().SingleAsync(CancellationToken)).IsEnabled.ShouldBeTrue();
        (await dbContext.DownloadTaskMovieFile.Select(x => x.DownloadStatus).SingleAsync(CancellationToken)).ShouldBe(
            status
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CancelLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldAllowDisabling_WhenDownloadsAreCompleted()
    {
        // Arrange
        await SetupDatabase(91221, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
            CancellationToken
        );
        Mock.SetupCommand(() => new CancelLibrarySyncJobCommand(file.PlexLibraryId))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = file.PlexServerId, IsEnabled = false }
        );

        // Assert
        result.StatusCode.ShouldBe(200);
        result.Response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        result.Response.Errors.Count.ShouldBe(0);
        (await dbContext.PlexServers.IgnoreIsEnabledFilter().SingleAsync(CancellationToken)).IsEnabled.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldEnableServer_WhenServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(91201, config => config.PlexServerCount = 1);

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = true }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var updated = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBe(true);
    }

    [Test]
    public async Task ShouldDisableServer_WhenServerIsEnabled()
    {
        // Arrange
        await SetupDatabase(91203, config => config.PlexServerCount = 1);

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var updated = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldCancelEveryTargetServerLibraryBeforeDisabling_WhenServerHasLibraries()
    {
        // Arrange
        await SetupDatabase(91212, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 2;
            config.PlexTvShowLibraryCount = 1;
        });

        var dbContext = IDbContext;
        var servers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetServerId = servers[0].Id;
        var targetLibraryIds = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.PlexServerId == targetServerId)
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToListAsync(CancellationToken);
        var otherLibraryIds = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.PlexServerId != targetServerId)
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToListAsync(CancellationToken);
        targetLibraryIds.ShouldNotBeEmpty();
        otherLibraryIds.ShouldNotBeEmpty();

        var commandExecutor = Mock.Mock<ICommandExecutor>();
        foreach (var libraryId in targetLibraryIds)
        {
            commandExecutor
                .Setup(x =>
                    x.Send(
                        It.Is<CancelLibrarySyncJobCommand>(c => c.PlexLibraryId == libraryId),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = targetServerId, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();
        endpointResult.Response.Errors.Count.ShouldBe(0);
        var updatedServers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.IsEnabled })
            .ToListAsync(CancellationToken);
        updatedServers.ShouldBe([
            new { Id = targetServerId, IsEnabled = false },
            new { Id = servers[1].Id, IsEnabled = true },
        ]);

        foreach (var libraryId in targetLibraryIds)
            commandExecutor.Verify(
                x =>
                    x.Send(
                        It.Is<CancelLibrarySyncJobCommand>(c => c.PlexLibraryId == libraryId),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        foreach (var libraryId in otherLibraryIds)
            commandExecutor.Verify(
                x =>
                    x.Send(
                        It.Is<CancelLibrarySyncJobCommand>(c => c.PlexLibraryId == libraryId),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        commandExecutor.Verify();
    }

    [Test]
    public async Task ShouldNotChangeUnrelatedServer_WhenUpdatingTargetServer()
    {
        // Arrange
        await SetupDatabase(91204, config => config.PlexServerCount = 2);

        var dbContext = IDbContext;
        var servers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetId = servers[0].Id;
        var otherId = servers[1].Id;
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = targetId, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);

        var target = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == targetId, CancellationToken);
        var other = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == otherId, CancellationToken);

        target.IsEnabled.ShouldBeFalse();
        other.IsEnabled.ShouldBe(true);
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(91202, config => config.PlexServerCount = 0);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = 9999, IsEnabled = true }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldKeepOwnedOverride_WhenTogglingIsEnabled()
    {
        // Arrange
        await SetupDatabase(91205, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, true), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.OwnedOverride.ShouldBe(true);
        updated.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldKeepDownloadsPausedFlag_WhenTogglingIsEnabled()
    {
        // Arrange
        await SetupDatabase(91206, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDownloadsPausedByUser, true), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsDownloadsPausedByUser.ShouldBe(true);
    }

    [Test]
    public async Task ShouldBeIdempotent_WhenEnablingAlreadyEnabledServer()
    {
        // Arrange
        await SetupDatabase(91207, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = true }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBe(true);
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.PlexServerChanged(It.IsAny<TelemetryAction>(), It.IsAny<int>(), It.IsAny<PlexServer?>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldBeIdempotent_WhenDisablingAlreadyDisabledServer()
    {
        // Arrange
        await SetupDatabase(91208, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        await db
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldToggleEnabledStateTwice_WhenTwoRequestsAreSent()
    {
        // Arrange
        await SetupDatabase(91209, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        // Act
        await TestEndpointHandleAsync(new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false });
        await TestEndpointHandleAsync(new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = true });

        // Assert
        var updated = await db
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == server.Id, CancellationToken);
        updated.IsEnabled.ShouldBe(true);
    }

    [Test]
    public async Task ShouldReturnSuccessResponse_WhenDisablingTargetServer()
    {
        // Arrange
        await SetupDatabase(91210, config => config.PlexServerCount = 1);
        var db = IDbContext;
        var server = await db.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
    }

    [Test]
    public async Task ShouldInvalidateServerLibraries_WhenTogglingIsEnabled()
    {
        // Arrange
        await SetupDatabase(91211, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 2;
            config.PlexTvShowLibraryCount = 2;
        });

        var dbContext = IDbContext;
        var server = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var libraryIds = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.PlexServerId == server.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(4);
        foreach (var libraryId in libraryIds)
            Mock.SetupCommand(() => new CancelLibrarySyncJobCommand(libraryId))
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand()).ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetServerEnabledRequest { PlexServerId = server.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBe(true);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public void ShouldRejectInvalidPlexServerId_WhenValidatingRequest()
    {
        // Arrange
        var validator = new SetServerEnabledRequestValidator();

        // Act
        var result = validator.Validate(new SetServerEnabledRequest { PlexServerId = 0, IsEnabled = true });

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
