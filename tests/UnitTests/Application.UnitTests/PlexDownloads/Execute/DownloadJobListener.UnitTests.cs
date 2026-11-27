using Quartz;

namespace Reaparr.Application.UnitTests;

public class DownloadJobListenerUnitTests : BaseUnitTest<DownloadJobListener>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldCheckDownloadQueue_WhenDownloadFinishesEvenIfMoveCheckThrows(bool moveCheckThrows)
    {
        // Arrange
        await SetupDatabase(39397, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        await IDbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.DownloadFinished);

        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(DownloadJob.GetJobKey(downloadTask.Id));
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new DownloadJobPayload(downloadTask.ToKey()).ToJobDataMap());

        var moveCheck = Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken));
        if (moveCheckThrows)
            moveCheck.ThrowsAsync(new InvalidOperationException("Move queue unavailable")).Verifiable(Times.Once());
        else
            moveCheck.ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(eventData =>
                        eventData.PlexServerIds.SequenceEqual(new[] { downloadTask.PlexServerId })
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await Sut.JobWasExecuted(context.Object, null, CancellationToken);

        // Assert
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }
}
