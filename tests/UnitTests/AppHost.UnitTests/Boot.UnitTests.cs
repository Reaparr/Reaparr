using Microsoft.Extensions.Hosting;
using Reaparr.Application;
using Reaparr.Application.Contracts;

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
            .Setup(x =>
                x.Send(
                    It.IsAny<RecalculatePlexGenreTypesCommand>(),
                    CancellationToken.None
                )
            )
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
    public async Task ShouldStartBackgroundJobsAfterRecoveringInterruptedDownloads()
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
