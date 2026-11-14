using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class MetadataSyncJobUnitTests : BaseUnitTest<MetadataSyncJob>
{
    private static IJobExecutionContext SetupJobContext(int serverId, CancellationToken cancellationToken = default)
    {
        var payload = new MetadataSyncJobPayload(serverId).ToJobDataMap();
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(payload);
        jobDetail.SetupGet(x => x.Key).Returns(MetadataSyncJob.GetJobKey(serverId));

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(payload);
        context.SetupGet(x => x.FireInstanceId).Returns("metadata-sync-test");
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupProperty(x => x.Result);
        return context.Object;
    }

    private void SetupBeginJob()
    {
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.MetadataSync,
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>()
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
    }

    [Test]
    [Arguments(false, JobStatus.Failed)]
    [Arguments(true, JobStatus.Cancelled)]
    public async Task ShouldStopBeforeEpisodes_WhenMovieMetadataFailsOrCancels(bool cancelled, JobStatus expectedStatus)
    {
        // Arrange
        await SetupDatabase(88101, config => config.PlexServerCount = 1);
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.SingleAsync(CancellationToken);
        var failure = new InvalidOperationException("movie metadata failed");
        var movieResult = cancelled
            ? ResultExtensions.TaskIsCancelled(nameof(ProcessMovieMetadataCommand)).ToResult<int>()
            : Result.Fail<int>(new ExceptionalError(failure));
        var context = SetupJobContext(server.Id, CancellationToken.None);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ProcessMovieMetadataCommand>(command =>
                        command.ServerId == server.Id && command.ServerName == server.Name
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(movieResult)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.MetadataSyncFinished(
                    server.Id,
                    0,
                    It.Is<ResultBase>(result => ReferenceEquals(result, movieResult))
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(movieResult, TelemetryActivity.MetadataSync))
            .Verifiable(Times.Once());
        SetupBeginJob();

        // Act
        await Sut.Execute(context);

        // Assert
        movieResult.IsFailed.ShouldBeTrue();
        movieResult.IsCancelled.ShouldBe(cancelled);
        movieResult.Errors.Count.ShouldBe(1);
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(expectedStatus);
        terminalResult.ErrorSummary.ShouldBe(movieResult.Errors.Single().Message);
        if (!cancelled)
            movieResult.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(failure);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ProcessEpisodeMetadataCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    [Arguments(false, JobStatus.Failed)]
    [Arguments(true, JobStatus.Cancelled)]
    public async Task ShouldRetainMovieCount_WhenEpisodeMetadataFailsOrCancels(bool cancelled, JobStatus expectedStatus)
    {
        // Arrange
        await SetupDatabase(88102, config => config.PlexServerCount = 1);
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.SingleAsync(CancellationToken);
        const int movieProcessedCount = 3;
        var failure = new InvalidOperationException("episode metadata failed");
        var episodeResult = cancelled
            ? ResultExtensions.TaskIsCancelled(nameof(ProcessEpisodeMetadataCommand)).ToResult<int>()
            : Result.Fail<int>(new ExceptionalError(failure));
        var context = SetupJobContext(server.Id, CancellationToken.None);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ProcessMovieMetadataCommand>(command =>
                        command.ServerId == server.Id && command.ServerName == server.Name
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok(movieProcessedCount))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ProcessEpisodeMetadataCommand>(command =>
                        command.ServerId == server.Id && command.ServerName == server.Name
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(episodeResult)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.MetadataSyncFinished(
                    server.Id,
                    movieProcessedCount,
                    It.Is<ResultBase>(result => ReferenceEquals(result, episodeResult))
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(episodeResult, TelemetryActivity.MetadataSync))
            .Verifiable(Times.Once());
        SetupBeginJob();

        // Act
        await Sut.Execute(context);

        // Assert
        episodeResult.IsFailed.ShouldBeTrue();
        episodeResult.IsCancelled.ShouldBe(cancelled);
        episodeResult.Errors.Count.ShouldBe(1);
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(expectedStatus);
        terminalResult.ErrorSummary.ShouldBe(episodeResult.Errors.Single().Message);
        if (!cancelled)
            episodeResult.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(failure);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldCompleteWithCumulativeProcessedCount_WhenBothMetadataProcessorsSucceed()
    {
        // Arrange
        await SetupDatabase(88103, config => config.PlexServerCount = 1);
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.SingleAsync(CancellationToken);
        const int movieProcessedCount = 2;
        const int episodeProcessedCount = 4;
        ResultBase? terminalTelemetryResult = null;
        var context = SetupJobContext(server.Id, CancellationToken.None);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ProcessMovieMetadataCommand>(command =>
                        command.ServerId == server.Id && command.ServerName == server.Name
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok(movieProcessedCount))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ProcessEpisodeMetadataCommand>(command =>
                        command.ServerId == server.Id && command.ServerName == server.Name
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok(episodeProcessedCount))
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.MetadataSyncFinished(
                    server.Id,
                    movieProcessedCount + episodeProcessedCount,
                    It.Is<ResultBase>(result => result.IsSuccess && result.Errors.Count == 0)
                )
            )
            .Callback<int, int, ResultBase>((_, _, result) => terminalTelemetryResult = result)
            .Verifiable(Times.Once());
        SetupBeginJob();

        // Act
        await Sut.Execute(context);

        // Assert
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(JobStatus.Completed);
        terminalResult.ErrorSummary.ShouldBeNull();
        terminalTelemetryResult.ShouldNotBeNull();
        terminalTelemetryResult!.IsSuccess.ShouldBeTrue();
        terminalTelemetryResult.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.MetadataSync), Times.Never());
        Mock.Mock<ITelemetry>().Verify();
    }
}
