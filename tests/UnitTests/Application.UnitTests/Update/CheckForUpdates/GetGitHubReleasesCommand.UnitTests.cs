using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Moq.Contrib.HttpClient;

namespace Reaparr.Application.UnitTests;

public class GetGitHubReleasesCommandUnitTests : BaseUnitTest<GetGitHubReleasesCommandHandler>
{
    [Test]
    public async Task ShouldReturnOnlyNewerDevReleases_WhenCurrentVersionIsDevRelease()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.38.0-dev.5", isPrerelease: true),
            CreateRelease("v0.38.0-dev.6", isPrerelease: true),
            CreateRelease("v0.38.0-dev.7", isPrerelease: true),
            CreateRelease("v0.38.0", isPrerelease: false),
        };
        var expectedVersions = new[] { "v0.38.0-dev.7" };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.6");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Version).ToList().ShouldBe(expectedVersions);
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnOnlyNewerStableReleases_WhenCurrentVersionIsStable()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.38.0-dev.7", isPrerelease: true),
            CreateRelease("v0.38.0", isPrerelease: false),
            CreateRelease("v0.38.1-dev.1", isPrerelease: true),
            CreateRelease("v0.38.1", isPrerelease: false),
        };
        var expectedVersions = new[] { "v0.38.1" };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Version).ToList().ShouldBe(expectedVersions);
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldExcludeSameVersionRelease_WhenCurrentVersionMatchesRelease()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.38.0-dev.5", isPrerelease: true),
            CreateRelease("v0.38.0-dev.6", isPrerelease: true),
        };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.6");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldExcludeOlderReleases_WhenReleasesAreBehindCurrentVersion()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.37.0-dev.1", isPrerelease: true),
            CreateRelease("v0.37.0-dev.2", isPrerelease: true),
        };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.1");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnEmptyList_WhenNoReleasesExist()
    {
        // Arrange

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.1");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient([]))
            .Verifiable(Times.Once());

        // Act

        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnFailure_WhenHttpRequestThrows()
    {
        // Arrange

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.1");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(
                new HttpClient(new ThrowingHttpMessageHandler()) { BaseAddress = new Uri("https://api.github.com/") }
            )
            .Verifiable(Times.Once());

        // Act

        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldSkipMalformedReleaseTags_WhenGitHubContainsNonSemverRelease()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("dev-latest", isPrerelease: true),
            CreateRelease("v0.38.0-dev.7", isPrerelease: true),
        };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.6");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act

        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Version).ShouldBe(["v0.38.0-dev.7"]);
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public void ShouldSetGitHubAuthorizationHeader_WhenGitHubTokenIsConfigured()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.GitHubToken = "test-github-token");

        var services = new ServiceCollection();
        services.RegisterGitHubHttpClient(Mock.Container.Resolve<IAppRuntimeInfo>());

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var client = httpClientFactory.CreateClient(HttpClientModule.GitHubClientName);

        // Assert
        client.DefaultRequestHeaders.Authorization.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization.Scheme.ShouldBe("Bearer");
        client.DefaultRequestHeaders.Authorization.Parameter.ShouldBe("test-github-token");
    }

    [Test]
    public void ShouldNotSetGitHubAuthorizationHeader_WhenGitHubTokenIsMissing()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.GitHubToken = null);

        var services = new ServiceCollection();

        // Act
        services.RegisterGitHubHttpClient(Mock.Container.Resolve<IAppRuntimeInfo>());
        using var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var client = httpClientFactory.CreateClient(HttpClientModule.GitHubClientName);

        // Assert
        client.DefaultRequestHeaders.Authorization.ShouldBeNull();
    }

    [Test]
    public async Task ShouldMapReleaseNotesCorrectly_WhenDevReleaseIsReturned()
    {
        // Arrange
        var publishedAt = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var releases = new[]
        {
            new GitHubReleaseDTO
            {
                TagName = "v0.38.0-dev.7",
                Prerelease = true,
                Body = "Release notes body",
                PublishedAt = publishedAt,
            },
        };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.6");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        var note = result.Value[0];
        note.Version.ShouldBe("v0.38.0-dev.7");
        note.Notes.ShouldBe("Release notes body");
        note.ReleaseDate.ShouldBe(publishedAt);
        note.IsDevRelease.ShouldBeTrue();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldMapIsDevReleaseFalse_WhenStableReleaseIsReturned()
    {
        // Arrange
        var publishedAt = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var releases = new[]
        {
            new GitHubReleaseDTO
            {
                TagName = "v0.38.1",
                Prerelease = false,
                Body = "Stable release notes",
                PublishedAt = publishedAt,
            },
        };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        var note = result.Value[0];
        note.Version.ShouldBe("v0.38.1");
        note.IsDevRelease.ShouldBeFalse();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldExcludeStableReleases_WhenCurrentVersionIsDevRelease()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.38.0-dev.7", isPrerelease: true),
            CreateRelease("v0.38.0", isPrerelease: false),
            CreateRelease("v0.39.0", isPrerelease: false),
        };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.6");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Version).ShouldBe(["v0.38.0-dev.7"]);
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldExcludeDevReleases_WhenCurrentVersionIsStable()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.38.1-dev.1", isPrerelease: true),
            CreateRelease("v0.38.1-dev.2", isPrerelease: true),
            CreateRelease("v0.38.1", isPrerelease: false),
            CreateRelease("v0.39.0-dev.1", isPrerelease: true),
        };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Version).ShouldBe(["v0.38.1"]);
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnMultipleNewerDevReleases_WhenSeveralExistAboveCurrentVersion()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.38.0-dev.5", isPrerelease: true),
            CreateRelease("v0.38.0-dev.6", isPrerelease: true),
            CreateRelease("v0.38.0-dev.7", isPrerelease: true),
            CreateRelease("v0.38.0-dev.8", isPrerelease: true),
        };
        var expectedVersions = new[] { "v0.38.0-dev.7", "v0.38.0-dev.8" };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.6");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Version).ToList().ShouldBe(expectedVersions);
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnMultipleNewerStableReleases_WhenSeveralExistAboveCurrentVersion()
    {
        // Arrange
        var releases = new[]
        {
            CreateRelease("v0.38.0", isPrerelease: false),
            CreateRelease("v0.38.1", isPrerelease: false),
            CreateRelease("v0.39.0", isPrerelease: false),
        };
        var expectedVersions = new[] { "v0.38.1", "v0.39.0" };

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(CreateGitHubHttpClient(releases))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Version).ToList().ShouldBe(expectedVersions);
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnEmptyList_WhenApiReturnsNull()
    {
        // Arrange

        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0-dev.1");

        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(
                new HttpClient(new NullResponseHttpMessageHandler())
                {
                    BaseAddress = new Uri("https://api.github.com/"),
                }
            )
            .Verifiable(Times.Once());

        // Act

        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnLaterStableRelease_WhenFirstPageContainsOnlyDevReleases()
    {
        // Arrange
        var firstPage = Enumerable.Range(1, 100)
            .Select(index => CreateRelease($"v0.40.0-dev.{index}", isPrerelease: true))
            .ToArray();
        GitHubReleaseDTO[] secondPage =
        [
            CreateRelease("v0.40.0", isPrerelease: false),
            CreateRelease("v0.38.0", isPrerelease: false),
            CreateRelease("v0.37.0", isPrerelease: false),
            CreateRelease("v0.40.0-dev.101", isPrerelease: true),
            CreateRelease("invalid", isPrerelease: false),
        ];
        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0");
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases?per_page=100&page=1")
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => CreateGitHubResponse(request, firstPage))
            .Verifiable(Times.Once());
        handler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases?per_page=100&page=2")
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => CreateGitHubResponse(request, secondPage))
            .Verifiable(Times.Once());
        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://api.github.com/") })
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.Select(x => x.Version).ShouldBe(["v0.40.0"]);
        handler.Verify();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRetainMatchingReleasesAcrossPages_WhenFinalDataPageIsFullOrShort(bool fullLastPage)
    {
        // Arrange
        var firstPage = Enumerable.Range(1, 100)
            .Select(index => CreateRelease($"v0.40.0-dev.{index}", isPrerelease: true))
            .ToArray();
        firstPage[0] = CreateRelease("v0.40.0", isPrerelease: false);
        var secondPage = new List<GitHubReleaseDTO> { CreateRelease("v0.39.0", isPrerelease: false) };
        if (fullLastPage)
            secondPage.AddRange(
                Enumerable.Range(1, 99).Select(index => CreateRelease($"v0.39.0-dev.{index}", isPrerelease: true))
            );
        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0");
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases?per_page=100&page=1")
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => CreateGitHubResponse(request, firstPage))
            .Verifiable(Times.Once());
        handler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases?per_page=100&page=2")
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => CreateGitHubResponse(request, secondPage))
            .Verifiable(Times.Once());
        if (fullLastPage)
            handler
                .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases?per_page=100&page=3")
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => CreateGitHubResponse(request, []))
                .Verifiable(Times.Once());
        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://api.github.com/") })
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.Select(x => x.Version).ShouldBe(["v0.40.0", "v0.39.0"]);
        handler.Verify();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldNotReturnPartialReleases_WhenLaterPageFailsOrIsCancelled(bool cancelRequest)
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        var firstPage = Enumerable.Range(1, 100)
            .Select(index => CreateRelease($"v0.40.0-dev.{index}", isPrerelease: true))
            .ToArray();
        firstPage[0] = CreateRelease("v0.40.0", isPrerelease: false);
        SetAppBuildInfo(x => x.InformationalVersion = "0.38.0");
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases?per_page=100&page=1")
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => CreateGitHubResponse(request, firstPage))
            .Verifiable(Times.Once());
        handler
            .SetupRequest(HttpMethod.Get, "https://api.github.com/repos/Reaparr/Reaparr/releases?per_page=100&page=2")
            .ReturnsAsync(
                (HttpRequestMessage request, CancellationToken token) =>
                {
                    if (cancelRequest)
                    {
                        cancellation.Cancel();
                        token.ThrowIfCancellationRequested();
                    }
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request };
                }
            )
            .Verifiable(Times.Once());
        Mock.Mock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(HttpClientModule.GitHubClientName))
            .Returns(new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://api.github.com/") })
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetGitHubReleasesCommand(), cancellation.Token);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelRequest);
        result.Errors.Count.ShouldBe(1);
        var error = result.Errors[0].ShouldBeOfType<ExceptionalError>();
        if (!cancelRequest)
            error.Exception.ShouldBeOfType<HttpRequestException>().StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        result.ValueOrDefault.ShouldBeNull();
        handler.Verify();
        Mock.Mock<IHttpClientFactory>().Verify();
    }

    private static HttpResponseMessage CreateGitHubResponse(
        HttpRequestMessage request,
        IReadOnlyList<GitHubReleaseDTO> releases
    ) =>
        new(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(JsonSerializer.Serialize(releases), Encoding.UTF8, "application/json"),
        };

    private static HttpClient CreateGitHubHttpClient(IReadOnlyList<GitHubReleaseDTO> releases) =>
        new(new GitHubReleasesResponseHandler(releases)) { BaseAddress = new Uri("https://api.github.com/") };

    private static GitHubReleaseDTO CreateRelease(string tagName, bool isPrerelease) =>
        new()
        {
            TagName = tagName,
            Prerelease = isPrerelease,
            PublishedAt = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
        };

    private sealed class GitHubReleasesResponseHandler(IReadOnlyList<GitHubReleaseDTO> releases) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(JsonSerializer.Serialize(releases), Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("Simulated network failure");
    }

    private sealed class NullResponseHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent("null", Encoding.UTF8, "application/json"),
                }
            );
    }
}
