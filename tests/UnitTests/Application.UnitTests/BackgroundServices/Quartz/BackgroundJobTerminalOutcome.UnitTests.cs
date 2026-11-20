using Quartz;

namespace Reaparr.Application.UnitTests;

public class BackgroundJobTerminalOutcomeUnitTests
{
    [Test]
    public void ShouldUseExplicitJobResult_WhenJobSetsTerminalOutcome()
    {
        // Arrange
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.Result).Returns(new BackgroundJobResult(JobStatus.Cancelled, "cancelled"));

        // Act
        var result = BackgroundJobTerminalOutcome.From(context.Object, null);

        // Assert
        result.Status.ShouldBe(JobStatus.Cancelled);
        result.ErrorSummary.ShouldBe("cancelled");
    }

    [Test]
    public void ShouldMapContainedFailureToQuartzReport_WithoutExposingErrorSummary()
    {
        // Arrange
        var context = new Mock<IJobExecutionContext>();
        var report = new BackgroundJobResult(JobStatus.Failed, "sensitive error detail");
        context.SetupGet(x => x.Result).Returns(report);

        // Act
        var outcome = BackgroundJobTerminalOutcome.From(context.Object, null);
        var quartzReport = (IJobRunReport)report;

        // Assert
        outcome.Status.ShouldBe(JobStatus.Failed);
        outcome.ErrorSummary.ShouldBe("sensitive error detail");
        quartzReport.Result.ShouldBe(JobRunResult.Failed);
        quartzReport.Summary.ShouldBe("Job failed");
        quartzReport.Metrics.ShouldBeNull();
    }

    [Test]
    public void ShouldMapContainedCancellationToQuartzReport_WithoutExposingErrorSummary()
    {
        // Arrange
        var report = new BackgroundJobResult(JobStatus.Cancelled, "sensitive cancellation detail");
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.Result).Returns(report);

        // Act
        var outcome = BackgroundJobTerminalOutcome.From(context.Object, null);
        var quartzReport = (IJobRunReport)report;

        // Assert
        outcome.Status.ShouldBe(JobStatus.Cancelled);
        outcome.ErrorSummary.ShouldBe("sensitive cancellation detail");
        quartzReport.Result.ShouldBe(JobRunResult.Cancelled);
        quartzReport.Summary.ShouldBe("Job was cancelled");
        quartzReport.Metrics.ShouldBeNull();
    }

    [Test]
    public void ShouldMapCompletedResultToQuartzSuccess_WithoutErrorSummary()
    {
        // Arrange
        var report = new BackgroundJobResult(JobStatus.Completed, "ignored detail");

        // Act
        var quartzReport = (IJobRunReport)report;

        // Assert
        quartzReport.Result.ShouldBe(JobRunResult.Succeeded);
        quartzReport.Summary.ShouldBeNull();
        quartzReport.Metrics.ShouldBeNull();
        report.ErrorSummary.ShouldBe("ignored detail");
    }

    [Test]
    public void ShouldMapUnhandledExceptionToFailed_WhenNoExplicitResultExists()
    {
        // Arrange
        var exception = new JobExecutionException(new InvalidOperationException("failed"));
        var context = new Mock<IJobExecutionContext>();

        // Act
        var result = BackgroundJobTerminalOutcome.From(context.Object, exception);

        // Assert
        result.Status.ShouldBe(JobStatus.Failed);
        result.ErrorSummary.ShouldBe("failed");
    }

    [Test]
    public void ShouldMapCancellationExceptionToCancelled_WhenNoExplicitResultExists()
    {
        // Arrange
        var exception = new JobExecutionException(new OperationCanceledException("cancelled"));
        var context = new Mock<IJobExecutionContext>();

        // Act
        var result = BackgroundJobTerminalOutcome.From(context.Object, exception);

        // Assert
        result.Status.ShouldBe(JobStatus.Cancelled);
        result.ErrorSummary.ShouldBe(exception.Message);
    }
}
