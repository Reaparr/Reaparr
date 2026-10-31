using System.Net;

namespace Reaparr.Application.UnitTests;

public class TestConnectionToSonarrCommandUnitTests : BaseCommandUnitTest<TestConnectionToSonarrCommand>
{
    [Test]
    public async Task ShouldReturnConnectionFailedWithoutPersisting_WhenDraftReturnsServiceUnavailable()
    {
        // Arrange
        var command = new TestConnectionToSonarrCommand(null, "http://sonarr.test", "draft-key");
        var client = new HttpClient(new StatusCodeHandler(HttpStatusCode.ServiceUnavailable, "Service Unavailable"))
        {
            BaseAddress = new Uri("http://sonarr.test"),
        };

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.Create("http://sonarr.test", "draft-key"))
            .Returns(Result.Ok(client))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(TestConnectionStatus.ConnectionFailed);
        result.Value.HttpStatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        result.Value.ErrorMessage.ShouldBe("Service Unavailable");
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldOverwriteStoredSuccess_WhenPersistedConnectionFails()
    {
        // Arrange
        await SetupDatabase(62631);
        var dbContext = IDbContext;
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062631"),
            DisplayName = "Sonarr",
            BaseUrl = "http://sonarr.test",
            SonarrApiKey = "stored-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "series",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.Success,
            LastConnectionTestHttpStatusCode = StatusCodes.Status200OK,
            LastConnectionTestedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = DateTime.UtcNow;
        var command = new TestConnectionToSonarrCommand(integration.Id, null, null);
        var client = new HttpClient(new StatusCodeHandler(HttpStatusCode.BadGateway, "Bad Gateway"))
        {
            BaseAddress = new Uri("http://sonarr.test"),
        };

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.Create("http://sonarr.test", "stored-key"))
            .Returns(Result.Ok(client))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(TestConnectionStatus.ConnectionFailed);
        var updated = await dbContext.SonarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.ConnectionFailed);
        updated.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        updated.LastConnectionTestErrorMessage.ShouldBe("Bad Gateway");
        updated.LastConnectionTestedAt.ShouldNotBeNull();
        updated.LastConnectionTestedAt.Value.ShouldBeGreaterThanOrEqualTo(before);
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldUseDraftCredentialsWithoutPersisting_WhenPersistedIntegrationHasDraftValues()
    {
        // Arrange
        await SetupDatabase(62633);
        var dbContext = IDbContext;
        var lastTestedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062633"),
            DisplayName = "Sonarr",
            BaseUrl = "http://stored-sonarr.test",
            SonarrApiKey = "stored-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "series",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.Success,
            LastConnectionTestHttpStatusCode = StatusCodes.Status200OK,
            LastConnectionTestedAt = lastTestedAt,
        };
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var command = new TestConnectionToSonarrCommand(integration.Id, "http://draft-sonarr.test", "draft-key");
        var client = new HttpClient(new StatusCodeHandler(HttpStatusCode.BadGateway, "Bad Gateway"))
        {
            BaseAddress = new Uri("http://draft-sonarr.test"),
        };

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.Create("http://draft-sonarr.test", "draft-key"))
            .Returns(Result.Ok(client))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(TestConnectionStatus.ConnectionFailed);
        result.Value.HttpStatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        result.Value.ErrorMessage.ShouldBe("Bad Gateway");
        var unchanged = await dbContext.SonarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);
        unchanged.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Success);
        unchanged.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status200OK);
        unchanged.LastConnectionTestErrorMessage.ShouldBeNull();
        unchanged.LastConnectionTestedAt.ShouldBe(lastTestedAt);
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnNotFound_WhenPersistedIntegrationDoesNotExist()
    {
        // Arrange
        await SetupDatabase(62632);
        var command = new TestConnectionToSonarrCommand(Guid.Parse("00000000-0000-0000-0000-000000062632"), null, null);

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result.Ok(new HttpClient()))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Has404NotFoundError().ShouldBeTrue();
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldRejectInvalidUrl_WhenDraftConnectionHasNoIntegrationId()
    {
        // Arrange
        var command = new TestConnectionToSonarrCommand(null, "not-a-url", "draft-key");

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result.Ok(new HttpClient()))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message == "Provided Sonarr URL must be a valid http/https URL.");
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldRejectIncompleteDraftCredentials_WhenNoIntegrationIdIsProvided()
    {
        // Arrange
        var command = new TestConnectionToSonarrCommand(null, "http://sonarr.test", null);

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result.Ok(new HttpClient()))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message == "Provide either an integration ID or a URL and API key.");
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode, string reasonPhrase) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(statusCode) { RequestMessage = request, ReasonPhrase = reasonPhrase }
            );
    }
}
