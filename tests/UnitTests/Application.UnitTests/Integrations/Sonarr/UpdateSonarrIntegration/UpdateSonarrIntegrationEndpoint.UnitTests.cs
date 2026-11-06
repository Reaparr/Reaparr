namespace Reaparr.Application.UnitTests;

public class UpdateSonarrIntegrationEndpointUnitTests
    : BaseEndpointUnitTest<
        UpdateSonarrIntegrationEndpoint,
        UpdateSonarrIntegrationRequest,
        ResultDTO<SonarrIntegrationDTO>
    >
{
    [Test]
    public async Task ShouldUpdateIntegrationAndMarkChangesPending_WhenCategoryChanges()
    {
        // Arrange
        await SetupDatabase(62603);
        var dbContext = IDbContext;
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062603"),
            DisplayName = "Sonarr",
            BaseUrl = "https://sonarr.example.com",
            SonarrApiKey = "old-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "old-category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
        };
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateSonarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = " Updated Sonarr ",
            Url = " https://updated-sonarr.example.com/ ",
            ApiKey = " new-key ",
            Category = " new-category ",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response!;
        var updated = await dbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ApiKey.ShouldBe("new-key");
        updated.DisplayName.ShouldBe("Updated Sonarr");
        updated.BaseUrl.ShouldBe("https://updated-sonarr.example.com");
        updated.Category.ShouldBe("new-category");
        updated.DownloadFolderId.ShouldBe(FolderTypeDefaults.DefaultDownloadFolderId);
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.ChangesPending);
        updated.QBittorrentApiKey.ShouldBe("qbt_23456789ABCDEFGHIJKLMNPQ");
        updated.TorznabApiKey.ShouldBe("0123456789abcdef0123456789abcdef");
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.IntegrationChanged(
                        It.Is<SonarrIntegration>(value =>
                            value.Id == integration.Id
                            && value.DisplayName == "Updated Sonarr"
                            && value.BaseUrl == "https://updated-sonarr.example.com"
                            && value.SonarrApiKey == "new-key"
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
    public async Task ShouldMarkChangesPendingAndClearConnectionEvidence_WhenUrlChanges()
    {
        // Arrange
        await SetupDatabase(62605);
        var dbContext = IDbContext;
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062605"),
            DisplayName = "Sonarr",
            BaseUrl = "https://sonarr.example.com",
            SonarrApiKey = "old-key",
            QBittorrentApiKey = "qbt_23456789ABCDEFGHIJKLMNPQ",
            TorznabApiKey = "0123456789abcdef0123456789abcdef",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Configured,
            LastConnectionTestStatus = TestConnectionStatus.Success,
            LastConnectionTestHttpStatusCode = StatusCodes.Status200OK,
            LastConnectionTestErrorMessage = null,
            LastConnectionTestedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateSonarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Sonarr",
            Url = "https://updated-sonarr.example.com",
            ApiKey = "old-key",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response!;
        var updated = await dbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        updated.BaseUrl.ShouldBe("https://updated-sonarr.example.com");
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.ChangesPending);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Unknown);
        updated.LastConnectionTestHttpStatusCode.ShouldBeNull();
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldBeNull();
    }

    [Test]
    public async Task ShouldMarkChangesPendingAndClearConnectionEvidence_WhenApiKeyChanges()
    {
        // Arrange
        await SetupDatabase(62606);
        var dbContext = IDbContext;
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062606"),
            DisplayName = "Sonarr",
            BaseUrl = "https://sonarr.example.com",
            SonarrApiKey = "old-key",
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
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateSonarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Sonarr",
            Url = "https://sonarr.example.com/",
            ApiKey = "new-key",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.ChangesPending);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Unknown);
        updated.LastConnectionTestHttpStatusCode.ShouldBeNull();
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldBeNull();
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.IntegrationChanged(
                        It.Is<SonarrIntegration>(value =>
                            value.Id == integration.Id
                            && value.SonarrApiKey == "new-key"
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
        await SetupDatabase(62607);
        var dbContext = IDbContext;
        var testedAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062607"),
            DisplayName = "Sonarr",
            BaseUrl = "https://sonarr.example.com",
            SonarrApiKey = "key",
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
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateSonarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Renamed Sonarr",
            Url = "https://sonarr.example.com",
            ApiKey = "key",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        updated.DisplayName.ShouldBe("Renamed Sonarr");
        updated.ProvisioningState.ShouldBe(IntegrationProvisioningState.Configured);
        updated.LastConnectionTestStatus.ShouldBe(TestConnectionStatus.Success);
        updated.LastConnectionTestHttpStatusCode.ShouldBe(StatusCodes.Status200OK);
        updated.LastConnectionTestErrorMessage.ShouldBeNull();
        updated.LastConnectionTestedAt.ShouldBe(testedAt);
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.IntegrationChanged(
                        It.Is<SonarrIntegration>(value =>
                            value.Id == integration.Id
                            && value.DisplayName == "Renamed Sonarr"
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
        await SetupDatabase(62608);
        var dbContext = IDbContext;
        var testedAt = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062608"),
            DisplayName = "Sonarr",
            BaseUrl = "https://sonarr.example.com/",
            SonarrApiKey = " key ",
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
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateSonarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Sonarr",
            Url = "https://sonarr.example.com///",
            ApiKey = " key ",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response!.Errors.Count.ShouldBe(0);
        updated.BaseUrl.ShouldBe("https://sonarr.example.com");
        updated.SonarrApiKey.ShouldBe("key");
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
                        It.IsAny<SonarrIntegration>(),
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
        await SetupDatabase(62609);
        var dbContext = IDbContext;
        var integration = new SonarrIntegration
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000062609"),
            DisplayName = "Sonarr",
            BaseUrl = "https://sonarr.example.com",
            SonarrApiKey = "old-key",
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
        dbContext.SonarrIntegrations.Add(integration);
        await dbContext.SaveChangesAsync(CancellationToken);
        var request = new UpdateSonarrIntegrationRequest
        {
            IntegrationId = integration.Id,
            Name = "Sonarr",
            Url = "https://new-sonarr.example.com",
            ApiKey = "new-key",
            Category = "category",
            DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var updated = await dbContext.SonarrIntegrations.SingleAsync(CancellationToken);

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
        await SetupDatabase(62604);
        var request = new UpdateSonarrIntegrationRequest
        {
            IntegrationId = Guid.Parse("00000000-0000-0000-0000-000000062604"),
            Name = "Missing Sonarr",
            Url = "https://missing-sonarr.example.com",
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
        (await IDbContext.SonarrIntegrations.CountAsync(CancellationToken)).ShouldBe(0);
    }
}
