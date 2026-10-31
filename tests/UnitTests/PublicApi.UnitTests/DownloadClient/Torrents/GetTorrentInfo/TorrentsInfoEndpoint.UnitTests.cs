using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using Reaparr.PublicAPI.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class TorrentsInfoEndpointUnitTests
    : BaseEndpointUnitTest<TorrentsInfoEndpoint, TorrentsInfoEndpointRequest, List<QBittorrentTorrentInfo>>
{
    private async Task<IntegrationIdentity> GetRadarrIdentity() =>
        (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();

    [Test]
    public async Task ShouldReturnMovie_WhenRadarrUsesCustomCategory()
    {
        // Arrange
        const string category = "Custom Radarr";
        const string hash = "hash-custom-radarr";
        await SetupDatabase(
            5107,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        await dbContext
            .RadarrIntegrations.Where(x => x.Id == integration.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Category, category), CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == movieFile.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.OwnedOverride, false), CancellationToken);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.HashId, hash).SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new TorrentsInfoEndpointRequest { Hashes = hash, Category = category },
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.Count.ShouldBe(1);
        endpointResult.Response[0].Hash.ShouldBe(hash);
        endpointResult.Response[0].Category.ShouldBe(category);
    }

    [Test]
    public async Task ShouldReturnEpisode_WhenSonarrUsesCustomCategory()
    {
        // Arrange
        const string category = "Custom Sonarr";
        const string hash = "hash-custom-sonarr";
        await SetupDatabase(
            5108,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.SonarrIntegrationCount = 1;
            }
        );

        var dbContext = IDbContext;
        var episodeFile = await dbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);
        var integration = await dbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        await dbContext
            .SonarrIntegrations.Where(x => x.Id == integration.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Category, category), CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == episodeFile.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.OwnedOverride, false), CancellationToken);
        await dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == episodeFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.SonarrIntegrationId, integration.Id)
                        .SetProperty(p => p.HashId, hash)
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new TorrentsInfoEndpointRequest { Hashes = hash, Category = category },
            integrationIdentity: integration.Id.ToSonarrIdentity()
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.Count.ShouldBe(1);
        endpointResult.Response[0].Hash.ShouldBe(hash);
        endpointResult.Response[0].Category.ShouldBe(category);
    }

    [Test]
    public async Task ShouldUseOwningIntegrationCategory_WhenMediaTypeAndIntegrationTypeDiffer()
    {
        // Arrange
        const string category = "custom-radarr-episode";
        const string hash = "hash-radarr-episode";
        await SetupDatabase(
            5109,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );

        var dbContext = IDbContext;
        var episodeFile = await dbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        await dbContext
            .RadarrIntegrations.Where(x => x.Id == integration.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Category, category), CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == episodeFile.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.OwnedOverride, false), CancellationToken);
        await dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == episodeFile.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.HashId, hash).SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new TorrentsInfoEndpointRequest { Hashes = hash, Category = category },
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.Count.ShouldBe(1);
        endpointResult.Response[0].Hash.ShouldBe(hash);
        endpointResult.Response[0].Category.ShouldBe(category);
    }

    [Test]
    public async Task ShouldReturnRatioLimitZero_WhenStatusIsCompleted()
    {
        // Arrange — Radarr only sets CanBeRemoved when HasReachedSeedLimit() is true.
        // ratio_limit=0 with ratio=0 satisfies that check so DELETE is triggered after import.
        var result = await PrepareAndExecuteTorrentsInfoTest(5101, "hash-completed", DownloadStatus.Completed);

        // Assert
        var response = result.Response;
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response[0].RatioLimit.ShouldBe(0);
        response[0].State.ShouldBe("pausedUP");
        result.PersistedStatus.ShouldBe(DownloadStatus.Completed);
        result.PersistedHash.ShouldBe("hash-completed");
    }

    [Test]
    public async Task ShouldReturnRatioLimitZero_WhenStatusIsMoveFinished()
    {
        // Arrange
        var result = await PrepareAndExecuteTorrentsInfoTest(5102, "hash-move-finished", DownloadStatus.MoveFinished);

        // Assert
        var response = result.Response;
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response[0].RatioLimit.ShouldBe(0);
        response[0].State.ShouldBe("pausedUP");
        result.PersistedStatus.ShouldBe(DownloadStatus.MoveFinished);
        result.PersistedHash.ShouldBe("hash-move-finished");
    }

    [Test]
    public async Task ShouldReturnRatioLimitZero_WhenStatusIsDownloadFinished()
    {
        // Arrange
        var result = await PrepareAndExecuteTorrentsInfoTest(
            5103,
            "hash-download-finished",
            DownloadStatus.DownloadFinished
        );

        // Assert
        var response = result.Response;
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response[0].RatioLimit.ShouldBe(0);
        response[0].State.ShouldBe("pausedUP");
        result.PersistedStatus.ShouldBe(DownloadStatus.DownloadFinished);
        result.PersistedHash.ShouldBe("hash-download-finished");
    }

    [Test]
    public async Task ShouldReturnRatioLimitMinusTwo_WhenStatusIsDownloading()
    {
        // Arrange
        // active downloads must not be flagged as ready for removal.
        var result = await PrepareAndExecuteTorrentsInfoTest(5104, "hash-downloading", DownloadStatus.Downloading);

        // Assert
        var response = result.Response;
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response[0].RatioLimit.ShouldBe(-2);
        response[0].State.ShouldBe("downloading");
        result.PersistedStatus.ShouldBe(DownloadStatus.Downloading);
        result.PersistedHash.ShouldBe("hash-downloading");
    }

    [Test]
    public async Task ShouldReturnRatioLimitMinusTwo_WhenStatusIsMovePaused()
    {
        // Arrange
        // a paused mid-move must not be flagged as ready for removal.
        var result = await PrepareAndExecuteTorrentsInfoTest(5105, "hash-move-paused", DownloadStatus.MovePaused);

        // Assert
        var response = result.Response;
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response[0].RatioLimit.ShouldBe(-2);
        response[0].State.ShouldBe("pausedUP");
        result.PersistedStatus.ShouldBe(DownloadStatus.MovePaused);
        result.PersistedHash.ShouldBe("hash-move-paused");
    }

    [Test]
    public async Task ShouldReturnEmptyList_WhenServerIsOwned()
    {
        // Arrange
        await SetupDatabase(
            5106,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.Where(x => x.Id == movieFile.PlexServerId)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.IsEnabled, true).SetProperty(p => p.OwnedOverride, true),
                CancellationToken
            );

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-owned")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        // Act
        var testResult = await TestEndpointHandleAsync(
            new TorrentsInfoEndpointRequest
            {
                Hashes = "hash-owned",
                Category = IntegrationDefinitions.RADARR_DEFAULT_CATEGORY,
            },
            integrationIdentity: await GetRadarrIdentity()
        );

        // Assert
        testResult.Response.ShouldNotBeNull();
        testResult.Response.ShouldBeEmpty();

        var persisted = await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .Select(x => new { x.HashId, x.DownloadStatus })
            .FirstAsync(CancellationToken);
        persisted.HashId.ShouldBe("hash-owned");
        persisted.DownloadStatus.ShouldBe(DownloadStatus.Completed);

        var server = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .FirstAsync(x => x.Id == movieFile.PlexServerId, CancellationToken);
        server.IsEnabled.ShouldBeTrue();
        server.OwnedOverride.ShouldBe(true);
    }

    [Test]
    [Arguments(DownloadStatus.AuthError, "error")]
    [Arguments(DownloadStatus.StorageError, "error")]
    [Arguments(DownloadStatus.SourceUnavailable, "error")]
    [Arguments(DownloadStatus.DownloadClientError, "error")]
    [Arguments(DownloadStatus.IntegrityError, "error")]
    [Arguments(DownloadStatus.Restarting, "queuedDL")]
    [Arguments(DownloadStatus.Queued, "queuedDL")]
    [Arguments(DownloadStatus.Paused, "pausedDL")]
    [Arguments(DownloadStatus.Moving, "moving")]
    [Arguments(DownloadStatus.Unknown, "stalledDL")]
    public async Task ShouldReportFileStateWithoutChangingTasksOrLeakingOtherOwnership(
        DownloadStatus status,
        string expectedState
    )
    {
        // Arrange
        await SetupDatabase(
            5138,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 2;
                config.RadarrIntegrationCount = 2;
            }
        );
        var dbContext = IDbContext;
        var integrations = await dbContext.RadarrIntegrations.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var files = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .Include(x => x.Parent)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        integrations.Count.ShouldBe(2);
        for (var index = 0; index < files.Count; index++)
        {
            files[index].RadarrIntegrationId = integrations[index].Id;
            files[index].Parent!.RadarrIntegrationId = integrations[index].Id;
            dbContext
                .Entry(files[index])
                .CurrentValues.SetValues(new { HashId = new string(index == 0 ? 'a' : 'b', 40) });
            files[index].DownloadStatus = status;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServers.ExecuteUpdateAsync(
            update =>
                update.SetProperty(server => server.IsEnabled, true).SetProperty(server => server.OwnedOverride, false),
            CancellationToken
        );
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
                x.RadarrIntegrationId,
            })
            .ToListAsync(CancellationToken);
        before.Select(x => x.DownloadStatus).ShouldBe([status, status]);

        // Act
        var response = await TestEndpointHandleAsync(
            new TorrentsInfoEndpointRequest { Hashes = "all" },
            integrationIdentity: integrations[0].Id.ToRadarrIdentity()
        );

        // Assert
        response.IsValid.ShouldBeTrue();
        response.ValidationErrors.Count.ShouldBe(0);
        response.StatusCode.ShouldBe(200);
        response.Response.ShouldNotBeNull();
        response
            .Response.Select(torrent => (torrent.Hash, torrent.State, torrent.Category, torrent.RatioLimit))
            .ShouldBe([(files[0].HashId!, expectedState, integrations[0].Category, -2f)]);
        (
            await dbContext
                .DownloadTaskMovieFile.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ParentId,
                    x.HashId,
                    x.DownloadStatus,
                    x.RadarrIntegrationId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                executor => executor.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    private async Task<(
        List<QBittorrentTorrentInfo> Response,
        DownloadStatus PersistedStatus,
        string? PersistedHash
    )> PrepareAndExecuteTorrentsInfoTest(int seed, string hash, DownloadStatus downloadStatus)
    {
        await SetupDatabase(
            seed,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.Where(x => x.Id == movieFile.PlexServerId)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.IsEnabled, true).SetProperty(p => p.OwnedOverride, false),
                CancellationToken
            );

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.HashId, hash).SetProperty(p => p.DownloadStatus, downloadStatus),
                CancellationToken
            );

        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new TorrentsInfoEndpointRequest { Hashes = hash, Category = integration.Category },
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        var persistedRow = await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .Select(x => new { x.DownloadStatus, x.HashId })
            .FirstAsync(CancellationToken);

        endpointResult.ShouldNotBeNull();
        endpointResult.Response.ShouldNotBeNull();

        return (endpointResult.Response, persistedRow.DownloadStatus, persistedRow.HashId);
    }
}
