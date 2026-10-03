namespace Reaparr.Application.UnitTests;

public class UpdateScheduledDownloadLimitsCommandUnitTests : BaseCommandUnitTest<UpdateScheduledDownloadLimitsCommand>
{
    [Test]
    public async Task ShouldRedistributeCappedSharesAndRemoveInactiveGrants_WhenServersHaveMultipleFiles()
    {
        // Arrange
        await SetupDatabase(
            85301,
            config =>
            {
                config.PlexServerCount = 4;
                config.PlexMovieLibraryCount = 1;
                config.MovieDownloadTasksCount = 0;
            }
        );
        var dbContext = IDbContext;
        var servers = (await dbContext.PlexServers.ToListAsync(CancellationToken))
            .OrderBy(x => x.MachineIdentifier, StringComparer.Ordinal)
            .ToList();
        servers.Count.ShouldBe(4);
        var activeIds = servers.Take(3).Select(x => x.Id).ToList();
        foreach (var server in servers.Take(3))
        {
            var libraryId = await dbContext
                .PlexLibraries.Where(x => x.PlexServerId == server.Id && x.Type == PlexMediaType.Movie)
                .Select(x => x.Id)
                .FirstAsync(CancellationToken);
            var tasks = FakeData.GetMovieDownloadTask(new Seed(85310 + server.Id)).Generate(2);
            tasks.SetRelationshipIds(server.Id, libraryId);
            dbContext.DownloadTaskMovie.AddRange(tasks);
        }

        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .DownloadTaskMovieFile.Where(x => activeIds.Contains(x.PlexServerId))
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );
        var activeFileServers = await dbContext
            .DownloadTaskMovieFile.Where(x => x.DownloadStatus == DownloadStatus.Downloading)
            .Select(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        activeFileServers.Distinct().Order().ShouldBe(activeIds.Order());
        activeFileServers.Count.ShouldBeGreaterThan(3);

        var settings = new UserSettings();
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = 1000 } },
        };
        settings.ServerSettings.SetDownloadSpeedLimit(servers[0].MachineIdentifier, 100);
        settings.ServerSettings.SetDownloadSpeedLimit(servers[3].MachineIdentifier, 200);
        speedLimits.SetScheduledDownloadSpeedLimits(
            new Dictionary<string, long> { [servers[3].MachineIdentifier] = 1 }
        );
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(speedLimits).As<IDownloadSpeedLimitProvider>();
        });

        // Act
        var result = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse("2026-10-05T12:00:00Z"))
        );

        // Assert
        servers
            .Select(x => speedLimits.GetEffectiveDownloadSpeedLimit(x.MachineIdentifier))
            .ShouldBe(new long[] { 102400, 460800, 460800, 204800 });
        settings
            .ServerSettings.Data.Single(x => x.MachineIdentifier == servers[0].MachineIdentifier)
            .DownloadSpeedLimit.ShouldBe(100);
        settings
            .ServerSettings.Data.Single(x => x.MachineIdentifier == servers[3].MachineIdentifier)
            .DownloadSpeedLimit.ShouldBe(200);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldSplitOneKilobyteWithoutRoundingUp_WhenThreeServersAreDownloading()
    {
        // Arrange
        await SetupDatabase(
            85302,
            config =>
            {
                config.PlexServerCount = 3;
                config.PlexMovieLibraryCount = 1;
                config.MovieDownloadTasksCount = 0;
            }
        );
        var dbContext = IDbContext;
        foreach (var server in await dbContext.PlexServers.ToListAsync(CancellationToken))
        {
            var libraryId = await dbContext
                .PlexLibraries.Where(x => x.PlexServerId == server.Id && x.Type == PlexMediaType.Movie)
                .Select(x => x.Id)
                .FirstAsync(CancellationToken);
            var tasks = FakeData.GetMovieDownloadTask(new Seed(85320 + server.Id)).Generate(1);
            tasks.SetRelationshipIds(server.Id, libraryId);
            dbContext.DownloadTaskMovie.AddRange(tasks);
        }

        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
            CancellationToken
        );
        var servers = (await dbContext.PlexServers.Select(x => x.MachineIdentifier).ToListAsync(CancellationToken))
            .Order(StringComparer.Ordinal)
            .ToList();
        servers.Count.ShouldBe(3);
        var settings = new UserSettings();
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = 1 } },
        };
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(speedLimits).As<IDownloadSpeedLimitProvider>();
        });

        // Act
        var result = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse("2026-10-05T12:00:00Z"))
        );

        // Assert
        servers.Select(speedLimits.GetEffectiveDownloadSpeedLimit).ShouldBe(new long[] { 341, 341, 342 });
        settings.ServerSettings.Data.ShouldBeEmpty();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReleaseOnlyScheduledLimits_WhenDisabledOrTheCurrentIntervalIsUnlimited(bool enabled)
    {
        // Arrange
        var settings = new UserSettings();
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule { Enabled = enabled };
        settings.ServerSettings.SetDownloadSpeedLimit("capped", 100);
        speedLimits.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["capped"] = 1, ["uncapped"] = 2 });
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(speedLimits).As<IDownloadSpeedLimitProvider>();
        });

        // Act
        var result = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse("2026-10-05T12:00:00Z"))
        );

        // Assert
        speedLimits.GetEffectiveDownloadSpeedLimit("capped").ShouldBe(102400);
        speedLimits.GetEffectiveDownloadSpeedLimit("uncapped").ShouldBe(0);
        settings.ServerSettings.GetDownloadSpeedLimit("capped").ShouldBe(100);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task ShouldKeepTheLastAllocation_WhenTheCurrentSavedPolicyIsInvalid(int invalidLimit)
    {
        // Arrange
        var settings = new UserSettings();
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = invalidLimit } },
        };
        speedLimits.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["retained"] = 12345 });
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(speedLimits).As<IDownloadSpeedLimitProvider>();
        });

        // Act
        var result = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse("2026-10-05T12:00:00Z"))
        );

        // Assert
        speedLimits.GetEffectiveDownloadSpeedLimit("retained").ShouldBe(12345);
        settings.ServerSettings.Data.ShouldBeEmpty();
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldLeaveExcessBudgetUnused_WhenEveryDownloadingServerHasAFiniteManualCap()
    {
        // Arrange
        await SetupDatabase(
            85331,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieDownloadTasksCount = 0;
            }
        );
        using var dbContext = IDbContext;
        var servers = (await dbContext.PlexServers.ToListAsync(CancellationToken))
            .OrderBy(x => x.MachineIdentifier, StringComparer.Ordinal)
            .ToList();
        servers.Count.ShouldBe(2);
        foreach (var server in servers)
        {
            var libraryId = await dbContext
                .PlexLibraries.Where(x => x.PlexServerId == server.Id && x.Type == PlexMediaType.Movie)
                .Select(x => x.Id)
                .FirstAsync(CancellationToken);
            var tasks = FakeData.GetMovieDownloadTask(new Seed(85330 + server.Id)).Generate(1);
            tasks.SetRelationshipIds(server.Id, libraryId);
            dbContext.DownloadTaskMovie.AddRange(tasks);
        }

        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
            CancellationToken
        );
        var settings = new UserSettings();
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = 1000 } },
        };
        settings.ServerSettings.SetDownloadSpeedLimit(servers[0].MachineIdentifier, 100);
        settings.ServerSettings.SetDownloadSpeedLimit(servers[1].MachineIdentifier, 200);
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(speedLimits).As<IDownloadSpeedLimitProvider>();
        });

        // Act
        var result = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse("2026-10-05T12:00:00Z"))
        );

        // Assert
        servers
            .Select(x => speedLimits.GetEffectiveDownloadSpeedLimit(x.MachineIdentifier))
            .ShouldBe(new long[] { 102400, 204800 });
        servers
            .Select(x => settings.ServerSettings.GetDownloadSpeedLimit(x.MachineIdentifier))
            .ShouldBe(new[] { 100, 200 });
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(DownloadStatus.Paused)]
    [Arguments(DownloadStatus.AutoPaused)]
    public async Task ShouldReleaseScheduledGrantsWithoutResumingFiles_WhenAllFilesArePaused(
        DownloadStatus pausedStatus
    )
    {
        // Arrange
        await SetupDatabase(85332, config => config.MovieDownloadTasksCount = 1);
        using var dbContext = IDbContext;
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, pausedStatus),
            CancellationToken
        );
        var machineIdentifier = await dbContext
            .DownloadTaskMovieFile.Select(x => x.PlexServer!.MachineIdentifier)
            .FirstAsync(CancellationToken);
        var settings = new UserSettings();
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = 1000 } },
        };
        settings.ServerSettings.SetDownloadSpeedLimit(machineIdentifier, 100);
        speedLimits.SetScheduledDownloadSpeedLimits(
            new Dictionary<string, long> { [machineIdentifier] = 1, ["inactive"] = 2 }
        );
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(speedLimits).As<IDownloadSpeedLimitProvider>();
        });

        // Act
        var result = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse("2026-10-05T12:00:00Z"))
        );

        // Assert
        speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier).ShouldBe(102400);
        speedLimits.GetEffectiveDownloadSpeedLimit("inactive").ShouldBe(0);
        (await dbContext.DownloadTaskMovieFile.Select(x => x.DownloadStatus).ToListAsync(CancellationToken)).ShouldBe(
            new[] { pausedStatus }
        );
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments("2026-10-05T08:59:59+05:45", 0)]
    [Arguments("2026-10-05T09:00:00+05:45", 200)]
    [Arguments("2026-10-05T09:29:59+05:45", 200)]
    [Arguments("2026-10-05T09:30:00+05:45", 400)]
    [Arguments("2026-10-05T17:29:59+05:45", 400)]
    [Arguments("2026-10-05T17:30:00+05:45", 0)]
    [Arguments("2026-10-04T23:45:00+05:45", 800)]
    [Arguments("2026-10-05T00:00:00+05:45", 0)]
    [Arguments("2026-10-06T00:00:00+05:45", 0)]
    public async Task ShouldApplyTheLatestDailyPointWithoutCrossingMidnight_WhenLocalTimeChanges(
        string localTime,
        int expectedKb
    )
    {
        // Arrange
        await SetupDatabase(85303, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
            CancellationToken
        );
        var machineIdentifier = await dbContext
            .PlexServers.Select(x => x.MachineIdentifier)
            .SingleAsync(CancellationToken);
        var settings = new UserSettings();
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new()
            {
                ["Monday"] = new()
                {
                    ["17:30"] = null,
                    ["09:30"] = 400,
                    ["09:00"] = 200,
                },
                ["Sunday"] = new() { ["23:30"] = 800 },
            },
        };
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        speedLimits.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { [machineIdentifier] = 12345 });
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(speedLimits).As<IDownloadSpeedLimitProvider>();
        });

        // Act
        var result = await TestHandlerExecuteAsync(
            new UpdateScheduledDownloadLimitsCommand(DateTimeOffset.Parse(localTime))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier).ShouldBe(expectedKb * 1024L);
        settings.ServerSettings.Data.ShouldBeEmpty();
    }
}
