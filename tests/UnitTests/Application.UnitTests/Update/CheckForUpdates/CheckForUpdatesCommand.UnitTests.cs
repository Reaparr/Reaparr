using NuGet.Versioning;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Reaparr.Application.UnitTests;

public class CheckForUpdatesCommandUnitTests : BaseUnitTest<CheckForUpdatesCommandHandler>
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldReturnUpdateAvailable_WhenVelopackFindsUpdateAndReleaseNotesFetchFails(bool desktop)
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.RuntimeMode = desktop ? "desktop" : "docker";
            x.Version = "0.36.0-dev.1";
        });

        var asset = new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(0, 38, 0, "dev.10") };
        var updateInfo = new UpdateInfo(asset, false);
        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>(MockBehavior.Loose);
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        var appBuildInfo = new Mock<IAppBuildInfo>(MockBehavior.Strict);
        appBuildInfo.SetupGet(x => x.IsDesktopMode).Returns(desktop);
        appBuildInfo.SetupGet(x => x.InformationalVersion).Returns("0.36.0-dev.1");
        appBuildInfo.SetupGet(x => x.IsDevRelease).Returns(true);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetGitHubReleasesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<IReadOnlyList<ReleaseNote>>("GitHub unavailable"))
            .Verifiable(Times.Once());
        mockManager.Setup(m => m.IsInstalled).Returns(true).Verifiable(desktop ? Times.Once() : Times.Never());
        mockManager.Setup(m => m.AppId).Returns("Reaparr").Verifiable(desktop ? Times.Once() : Times.Never());
        mockManager
            .Setup(m => m.CurrentVersion)
            .Returns(new SemanticVersion(0, 36, 0, "dev.1"))
            .Verifiable(desktop ? Times.Once() : Times.Never());
        mockManager.Setup(m => m.CheckForUpdatesAsync()).ReturnsAsync(updateInfo).Verifiable(Times.Once());
        // Act
        var sut = new CheckForUpdatesCommandHandler(
            Log,
            Mock.Mock<ICommandExecutor>().Object,
            appBuildInfo.Object,
            mockManager.Object
        );
        var result = await sut.ExecuteAsync(new CheckForUpdatesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.IsUpdateAvailable.ShouldBeTrue();
        result.Value.NewestVersion.ShouldBe("0.38.0-dev.10");
        result.Value.CurrentVersion.ShouldBe("0.36.0-dev.1");
        result.Value.ReleaseNotes.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        mockManager.Verify();
        mockSource.VerifyNoOtherCalls();
    }

    [Test]
    [Arguments(FallbackOutcome.Failed)]
    [Arguments(FallbackOutcome.Cancelled)]
    [Arguments(FallbackOutcome.Empty)]
    public async Task ShouldPreserveGitHubOutcome_WhenVelopackHasNoUpdate(FallbackOutcome outcome)
    {
        // Arrange
        var releasesResult = outcome switch
        {
            FallbackOutcome.Failed => Result.Fail<IReadOnlyList<ReleaseNote>>("GitHub unavailable"),
            FallbackOutcome.Cancelled => ResultExtensions
                .TaskIsCancelled(nameof(GetGitHubReleasesCommand))
                .ToResult<IReadOnlyList<ReleaseNote>>(),
            _ => Result.Ok<IReadOnlyList<ReleaseNote>>([]),
        };
        var appBuildInfo = new MockAppBuildInfo { Version = "1.2.3", InformationalVersion = "1.2.3" };
        var manager = new Mock<UpdateManager>(
            new Mock<IUpdateSource>().Object,
            null!,
            new Mock<IVelopackLocator>(MockBehavior.Loose).Object
        );
        var sut = new CheckForUpdatesCommandHandler(
            Log,
            Mock.Mock<ICommandExecutor>().Object,
            appBuildInfo,
            manager.Object
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetGitHubReleasesCommand>(), CancellationToken))
            .ReturnsAsync(releasesResult)
            .Verifiable(Times.Once());
        manager.Setup(x => x.CheckForUpdatesAsync()).ReturnsAsync((UpdateInfo?)null).Verifiable(Times.Once());

        // Act
        var result = await sut.ExecuteAsync(new CheckForUpdatesCommand(), CancellationToken);

        // Assert
        if (outcome == FallbackOutcome.Empty)
        {
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
            result.Value.IsUpdateAvailable.ShouldBeFalse();
            result.Value.CurrentVersion.ShouldBe("1.2.3");
            result.Value.NewestVersion.ShouldBe("1.2.3");
            result.Value.ReleaseNotes.ShouldBeEmpty();
        }
        else
        {
            result.IsFailed.ShouldBeTrue();
            result.Errors.Count.ShouldBe(1);
            result.IsCancelled.ShouldBe(outcome == FallbackOutcome.Cancelled);
            result.Reasons.ShouldBe(releasesResult.Reasons);
            result.ValueOrDefault.ShouldBeNull();
        }
        Mock.Mock<ICommandExecutor>().Verify();
        manager.Verify();
    }

    public enum FallbackOutcome
    {
        Failed,
        Cancelled,
        Empty,
    }
}
