using Quartz;

namespace Reaparr.Application.UnitTests;

public class MoveDownloadFileJobUnitTests : BaseUnitTest<MoveDownloadFileJob>
{
    private static IJobExecutionContext SetupJobContext(
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    )
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(new MoveDownloadFileJobPayload(key).ToJobDataMap());

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(jobDetail.Object.JobDataMap);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        return context.Object;
    }

    [Test]
    public async Task ShouldDispatchCompletedStatus_WhenMoveSucceeds()
    {
        // Arrange
        await SetupDatabase(
            11001,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        downloadTask.DownloadStatus = DownloadStatus.DownloadFinished;
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<MoveDownloadFileFromFileTaskCommand>).ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<CleanUpDownloadTaskFoldersCommand>).ReturnsAsync(Result.Ok()).Verifiable(Times.Once);

        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k == downloadTask.ToKey()),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Completed),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Simulate the command handler setting MoveFinished status in the DB
        await dbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.MoveFinished);

        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        await Sut.Execute(context);

        // Assert
        var after = await dbContext.GetDownloadTaskFileAsync(downloadTask.ToKey(), CancellationToken);
        after.ShouldNotBeNull();
        after.DownloadStatus.ShouldBe(DownloadStatus.MoveFinished);

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<MoveDownloadFileFromFileTaskCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k == downloadTask.ToKey()),
                        It.Is<DownloadStatus>(s => s == DownloadStatus.Completed),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldUseNonCancellableFinalization_WhenMoveSucceeds()
    {
        // Arrange
        await SetupDatabase(
            11006,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        downloadTask.DownloadStatus = DownloadStatus.DownloadFinished;
        await IDbContext.SaveChangesAsync(CancellationToken);
        await IDbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.MoveFinished);

        Mock.SetupCommand(It.IsAny<MoveDownloadFileFromFileTaskCommand>).ReturnsAsync(Result.Ok());
        Mock.SetupCommand(It.IsAny<CleanUpDownloadTaskFoldersCommand>).ReturnsAsync(Result.Ok()).Verifiable(Times.Once);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k == downloadTask.ToKey()),
                    DownloadStatus.Completed,
                    CancellationToken.None
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        await Sut.Execute(context);

        // Assert
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task ShouldNotSetStatusToCompleted_WhenMoveCommandFails()
    {
        // Arrange
        await SetupDatabase(
            11002,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        downloadTask.DownloadStatus = DownloadStatus.DownloadFinished;
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<MoveDownloadFileFromFileTaskCommand>).ReturnsAsync(Result.Fail("Move failed"));

        // These should NOT be called when the move command fails
        Mock.SetupCommand(It.IsAny<CleanUpDownloadTaskFoldersCommand>)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        await Sut.Execute(context);

        // Assert: status stays at DownloadFinished (the command handler sets MoveError,
        // but since the command is mocked to fail without touching the DB, status is unchanged)
        var after = await dbContext.GetDownloadTaskFileAsync(downloadTask.ToKey(), CancellationToken);
        after.ShouldNotBeNull();
        after.DownloadStatus.ShouldNotBe(DownloadStatus.Completed);

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<MoveDownloadFileFromFileTaskCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldNotSetStatusToCompleted_WhenMoveCommandFailsAndTaskIsAlreadyMoveFinished()
    {
        // Arrange
        await SetupDatabase(
            11004,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        downloadTask.DownloadStatus = DownloadStatus.MoveFinished;
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<MoveDownloadFileFromFileTaskCommand>).ReturnsAsync(Result.Fail("Move failed"));

        Mock.SetupCommand(It.IsAny<CleanUpDownloadTaskFoldersCommand>)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        await Sut.Execute(context);

        // Assert
        var after = await dbContext.GetDownloadTaskFileAsync(downloadTask.ToKey(), CancellationToken);
        after.ShouldNotBeNull();
        after.DownloadStatus.ShouldBe(DownloadStatus.MoveFinished);

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<MoveDownloadFileFromFileTaskCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldNotThrowAndShouldQueueNext_WhenCompletedStatusUpdateThrowsException()
    {
        // Arrange
        await SetupDatabase(
            11005,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        downloadTask.DownloadStatus = DownloadStatus.MoveFinished;
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<MoveDownloadFileFromFileTaskCommand>).ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<CleanUpDownloadTaskFoldersCommand>)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k == downloadTask.ToKey()),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Completed),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("dispatcher exploded"))
            .Verifiable(Times.Once());

        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        var act = async () => await Sut.Execute(context);

        // Assert
        await act.ShouldNotThrowAsync();

        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldReturnHundredPercent_WhenCompletedAndDataReceivedIsZero()
    {
        // Regression: after MoveFinished -> Completed the percentage must stay at 100 and never
        // fall back to DataReceived / DataTotal, which can be 0 for externally-managed downloads.
        // Arrange
        await SetupDatabase(
            11003,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);

        // Simulate a task whose download bytes were not tracked (e.g. external client) but whose
        // file transfer has fully completed.
        downloadTask.DataReceived = 0;
        downloadTask.DataTotal = 100_000_000;
        downloadTask.FileDataTransferred = downloadTask.DataTotal;
        downloadTask.CurrentFileTransferBytesOffset = downloadTask.DataTotal;
        downloadTask.Percentage = 100;
        downloadTask.DownloadStatus = DownloadStatus.MoveFinished;
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.SetupCommand(It.IsAny<MoveDownloadFileFromFileTaskCommand>).ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<CleanUpDownloadTaskFoldersCommand>).ReturnsAsync(Result.Ok()).Verifiable(Times.Once);

        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k == downloadTask.ToKey()),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Completed),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        await Sut.Execute(context);

        // Assert
        var after = await dbContext
            .DownloadTaskMovieFile.AsNoTracking()
            .FirstAsync(x => x.Id == downloadTask.Id, CancellationToken);
        after.ShouldNotBeNull();
        after.DownloadStatus.ShouldBe(DownloadStatus.MoveFinished);
        after.DataReceived.ShouldBe(0L); // confirm download bytes remain at 0
        after.FileDataTransferred.ShouldBe(after.DataTotal); // confirm transfer bytes still full
        after.Percentage.ShouldBe(100m); // must stay 100 while the completion update is dispatched

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<MoveDownloadFileFromFileTaskCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k == downloadTask.ToKey()),
                        It.Is<DownloadStatus>(s => s == DownloadStatus.Completed),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldNotInvokeMoveCommand_WhenQueuedMoveReachesStoppedTask()
    {
        // Arrange
        await SetupDatabase(11007, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        await dbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.Stopped);
        var context = SetupJobContext(downloadTask.ToKey());
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<MoveDownloadFileFromFileTaskCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()), Times.Once());
    }
}
