namespace Reaparr.Application.UnitTests;

public class UpdateRadarrIntegrationEndpointUnitTests
    : BaseEndpointUnitTest<
        UpdateRadarrIntegrationEndpoint,
        UpdateRadarrIntegrationRequest,
        ResultDTO<RadarrIntegrationDTO>
    >
{
    [Test]
    public async Task ShouldUpdateIntegrationAndMarkChangesPending_WhenCategoryChanges()
    {
        // Arrange
        await SetupDatabase(55303);
        var dbContext = IDbContext;
        var integration = new RadarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000055303"),
            DisplayName = "Radarr",
            BaseUrl = "https://radarr.example.com",
            RadarrApiKey = "old-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "old-category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
        };
        dbContext.RadarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateRadarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = " Updated Radarr ",
            Url = " https://updated-radarr.example.com/ ",
            ApiKey = " new-key ",
            Category = " new-category ",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response!;
        var updated = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ApiKey.ShouldBe("new-key");
        updated.DisplayName.ShouldBe("Updated Radarr");
        updated.BaseUrl.ShouldBe("https://updated-radarr.example.com");
        updated.Category.ShouldBe("new-category");
        updated.DownloadFolderId.ShouldBe(FolderTypeDefaults.DefaultDownloadFolderId);
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.ChangesPending);
        updated.QBittorrentApiKey.ShouldBe("qbt_23456789ABCDEFGHIJKLMNPQ");
        updated.TorznabApiKey.ShouldBe("0123456789abcdef0123456789abcdef");
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.IntegrationChanged(
                        It.Is<RadarrIntegration>(value =>
                            value.Id == integration.Id
                            && value.DisplayName == "Updated Radarr"
                            && value.BaseUrl == "https://updated-radarr.example.com"
                            && value.RadarrApiKey == "new-key"
                            && value.Category == "new-category"
                            && value.DownloadFolderId == FolderTypeDefaults.DefaultDownloadFolderId
                            && value.ProvisioningState == IntegrationProvisioningState.ChangesPending
                        ),
                        TelemetryAction.Update,
                        true
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldMarkChangesPendingAndClearConnectionEvidence_WhenApiKeyChanges()
    {
        // Arrange
        await SetupDatabase(55305);
        var dbContext = IDbContext;
        var integration = new RadarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000055305"),
            DisplayName = "Radarr",
            BaseUrl = "https://radarr.example.com",
            RadarrApiKey = "old-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.ConnectionFailed,
            LastConnectionTestHttpStatusCode = StatusCodes.Status503ServiceUnavailable,
            LastConnectionTestErrorMessage = "Connection failed",
            LastConnectionTestedAt = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc),
        };
        dbContext.RadarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateRadarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Radarr",
            Url = "https://radarr.example.com",
            ApiKey = "new-key",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        updated.BaseUrl.ShouldBe("https://radarr.example.com");
        updated.RadarrApiKey.ShouldBe("new-key");
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.ChangesPending);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Unknown);
        updated.LastConnectionTestHttpStatusCode.ShouldBeNull();
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldBeNull();
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.IntegrationChanged(
                        It.Is<RadarrIntegration>(value =>
                            value.Id == integration.Id
                            && value.RadarrApiKey == "new-key"
                            && value.ProvisioningState == IntegrationProvisioningState.ChangesPending
                        ),
                        TelemetryAction.Update,
                        true
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldRemainConfiguredAndPreserveConnectionEvidence_WhenOnlyNameChanges()
    {
        // Arrange
        await SetupDatabase(55306);
        var dbContext = IDbContext;
        var testedAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var integration = new RadarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000055306"),
            DisplayName = "Radarr",
            BaseUrl = "https://radarr.example.com",
            RadarrApiKey = "key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.Success,
            LastConnectionTestHttpStatusCode = StatusCodes.Status200OK,
            LastConnectionTestErrorMessage = null,
            LastConnectionTestedAt = testedAt,
        };
        dbContext.RadarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateRadarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Renamed Radarr",
            Url = "https://radarr.example.com",
            ApiKey = "key",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        updated.DisplayName.ShouldBe("Renamed Radarr");
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Configured);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Success);
        updated.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status200OK);
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldBe(testedAt);
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.IntegrationChanged(
                        It.Is<RadarrIntegration>(value =>
                            value.Id == integration.Id
                            && value.DisplayName == "Renamed Radarr"
                            && value.ProvisioningState == IntegrationProvisioningState.Configured
                        ),
                        TelemetryAction.Update,
                        false
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldPreserveConnectionEvidenceAndSuppressTelemetry_WhenUrlAndApiKeyOnlyChangeByNormalization()
    {
        // Arrange
        await SetupDatabase(55307);
        var dbContext = IDbContext;
        var testedAt = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        var integration = new RadarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000055307"),
            DisplayName = "Radarr",
            BaseUrl = "https://radarr.example.com/",
            RadarrApiKey = " key ",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.Success,
            LastConnectionTestHttpStatusCode = StatusCodes.Status200OK,
            LastConnectionTestErrorMessage = null,
            LastConnectionTestedAt = testedAt,
        };
        dbContext.RadarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateRadarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Radarr",
            Url = "https://radarr.example.com///",
            ApiKey = " key ",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        updated.BaseUrl.ShouldBe("https://radarr.example.com");
        updated.RadarrApiKey.ShouldBe("key");
        endpointResult.Response.Value.ShouldNotBeNull().ApiKey.ShouldBe("key");
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Configured);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Success);
        updated.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status200OK);
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldBe(testedAt);
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.IntegrationChanged(
                        It.IsAny<RadarrIntegration>(),
                        It.IsAny<TelemetryAction>(),
                        It.IsAny<bool>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldRemainUnconfiguredAndClearConnectionEvidence_WhenConnectionIdentityChangesBeforeSetup()
    {
        // Arrange
        await SetupDatabase(55308);
        var dbContext = IDbContext;
        var integration = new RadarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000055308"),
            DisplayName = "Radarr",
            BaseUrl = "https://radarr.example.com",
            RadarrApiKey = "old-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Unconfigured,
            LastConnectionTestStatus = TestConnectionStatus.Success,
            LastConnectionTestHttpStatusCode = StatusCodes.Status200OK,
            LastConnectionTestErrorMessage = null,
            LastConnectionTestedAt = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
        };
        dbContext.RadarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateRadarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Radarr",
            Url = "https://new-radarr.example.com",
            ApiKey = "new-key",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Unconfigured);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Unknown);
        updated.LastConnectionTestHttpStatusCode.ShouldBeNull();
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnNotFound_WhenIntegrationDoesNotExist()
    {
        // Arrange
        await SetupDatabase(55304);
        var request = new UpdateRadarrIntegrationRequest
        {
            IntegrationId = Guid.Parse("00000000-0000-0000-0000-000000055304"),
            Name = "Missing Radarr",
            Url = "https://missing-radarr.example.com",
            ApiKey = "missing-key",
            Category = "missing-category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response!;

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        (await IDbContext.RadarrIntegrations.CountAsync(CancellationToken)).ShouldBe(0);
    }
}
