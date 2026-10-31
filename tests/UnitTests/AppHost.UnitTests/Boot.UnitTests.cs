using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Reaparr.Application;
using Reaparr.Application.Contracts;
using Reaparr.FileSystem.Contracts;

namespace Reaparr.AppHost.UnitTests;

public class BootUnitTests : BaseUnitTest<Boot>
{
    [Test]
    public async Task ShouldRunGenreTypeRecalculationAfterLegacyIntegrationMigration()
    {
        // Arrange
        using var applicationStarted = new CancellationTokenSource();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequence = new MockSequence();

        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStarted).Returns(applicationStarted.Token);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopped).Returns(CancellationToken.None);
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<MigrateLegacyArrSettingsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Fail("Import failed"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<RecalculatePlexGenreTypesCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok(new PlexGenreTypeRecalculationResult(0, 0)))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<NotifyArrAppsOnStartupCommand>(), CancellationToken.None))
            .ReturnsAsync(() =>
            {
                completed.SetResult();
                return Result.Ok();
            })
            .Verifiable(Times.Once());

        // Act
        _ = Sut;
        applicationStarted.Cancel();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldCleanUpInterruptedLibrarySyncsBeforeStartingBackgroundJobs()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsIntegrationTestMode = true);
        var sequence = new MockSequence();

        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStarted).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopped).Returns(CancellationToken.None);
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<CreateDefaultAppUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadQueue>()
            .InSequence(sequence)
            .Setup(x => x.Setup(CancellationToken.None))
            .Returns(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<RecoverInterruptedDownloadsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<CleanupLibrarySyncJobQueueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IBackgroundJobsSetup>()
            .InSequence(sequence)
            .Setup(x => x.SetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.StartAsync(CancellationToken);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadQueue>().Verify();
        Mock.Mock<IBackgroundJobsSetup>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldStopApplicationBeforeStartingBackgroundJobs_WhenLibrarySyncCleanupFails()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsIntegrationTestMode = true);
        var sequence = new MockSequence();

        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStarted).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopped).Returns(CancellationToken.None);
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<CreateDefaultAppUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadQueue>()
            .InSequence(sequence)
            .Setup(x => x.Setup(CancellationToken.None))
            .Returns(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<RecoverInterruptedDownloadsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<CleanupLibrarySyncJobQueueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("cleanup failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IHostApplicationLifetime>()
            .InSequence(sequence)
            .Setup(x => x.StopApplication())
            .Verifiable(Times.Once());

        // Act
        await Sut.StartAsync(CancellationToken);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadQueue>().Verify();
        Mock.Mock<IHostApplicationLifetime>().Verify();
        Mock.Mock<IBackgroundJobsSetup>().Verify(x => x.SetupAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldKickQueuedLibrarySyncsAfterStartingBackgroundJobs()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsIntegrationTestMode = false);
        var sequence = new MockSequence();

        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStarted).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopped).Returns(CancellationToken.None);
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<CreateDefaultAppUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadQueue>()
            .InSequence(sequence)
            .Setup(x => x.Setup(CancellationToken.None))
            .Returns(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<RecoverInterruptedDownloadsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<CleanupLibrarySyncJobQueueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IBackgroundJobsSetup>()
            .InSequence(sequence)
            .Setup(x => x.SetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .InSequence(sequence)
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadQueue>()
            .InSequence(sequence)
            .Setup(x => x.CheckDownloadQueueForAllServers(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IMoveDownloadFileQueue>()
            .InSequence(sequence)
            .Setup(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.StartAsync(CancellationToken);

        // Assert
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        Mock.Mock<IDownloadQueue>()
            .Verify(x => x.CheckDownloadQueueForAllServers(It.IsAny<CancellationToken>()), Times.Once);
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
    }

    [Test]
    [Arguments(DownloadStatus.DownloadFinished)]
    [Arguments(DownloadStatus.MoveError)]
    public async Task ShouldReconcileReadyToMoveFilesAfterBackgroundJobsAreReady_OnNormalStartup(
        DownloadStatus readyStatus
    )
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsIntegrationTestMode = false);
        await SetupDatabase(
            58123,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.MovieDownloadTasksCount = 3;
            }
        );

        var dbContext = IDbContext;
        var files = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .Take(3)
            .ToListAsync(CancellationToken);
        files.SetDownloadStatus(DownloadStatus.DownloadFinished);
        files[0].DownloadStatus = readyStatus;
        files[1].DownloadStatus = DownloadStatus.Completed;
        files[2].DownloadStatus = DownloadStatus.Paused;
        files.Count.ShouldBe(3);
        files.Select(x => x.DownloadStatus).ShouldBe([readyStatus, DownloadStatus.Completed, DownloadStatus.Paused]);
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedKey = files[0].ToKey();
        var startupToken = CancellationToken;
        var sequence = new MockSequence();

        SetupDependencies(builder =>
            builder.RegisterType<MoveDownloadFileJobQueue>().As<IMoveDownloadFileQueue>().SingleInstance()
        );

        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStarted).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopped).Returns(CancellationToken.None);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDefaultAppUserCommand>(), startupToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<RecoverInterruptedDownloadsCommand>(), startupToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CleanupLibrarySyncJobQueueCommand>(), startupToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), startupToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadQueue>()
            .Setup(x => x.Setup(CancellationToken.None))
            .Returns(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadQueue>()
            .Setup(x => x.CheckDownloadQueueForAllServers(startupToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IBackgroundJobsSetup>()
            .InSequence(sequence)
            .Setup(x => x.SetupAsync(startupToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .InSequence(sequence)
            .Setup(x => x.StartMoveDownloadFileJob(It.Is<DownloadTaskKey>(key => key == expectedKey), startupToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.StartAsync(startupToken);

        // Assert
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x =>
                    x.StartMoveDownloadFileJob(
                        It.Is<DownloadTaskKey>(key => key != expectedKey),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadQueue>().Verify();
        Mock.Mock<IBackgroundJobsSetup>().Verify();
    }

    [Test]
    public async Task ShouldStopApplication_WhenBackgroundJobsSetupFails()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsIntegrationTestMode = true);

        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStarted).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopped).Returns(CancellationToken.None);
        Mock.Mock<IHostApplicationLifetime>().Setup(x => x.StopApplication()).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDefaultAppUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<RecoverInterruptedDownloadsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CleanupLibrarySyncJobQueueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadQueue>()
            .Setup(x => x.Setup(CancellationToken.None))
            .Returns(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IBackgroundJobsSetup>()
            .Setup(x => x.SetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Background jobs setup failed"))
            .Verifiable(Times.Once());

        // Act
        await Sut.StartAsync(CancellationToken);

        // Assert
        Mock.Mock<IHostApplicationLifetime>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadQueue>().Verify();
        Mock.Mock<IBackgroundJobsSetup>().Verify();
    }
}
