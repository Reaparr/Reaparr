using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class CheckPlexLibrariesForUpdatesJobUnitTests : BaseUnitTest<CheckPlexLibrariesForUpdatesJob>
{
    private static IJobExecutionContext SetupJobContext(CancellationToken cancellationToken = default)
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail)
            .Returns(
                JobBuilder
                    .Create<CheckPlexLibrariesForUpdatesJob>()
                    .WithIdentity(nameof(CheckPlexLibrariesForUpdatesJob))
                    .Build()
            );
        context.SetupGet(x => x.FireInstanceId).Returns("test-fire-instance");
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        context.SetupProperty(x => x.Result);
        return context.Object;
    }
    private void SetupBeginJob()
    {
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.LibraryUpdateCheck,
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>()
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
    }

    [Test]
    public async Task ShouldRefreshLibraryAccessForEnabledServers_WhenAutoSyncIsEnabled()
    {
        // Arrange
        await SetupDatabase(
            4501,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexAccountCount = 1;
            }
        );
        var dbContext = IDbContext;

        await dbContext
            .PlexServers.IgnoreQueryFilters()
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);

        var serverIds = await dbContext.PlexServers.Select(x => x.Id).ToListAsync(CancellationToken);
        var accountId = await dbContext
            .PlexAccounts.IgnoreQueryFilters()
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        await dbContext.PlexAccountServers.IgnoreQueryFilters().ExecuteDeleteAsync(CancellationToken);
        dbContext.PlexAccountServers.AddRange(
            serverIds.Select(serverId => new PlexAccountServer
            {
                PlexAccountId = accountId,
                PlexServerId = serverId,
                AuthToken = $"token-{serverId}",
                AuthTokenCreationDate = DateTime.UtcNow,
                IsServerOwned = true,
            })
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryAccessCommand>(cmd =>
                        cmd.PlexAccountId == accountId && serverIds.Contains(cmd.PlexServerId)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] }))
            .Verifiable(Times.Exactly(2));
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());
        var context = SetupJobContext();

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldSkipServerWithoutTokenMapping_WhenRefreshingLibraryAccess()
    {
        // Arrange
        await SetupDatabase(
            4503,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexAccountCount = 1;
            }
        );
        var dbContext = IDbContext;

        await dbContext
            .PlexServers.IgnoreQueryFilters()
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);

        var serverIds = await dbContext.PlexServers.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        var accountId = await dbContext
            .PlexAccounts.IgnoreQueryFilters()
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        await dbContext.PlexAccountServers.IgnoreQueryFilters().ExecuteDeleteAsync(CancellationToken);
        dbContext.PlexAccountServers.Add(
            new PlexAccountServer
            {
                PlexAccountId = accountId,
                PlexServerId = serverIds[0],
                AuthToken = "token-mapped",
                AuthTokenCreationDate = DateTime.UtcNow,
                IsServerOwned = true,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryAccessCommand>(cmd =>
                        cmd.PlexAccountId == accountId && cmd.PlexServerId == serverIds[0]
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] }))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());
        var context = SetupJobContext();

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldQueueOnlyOutdatedLibraries_WhenMarkedByRefresh()
    {
        // Arrange
        var now = DateTime.UtcNow;
        await SetupDatabase(
            4502,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 4;
                config.PlexTvShowLibraryCount = 0;
            }
        );
        var dbContext = IDbContext;

        await dbContext
            .PlexServers.IgnoreQueryFilters()
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);

        var serverId = await dbContext.PlexServers.Select(x => x.Id).FirstAsync(CancellationToken);
        var libraries = await dbContext.PlexLibraries.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries[0].PlexServerId = serverId;
        libraries[0].UpdatedAt = now;
        libraries[0].ContentChangedAt = 10;
        libraries[0].SyncedAt = now.AddHours(-2);
        libraries[0].Outdated = true;
        libraries[1].PlexServerId = serverId;
        libraries[1].UpdatedAt = now;
        libraries[1].ContentChangedAt = 20;
        libraries[1].SyncedAt = now.AddHours(-2);
        libraries[1].Outdated = true;
        libraries[2].PlexServerId = serverId;
        libraries[2].UpdatedAt = now;
        libraries[2].ContentChangedAt = 20;
        libraries[2].SyncedAt = now.AddHours(1);
        libraries[2].Outdated = false;
        libraries[3].PlexServerId = serverId;
        libraries[3].UpdatedAt = now;
        libraries[3].ContentChangedAt = 0;
        libraries[3].SyncedAt = now.AddHours(1);
        libraries[3].Outdated = false;
        var expectedLibraryIds = libraries.Take(2).Select(x => x.Id).ToList();

        var accountId = await dbContext
            .PlexAccounts.IgnoreQueryFilters()
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        await dbContext.PlexAccountServers.IgnoreQueryFilters().ExecuteDeleteAsync(CancellationToken);
        dbContext.PlexAccountServers.Add(
            new PlexAccountServer
            {
                PlexAccountId = accountId,
                PlexServerId = serverId,
                AuthToken = "token-outdated",
                AuthTokenCreationDate = DateTime.UtcNow,
                IsServerOwned = true,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryAccessCommand>(cmd =>
                        cmd.PlexAccountId == accountId && cmd.PlexServerId == serverId
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] }))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueLibrarySyncJobCommand>(cmd =>
                        cmd.PlexLibraryIds.OrderBy(id => id).SequenceEqual(expectedLibraryIds.OrderBy(id => id))
                        && cmd.Trigger == TelemetryTrigger.Scheduled
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var context = SetupJobContext();

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }
    [Test]
    public async Task ShouldContainUnexpectedRefreshExceptionAndMarkJobFailed()
    {
        // Arrange
        await SetupDatabase(
            4504,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
            }
        );
        var dbContext = IDbContext;
        await dbContext
            .PlexServers.IgnoreQueryFilters()
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);
        var serverId = await dbContext.PlexServers.Select(x => x.Id).FirstAsync(CancellationToken);
        var accountId = await dbContext
            .PlexAccounts.IgnoreQueryFilters()
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        await dbContext.PlexAccountServers.IgnoreQueryFilters().ExecuteDeleteAsync(CancellationToken);
        dbContext.PlexAccountServers.Add(
            new PlexAccountServer
            {
                PlexAccountId = accountId,
                PlexServerId = serverId,
                AuthToken = "token-refresh-error",
                AuthTokenCreationDate = DateTime.UtcNow,
                IsServerOwned = true,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var exception = new InvalidOperationException("refresh access failed");
        ResultBase? capturedResult = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryAccessCommand>(
                        command => command.PlexAccountId == accountId && command.PlexServerId == serverId
                    ),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(exception)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.LibraryUpdateCheck))
            .Callback<ResultBase, TelemetryActivity>((captured, _) => capturedResult = captured)
            .Verifiable(Times.Once());
        var context = SetupJobContext();

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(JobStatus.Failed);
        terminalResult.ErrorSummary.ShouldBe(exception.Message);
        capturedResult.ShouldNotBeNull();
        capturedResult!.HasException<InvalidOperationException>(out var capturedErrors).ShouldBeTrue();
        capturedErrors.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<TelemetryActivity>()), Times.Never());
    }

    [Test]
    public async Task ShouldCancel_WhenCallerCancellationIsObservedDuringDatabaseRead()
    {
        // Arrange
        await SetupDatabase(
            4505,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
            }
        );
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var context = SetupJobContext(cancellationSource.Token);

        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.LibraryUpdateCheck))
            .Verifiable(Times.Once());
        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.LibraryUpdateCheck), Times.Once());
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<TelemetryActivity>()), Times.Never());
    }
}
