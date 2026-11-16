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
    public async Task ShouldFilterDesktopReleaseNotesToVelopackTargetVersion()
    {
        // Arrange
        var updateInfo = new UpdateInfo(
            new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(0, 39, 0) },
            false
        );
        var releaseNotes = new List<ReleaseNote>
        {
            new()
            {
                Version = "v0.39.0",
                Notes = "Target release notes",
                ReleaseDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                IsDevRelease = false,
            },
            new()
            {
                Version = "v0.40.0",
                Notes = "Future release notes",
                ReleaseDate = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc),
                IsDevRelease = false,
            },
        };
        var appBuildInfo = new Mock<IAppBuildInfo>(MockBehavior.Strict);
        appBuildInfo.SetupGet(x => x.IsDesktopMode).Returns(true);
        appBuildInfo.SetupGet(x => x.InformationalVersion).Returns("0.38.0");
        var manager = new Mock<UpdateManager>(
            new Mock<IUpdateSource>().Object,
            null!,
            new Mock<IVelopackLocator>(MockBehavior.Loose).Object
        );
        manager.Setup(x => x.IsInstalled).Returns(true);
        manager.Setup(x => x.AppId).Returns("Reaparr");
        manager.Setup(x => x.CurrentVersion).Returns(new SemanticVersion(0, 38, 0));
        manager.Setup(x => x.CheckForUpdatesAsync()).ReturnsAsync(updateInfo);
        var executor = Mock.Mock<ICommandExecutor>();
        executor
            .Setup(x => x.Send(It.IsAny<GetGitHubReleasesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<IReadOnlyList<ReleaseNote>>(releaseNotes));

        // Act
        var sut = new CheckForUpdatesCommandHandler(Log, executor.Object, appBuildInfo.Object, manager.Object);
        var result = await sut.ExecuteAsync(new CheckForUpdatesCommand(), CancellationToken);

        // Assert
        result.Value.ReleaseNotes.Select(x => x.Version).ShouldBe(["v0.39.0"]);
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

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldSkipReleaseNotes_WhenDesktopHasNoUpdateOrUpdateCheckFails(bool installed, bool failCheck)
    {
        // Arrange
        var appBuildInfo = new MockAppBuildInfo
        {
            RuntimeMode = "desktop",
            Version = "0.38.0",
            InformationalVersion = "0.38.0",
        };
        var manager = new Mock<UpdateManager>(
            new Mock<IUpdateSource>().Object,
            null!,
            new Mock<IVelopackLocator>(MockBehavior.Loose).Object
        );
        manager.Setup(x => x.IsInstalled).Returns(installed).Verifiable(Times.Once());
        manager.Setup(x => x.AppId).Returns("Reaparr").Verifiable(Times.Once());
        manager.Setup(x => x.CurrentVersion).Returns(new SemanticVersion(0, 38, 0)).Verifiable(Times.Once());
        var failure = new InvalidOperationException("Update feed unavailable");
        if (failCheck)
            manager.Setup(x => x.CheckForUpdatesAsync()).ThrowsAsync(failure).Verifiable(Times.Once());
        else
            manager
                .Setup(x => x.CheckForUpdatesAsync())
                .ReturnsAsync((UpdateInfo?)null)
                .Verifiable(installed ? Times.Once() : Times.Never());
        var executor = Mock.Mock<ICommandExecutor>();
        var sut = new CheckForUpdatesCommandHandler(Log, executor.Object, appBuildInfo, manager.Object);

        // Act
        var result = await sut.ExecuteAsync(new CheckForUpdatesCommand(), CancellationToken);

        // Assert
        if (failCheck)
        {
            result.IsFailed.ShouldBeTrue();
            result.IsCancelled.ShouldBeFalse();
            result.Errors.Count.ShouldBe(1);
            result.Errors[0].ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(failure);
            result.ValueOrDefault.ShouldBeNull();
        }
        else
        {
            result.IsSuccess.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
            result.Value.IsUpdateAvailable.ShouldBeFalse();
            result.Value.CurrentVersion.ShouldBe("0.38.0");
            result.Value.NewestVersion.ShouldBe("0.38.0");
            result.Value.ReleaseNotes.ShouldBeEmpty();
        }
        executor.Verify(
            x => x.Send(It.IsAny<GetGitHubReleasesCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        manager.Verify();
    }

    public enum FallbackOutcome
    {
        Failed,
        Cancelled,
        Empty,
    }
}
