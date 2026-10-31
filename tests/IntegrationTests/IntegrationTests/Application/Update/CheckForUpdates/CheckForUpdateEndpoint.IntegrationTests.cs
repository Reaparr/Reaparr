using System.Text;
using System.Text.Json;
using Moq;
using Moq.Contrib.HttpClient;
using NuGet.Versioning;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Reaparr.IntegrationTests;

public class CheckForUpdateEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldReturnNoUpdate_WhenDockerModeAndCommandExecutorReturnsNoUpdate()
    {
        // Arrange
        var seed = new Seed(11001);

        var updateResult = new AppUpdateCheckResult
        {
            IsUpdateAvailable = false,
            NewestVersion = "0.38.0",
            CurrentVersion = "0.38.0",
            ReleaseNotes = [],
        };

        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.OverrideAppBuildInfo = new MockAppBuildInfo
                {
                    RuntimeMode = "docker",
                    Version = "0.38.0",
                    RuntimeIdentifier = "linux-x64",
                    CurrentOS = OperatingSystemPlatform.Linux,
                };
                config.OverrideServices = builder =>
                {
                    builder.Register(_ => CreateCommandExecutor(updateResult)).As<ICommandExecutor>().SingleInstance();
                };
            }
        );

        var client = container.GetApiClient();

        // Act
        var response = await client.GETAsync<CheckForUpdateEndpoint, ResultDTO<AppUpdateCheckDTO>>();

        // Assert
        var responseContent = await response.Response.Content.ReadAsStringAsync(CancellationToken);
        response.Response.StatusCode.ShouldBe(HttpStatusCode.OK, responseContent);
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.ShouldBeEmpty();

        var dto = response.Result.Value.ShouldNotBeNull();
        dto.IsUpdateAvailable.ShouldBeFalse();
        dto.NewestVersion.ShouldBe("0.38.0");
        dto.CurrentVersion.ShouldBe("0.38.0");
        dto.ReleaseNotes.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnUpdateAvailableFromActualHandlerWithoutNotification_WhenDockerModeAndReadTwice()
    {
        // Arrange
        var releasePayload = CreateStableReleasePayload();
        var githubHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        githubHandler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases")
            .ReturnsAsync(
                (HttpRequestMessage request, CancellationToken _) => CreateGitHubResponse(request, releasePayload)
            )
            .Verifiable(Times.Exactly(2));
        var githubClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        githubClientFactory
            .Setup(x => x.CreateClient("GitHub"))
            .Returns(() =>
                new HttpClient(githubHandler.Object, disposeHandler: false)
                {
                    BaseAddress = new Uri("https://api.github.com/"),
                }
            )
            .Verifiable(Times.Exactly(2));
        var asset = new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(0, 39, 0) };
        var updateManager = new Mock<UpdateManager>(
            new Mock<IUpdateSource>().Object,
            null!,
            new Mock<IVelopackLocator>(MockBehavior.Loose).Object
        );
        updateManager
            .Setup(x => x.CheckForUpdatesAsync())
            .ReturnsAsync(new UpdateInfo(asset, false))
            .Verifiable(Times.Exactly(2));
        using var container = await CreateContainer(
            new Seed(11002),
            config =>
            {
                config.OverrideAppBuildInfo = new MockAppBuildInfo
                {
                    RuntimeMode = "docker",
                    Version = "0.38.0",
                    InformationalVersion = "0.38.0",
                    RuntimeIdentifier = "linux-x64",
                    CurrentOS = OperatingSystemPlatform.Linux,
                };
                config.OverrideServices = builder =>
                    RegisterUpdateServices(builder, githubClientFactory.Object, updateManager.Object);
            }
        );
        var notificationService = container.Resolve<MockNotificationHubService>();
        var client = container.GetApiClient();

        // Act
        var firstResponse = await client.GETAsync<CheckForUpdateEndpoint, ResultDTO<AppUpdateCheckDTO>>();
        var secondResponse = await client.GETAsync<CheckForUpdateEndpoint, ResultDTO<AppUpdateCheckDTO>>();

        // Assert
        foreach (var response in new[] { firstResponse, secondResponse })
        {
            var responseContent = await response.Response.Content.ReadAsStringAsync(CancellationToken);
            response.Response.StatusCode.ShouldBe(HttpStatusCode.OK, responseContent);
            response.Result.IsSuccess.ShouldBeTrue();
            response.Result.Errors.ShouldBeEmpty();
            var dto = response.Result.Value.ShouldNotBeNull();
            dto.IsUpdateAvailable.ShouldBeTrue();
            dto.NewestVersion.ShouldBe("0.39.0");
            dto.CurrentVersion.ShouldBe("0.38.0");
            dto.ReleaseNotes.Count.ShouldBe(1);
            dto.ReleaseNotes[0].Version.ShouldBe("v0.39.0");
            dto.ReleaseNotes[0].Notes.ShouldBe("Stable release notes");
            dto.ReleaseNotes[0].ReleaseDate.ShouldBe(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));
            dto.ReleaseNotes[0].IsDevRelease.ShouldBeFalse();
        }
        notificationService.RefreshNotificationList.ShouldNotContain(RefreshDataType.UpdateAvailable);
        githubHandler.Verify();
        githubClientFactory.Verify();
        updateManager.Verify();
    }

    [Test]
    public async Task ShouldReturnFailureWithoutUpdateValueOrNotification_WhenActualGitHubFallbackFails()
    {
        // Arrange
        var githubHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        githubHandler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases")
            .ReturnsAsync(
                (HttpRequestMessage request, CancellationToken _) =>
                    new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request }
            )
            .Verifiable(Times.Once());
        var githubClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        githubClientFactory
            .Setup(x => x.CreateClient("GitHub"))
            .Returns(() =>
                new HttpClient(githubHandler.Object, disposeHandler: false)
                {
                    BaseAddress = new Uri("https://api.github.com/"),
                }
            )
            .Verifiable(Times.Once());
        var manager = new Mock<UpdateManager>(
            new Mock<IUpdateSource>().Object,
            null!,
            new Mock<IVelopackLocator>(MockBehavior.Loose).Object
        );
        manager.Setup(x => x.CheckForUpdatesAsync()).ReturnsAsync((UpdateInfo?)null).Verifiable(Times.Once());
        using var container = await CreateContainer(
            new Seed(11006),
            config =>
            {
                config.OverrideAppBuildInfo = new MockAppBuildInfo
                {
                    RuntimeMode = "docker",
                    Version = "0.38.0",
                    InformationalVersion = "0.38.0",
                    RuntimeIdentifier = "linux-x64",
                    CurrentOS = OperatingSystemPlatform.Linux,
                };
                config.OverrideServices = builder =>
                    RegisterUpdateServices(builder, githubClientFactory.Object, manager.Object);
            }
        );
        var notifications = container.Resolve<MockNotificationHubService>();
        using var client = container.GetApiClient();

        // Act
        var response = await client.GETAsync<CheckForUpdateEndpoint, ResultDTO<AppUpdateCheckDTO>>();

        // Assert
        response.Response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Result.IsSuccess.ShouldBeFalse();
        response.Result.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        response.Result.Errors.Count.ShouldBe(1);
        response.Result.Successes.Count.ShouldBe(0);
        response.Result.Value.ShouldBeNull();
        notifications.RefreshNotificationList.ShouldNotContain(RefreshDataType.UpdateAvailable);
        githubHandler.Verify();
        githubClientFactory.Verify();
        manager.Verify();
    }

    [Test]
    public async Task ShouldReturnUpdateAvailable_WhenDockerModeAndCommandExecutorReturnsDevUpdate()
    {
        // Arrange
        var seed = new Seed(11003);
        var updateResult = new AppUpdateCheckResult
        {
            IsUpdateAvailable = true,
            NewestVersion = "0.38.0-dev.7",
            CurrentVersion = "0.38.0",
            ReleaseNotes =
            [
                new ReleaseNote
                {
                    Version = "v0.38.0-dev.7",
                    Notes = "Development release notes",
                    ReleaseDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                    IsDevRelease = true,
                },
            ],
        };

        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.OverrideAppBuildInfo = new MockAppBuildInfo
                {
                    RuntimeMode = "docker",
                    Version = "0.38.0-dev.6",
                    InformationalVersion = "0.38.0-dev.6",
                    RuntimeIdentifier = "linux-x64",
                    CurrentOS = OperatingSystemPlatform.Linux,
                };
                config.OverrideServices = builder =>
                    builder.Register(_ => CreateCommandExecutor(updateResult)).As<ICommandExecutor>().SingleInstance();
            }
        );

        var client = container.GetApiClient();

        // Act
        var response = await client.GETAsync<CheckForUpdateEndpoint, ResultDTO<AppUpdateCheckDTO>>();

        // Assert
        var responseContent = await response.Response.Content.ReadAsStringAsync(CancellationToken);
        response.Response.StatusCode.ShouldBe(HttpStatusCode.OK, responseContent);
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.ShouldBeEmpty();

        var dto = response.Result.Value.ShouldNotBeNull();
        dto.IsUpdateAvailable.ShouldBeTrue();
        dto.NewestVersion.ShouldBe("0.38.0-dev.7");
        dto.CurrentVersion.ShouldBe("0.38.0");
        dto.ReleaseNotes.Count.ShouldBe(1);
        dto.ReleaseNotes[0].Version.ShouldBe("v0.38.0-dev.7");
        dto.ReleaseNotes[0].Notes.ShouldBe("Development release notes");
        dto.ReleaseNotes[0].IsDevRelease.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnUpdateAvailable_WhenDesktopModeAndCommandExecutorReturnsUpdate()
    {
        // Arrange
        var seed = new Seed(11004);
        var updateResult = new AppUpdateCheckResult
        {
            IsUpdateAvailable = true,
            NewestVersion = "9.9.9",
            CurrentVersion = "0.38.0",
            ReleaseNotes =
            [
                new ReleaseNote
                {
                    Version = "9.9.9",
                    Notes = "Desktop release notes",
                    ReleaseDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                    IsDevRelease = false,
                },
            ],
        };

        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.OverrideAppBuildInfo = new MockAppBuildInfo
                {
                    RuntimeMode = "desktop",
                    Version = "0.38.0",
                    RuntimeIdentifier = "linux-x64",
                    CurrentOS = OperatingSystemPlatform.Linux,
                };
                config.OverrideServices = builder =>
                    builder.Register(_ => CreateCommandExecutor(updateResult)).As<ICommandExecutor>().SingleInstance();
            }
        );

        var client = container.GetApiClient();

        // Act
        var response = await client.GETAsync<CheckForUpdateEndpoint, ResultDTO<AppUpdateCheckDTO>>();

        // Assert
        var responseContent = await response.Response.Content.ReadAsStringAsync(CancellationToken);
        response.Response.StatusCode.ShouldBe(HttpStatusCode.OK, responseContent);
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.ShouldBeEmpty();

        var dto = response.Result.Value.ShouldNotBeNull();
        dto.IsUpdateAvailable.ShouldBeTrue();
        dto.NewestVersion.ShouldBe("9.9.9");
        dto.CurrentVersion.ShouldBe("0.38.0");
        dto.ReleaseNotes.Count.ShouldBe(1);
        dto.ReleaseNotes[0].Version.ShouldBe("9.9.9");
        dto.ReleaseNotes[0].Notes.ShouldBe("Desktop release notes");
        dto.ReleaseNotes[0].IsDevRelease.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnNoUpdate_WhenDesktopModeAndCommandExecutorReturnsNoUpdate()
    {
        // Arrange
        var seed = new Seed(11005);

        var updateResult = new AppUpdateCheckResult
        {
            IsUpdateAvailable = false,
            NewestVersion = "0.38.0",
            CurrentVersion = "0.38.0",
            ReleaseNotes = [],
        };

        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.OverrideAppBuildInfo = new MockAppBuildInfo
                {
                    RuntimeMode = "desktop",
                    Version = "0.38.0",
                    RuntimeIdentifier = "linux-x64",
                    CurrentOS = OperatingSystemPlatform.Linux,
                };
                config.OverrideServices = builder =>
                    builder.Register(_ => CreateCommandExecutor(updateResult)).As<ICommandExecutor>().SingleInstance();
            }
        );

        var client = container.GetApiClient();

        // Act
        var response = await client.GETAsync<CheckForUpdateEndpoint, ResultDTO<AppUpdateCheckDTO>>();

        // Assert
        var responseContent = await response.Response.Content.ReadAsStringAsync(CancellationToken);
        response.Response.StatusCode.ShouldBe(HttpStatusCode.OK, responseContent);
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.ShouldBeEmpty();

        var dto = response.Result.Value.ShouldNotBeNull();
        dto.IsUpdateAvailable.ShouldBeFalse();
        dto.NewestVersion.ShouldBe("0.38.0");
        dto.CurrentVersion.ShouldBe("0.38.0");
        dto.ReleaseNotes.ShouldBeEmpty();
    }

    private static string CreateStableReleasePayload() =>
        JsonSerializer.Serialize(
            new[]
            {
                new GitHubReleaseDTO
                {
                    TagName = "v0.39.0",
                    Body = "Stable release notes",
                    PublishedAt = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                    Prerelease = false,
                },
            }
        );

    private static HttpResponseMessage CreateGitHubResponse(HttpRequestMessage request, string payload) =>
        new(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

    private static void RegisterUpdateServices(
        Autofac.ContainerBuilder builder,
        IHttpClientFactory githubClientFactory,
        UpdateManager updateManager
    )
    {
        builder.RegisterInstance(githubClientFactory).As<IHttpClientFactory>().SingleInstance();
        builder.RegisterInstance(updateManager).As<UpdateManager>().SingleInstance();
        builder.RegisterType<MockNotificationHubService>().As<INotificationHubService>().AsSelf().SingleInstance();
    }

    private static FakeCommandExecutor CreateCommandExecutor(AppUpdateCheckResult updateResult) =>
        new FakeCommandExecutor().Intercept<CheckForUpdatesCommand, Result<AppUpdateCheckResult>>(
            (_, _) => Task.FromResult(Result.Ok(updateResult))
        );
}
