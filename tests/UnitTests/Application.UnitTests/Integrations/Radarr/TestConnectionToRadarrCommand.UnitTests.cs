using System.Net;

namespace Reaparr.Application.UnitTests;

public class TestConnectionToRadarrCommandUnitTests : BaseCommandUnitTest<TestConnectionToRadarrCommand>
{
    [Test]
    public async Task ShouldReturnInvalidApiKeyWithoutPersisting_WhenDraftReturnsUnauthorized()
    {
        // Arrange
        var command = new TestConnectionToRadarrCommand(null, "http://radarr.test", "draft-key");
        var client = new HttpClient(new StatusCodeHandler(HttpStatusCode.Unauthorized, "Unauthorized"))
        {
            BaseAddress = new Uri("http://radarr.test"),
        };

        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.Create("http://radarr.test", "draft-key"))
            .Returns(Result.Ok(client))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(TestConnectionStatus.InvalidApiKey);
        result.Value.HttpStatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        result.Value.ErrorMessage.ShouldBe("Unauthorized");
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldOverwriteStoredFailure_WhenPersistedConnectionSucceeds()
    {
        // Arrange
        await SetupDatabase(55331);
        var dbContext = IDbContext;
        var integration = new RadarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000055331"),
            DisplayName = "Radarr",
            BaseUrl = "http://radarr.test",
            RadarrApiKey = "stored-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "movies",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.ConnectionFailed,
            LastConnectionTestHttpStatusCode = StatusCodes.Status503ServiceUnavailable,
            LastConnectionTestErrorMessage = "Service Unavailable",
            LastConnectionTestedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        dbContext.RadarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = DateTime.UtcNow;
        var command = new TestConnectionToRadarrCommand(integration.Id, null, null);
        var client = new HttpClient(new StatusCodeHandler(HttpStatusCode.OK, "OK"))
        {
            BaseAddress = new Uri("http://radarr.test"),
        };

        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.Create("http://radarr.test", "stored-key"))
            .Returns(Result.Ok(client))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(TestConnectionStatus.Success);
        var updated = await dbContext.RadarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Success);
        updated.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status200OK);
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldNotBeNull();
        updated.LastConnectionTestedAt.Value.ShouldBeGreaterThanOrEqualTo(before);
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldUseDraftCredentialsWithoutPersisting_WhenPersistedIntegrationHasDraftValues()
    {
        // Arrange
        await SetupDatabase(55332);
        var dbContext = IDbContext;
        var lastTestedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var integration = new RadarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000055332"),
            DisplayName = "Radarr",
            BaseUrl = "http://stored-radarr.test",
            RadarrApiKey = "stored-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "movies",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.ConnectionFailed,
            LastConnectionTestHttpStatusCode = StatusCodes.Status503ServiceUnavailable,
            LastConnectionTestErrorMessage = "Service Unavailable",
            LastConnectionTestedAt = lastTestedAt,
        };
        dbContext.RadarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var command = new TestConnectionToRadarrCommand(integration.Id, "http://draft-radarr.test", "draft-key");
        var client = new HttpClient(new StatusCodeHandler(HttpStatusCode.OK, "OK"))
        {
            BaseAddress = new Uri("http://draft-radarr.test"),
        };

        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.Create("http://draft-radarr.test", "draft-key"))
            .Returns(Result.Ok(client))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(TestConnectionStatus.Success);
        var unchanged = await dbContext.RadarrIntegrations.AsNoTracking().SingleAsync(CancellationToken);
        unchanged.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.ConnectionFailed);
        unchanged.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        unchanged.LastConnectionTestErrorMessage.ShouldBe("Service Unavailable");
        unchanged.LastConnectionTestedAt.ShouldBe(lastTestedAt);
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldRejectIncompleteDraftCredentials_WhenPersistedIntegrationIdIsProvided()
    {
        // Arrange
        var command = new TestConnectionToRadarrCommand(Guid.NewGuid(), "http://radarr.test", null);

        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result.Ok(new HttpClient()))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message == "Provide either an integration ID or a URL and API key.");
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldRejectInvalidUrl_WhenDraftConnectionHasNoIntegrationId()
    {
        // Arrange
        var command = new TestConnectionToRadarrCommand(null, "not-a-url", "draft-key");

        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result.Ok(new HttpClient()))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message == "Provided Radarr URL must be a valid http/https URL.");
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldRejectIncompleteDraftCredentials_WhenNoIntegrationIdIsProvided()
    {
        // Arrange
        var command = new TestConnectionToRadarrCommand(null, "http://radarr.test", null);

        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result.Ok(new HttpClient()))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message == "Provide either an integration ID or a URL and API key.");
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
    }

    [Test]
    public async Task ShouldReturnNotFound_WhenPersistedIntegrationDoesNotExist()
    {
        // Arrange
        await SetupDatabase(55333);
        var command = new TestConnectionToRadarrCommand(Guid.Parse("00000000-0000-0000-0000-000000055333"), null, null);

        Mock.Mock<IRadarrHttpClientFactory>()
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result.Ok(new HttpClient()))
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<TestConnectionResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Has404NotFoundError().ShouldBeTrue();
        Mock.Mock<IRadarrHttpClientFactory>().Verify();
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
