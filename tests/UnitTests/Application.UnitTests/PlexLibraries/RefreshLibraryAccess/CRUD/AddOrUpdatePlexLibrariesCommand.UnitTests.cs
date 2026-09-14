namespace Reaparr.Application.UnitTests;

public class AddOrUpdatePlexLibrariesCommandUnitTests : BaseUnitTest<AddOrUpdatePlexLibrariesCommandHandler>
{
    [Before(Test)]
    public void SetupComparisonJobLifecycle()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<InvalidateLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
    }

    [Test]
    public async Task ShouldAddAllPlexLibraries_WhenNoneExistInTheDatabase()
    {
        // Arrange
        var serverCount = 5;
        var libraryCount = 5;
        var seed = await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = serverCount;
                config.PlexAccountCount = 1;
            }
        );

        var plexAccount = IDbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServers = await IDbContext.PlexServers.ToListAsync(CancellationToken);
        plexServers.ShouldNotBeNull();

        var plexLibraries = new List<PlexLibrary>();
        foreach (var plexServer in plexServers)
        {
            var list = FakeData.GetPlexLibrary(seed).Generate(libraryCount);
            foreach (var plexLibrary in list)
                plexLibrary.PlexServerId = plexServer.Id;

            plexLibraries.AddRange(list);
        }

        Mock.SetupCommand(() => It.IsAny<QueueLibrarySyncJobCommand>()).ReturnsAsync(Result.Ok());

        // Act
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = plexLibraries,
        };
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(serverCount);
        foreach (var rapport in result.Value)
        {
            rapport.GetGranted.Count.ShouldBe(libraryCount);
            rapport.GetUpdated.Count.ShouldBe(0);
            rapport.GetRevoked.Count.ShouldBe(0);
        }

        var plexLibrariesDb = IDbContext.PlexLibraries.ToList();
        plexLibrariesDb.Count.ShouldBe(serverCount * libraryCount);
        var plexAccountLibrariesDb = IDbContext.PlexAccountLibraries.ToList();
        plexAccountLibrariesDb.Count.ShouldBe(serverCount * libraryCount);

        foreach (var expectedPlexLibrary in plexLibraries)
        {
            var plexLibraryDb = plexLibrariesDb.Find(x => x.Key == expectedPlexLibrary.Key);
            plexLibraryDb.ShouldNotBeNull();
        }

        foreach (var plexAccountLibrary in plexAccountLibrariesDb)
        {
            plexAccountLibrary.PlexAccountId.ShouldBe(plexAccount.Id);
            plexAccountLibrary.PlexServerId.ShouldBeInRange(1, serverCount);
            plexAccountLibrary.PlexLibraryId.ShouldBeInRange(1, serverCount * libraryCount);
        }

        var expectedLibraryIds = plexLibrariesDb.Select(x => x.Id).OrderBy(x => x).ToList();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<QueueLibrarySyncJobCommand>(command =>
                            command.PlexLibraryIds.OrderBy(id => id).SequenceEqual(expectedLibraryIds)
                            && !command.ForceLibrarySync
                            && !command.ForceMediaRefresh
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldPersistGrantedHistoryEvents_WhenNewLibraryAccessIsGranted()
    {
        // Arrange
        var serverCount = 1;
        var libraryCount = 2;
        var seed = await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = serverCount;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = dbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServer = await dbContext.PlexServers.FirstAsync(CancellationToken);
        var plexLibraries = FakeData.GetPlexLibrary(seed).Generate(libraryCount);
        foreach (var plexLibrary in plexLibraries)
            plexLibrary.PlexServerId = plexServer.Id;

        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = plexLibraries,
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var historyEvents = await dbContext
            .PlexLibraryAccessHistoryEvents.OrderBy(x => x.PlexLibraryId)
            .ToListAsync(CancellationToken);

        historyEvents.Count.ShouldBe(libraryCount);
        historyEvents.Select(x => x.State).ShouldAllBe(x => x == PlexAccessState.Granted);
        historyEvents.Select(x => x.PlexAccountId).ShouldAllBe(x => x == plexAccount.Id);
        historyEvents.Select(x => x.PlexAccountNameSnapshot).ShouldAllBe(x => x == plexAccount.DisplayName);
        historyEvents.Select(x => x.PlexServerId).ShouldAllBe(x => x == plexServer.Id);
        historyEvents.Select(x => x.PlexServerNameSnapshot).ShouldAllBe(x => x == plexServer.Name);
        historyEvents.Select(x => x.PlexLibraryNameSnapshot).ShouldBe(plexLibraries.Select(x => x.Name));
        historyEvents.Select(x => x.RefreshRunId).Distinct().Count().ShouldBe(1);
        historyEvents.Select(x => x.CreatedAt).ShouldAllBe(x => x > DateTime.MinValue);
    }

    [Test]
    public async Task ShouldPersistRevokedAndBackfillGrantedHistoryEvents_WhenLibraryAccessChangesWithoutPriorHistory()
    {
        // Arrange
        var serverCount = 1;
        var libraryCount = 3;
        await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = serverCount;
                config.PlexMovieLibraryCount = libraryCount;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = dbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexLibraries = dbContext.PlexLibraries.AsTracking().OrderBy(x => x.Id).ToList();
        var removedLibrary = plexLibraries.Last();
        var incomingLibraries = plexLibraries.Take(libraryCount - 1).ToList();
        var updatedTime = DateTime.UtcNow - TimeSpan.FromHours(2);
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = incomingLibraries.ToApiLibraries(updatedTime),
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var historyEvents = await dbContext.PlexLibraryAccessHistoryEvents.ToListAsync(CancellationToken);

        historyEvents.Count.ShouldBe(3);
        historyEvents.Count(x => x.State == PlexAccessState.Granted).ShouldBe(2);
        historyEvents.Count(x => x.State == PlexAccessState.Revoked).ShouldBe(1);

        var revokedEvent = historyEvents.Single(x => x.State == PlexAccessState.Revoked);
        revokedEvent.PlexAccountId.ShouldBe(plexAccount.Id);
        revokedEvent.PlexAccountNameSnapshot.ShouldBe(plexAccount.DisplayName);
        revokedEvent.PlexServerId.ShouldBe(removedLibrary.PlexServerId);
        revokedEvent.PlexLibraryId.ShouldBe(removedLibrary.Id);
        revokedEvent.PlexLibraryNameSnapshot.ShouldBe(removedLibrary.Name);

        var grantedLibraryIdsNullable = historyEvents
            .Where(x => x.State == PlexAccessState.Granted)
            .Select(x => x.PlexLibraryId)
            .ToList();
        grantedLibraryIdsNullable.ShouldAllBe(x => x.HasValue);

        var grantedLibraryIds = grantedLibraryIdsNullable.Select(x => x!.Value).OrderBy(x => x).ToList();
        grantedLibraryIds.ShouldBe(incomingLibraries.Select(x => x.Id).OrderBy(x => x).ToList());

        historyEvents.Select(x => x.RefreshRunId).Distinct().Count().ShouldBe(1);
        historyEvents.Select(x => x.CreatedAt).ShouldAllBe(x => x > DateTime.MinValue);
    }

    [Test]
    public async Task ShouldReportGrantedOnly_WhenExistingLibraryAccessIsRestored()
    {
        // Arrange
        await SetupDatabase(
            35,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = await dbContext.PlexAccounts.FirstAsync(CancellationToken);
        var plexLibrary = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        await dbContext
            .PlexAccountLibraries.Where(x =>
                x.PlexAccountId == plexAccount.Id && x.PlexLibraryId == plexLibrary.Id
            )
            .ExecuteDeleteAsync(CancellationToken);

        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = new List<PlexLibrary> { plexLibrary }.ToApiLibraries(DateTime.UtcNow),
        };

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var rapport = result.Value.Single();
        rapport.GetGranted.Count.ShouldBe(1);
        rapport.GetUpdated.Count.ShouldBe(0);
        rapport.GetRevoked.Count.ShouldBe(0);
        rapport.GetGranted.Single().PlexLibraryId.ShouldBe(plexLibrary.Id);

        var accountLibraryAccess = await dbContext
            .PlexAccountLibraries.Where(x => x.PlexAccountId == plexAccount.Id)
            .ToListAsync(CancellationToken);
        accountLibraryAccess.Count.ShouldBe(1);
        accountLibraryAccess.Single().PlexLibraryId.ShouldBe(plexLibrary.Id);

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldUpdatePlexLibraries_WhenTheyExistInTheDatabase()
    {
        // Arrange
        var serverCount = 5;
        var libraryCount = 5;
        await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = serverCount;
                config.PlexMovieLibraryCount = libraryCount;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = dbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServers = await dbContext.PlexServers.ToListAsync(CancellationToken);
        plexServers.ShouldNotBeNull();
        var destinationFolderId = await dbContext.FolderPaths.Select(x => x.Id).FirstAsync(CancellationToken);

        // Set values that should not be overwritten by refreshing the libraries
        var syncedAtDateTime = DateTime.UtcNow - TimeSpan.FromHours(6);
        var plexLibraries = dbContext.PlexLibraries.AsTracking().ToList();
        foreach (var plexLibrary in plexLibraries)
        {
            // Should not be overwritten because this happens when media is synced
            plexLibrary.SyncedAt = syncedAtDateTime;
            plexLibrary.DefaultDestinationId = destinationFolderId;
        }

        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.MediaSize, 123_456_789)
                    .SetProperty(y => y.MovieCount, 11)
                    .SetProperty(y => y.TvShowCount, 12)
                    .SetProperty(y => y.SeasonCount, 13)
                    .SetProperty(y => y.EpisodeCount, 14)
                    .SetProperty(y => y.ActorsCount, 15)
                    .SetProperty(y => y.GenresCount, 16)
                    .SetProperty(y => y.CountriesCount, 17),
            CancellationToken
        );

        // Create API Data
        var updatedTime = DateTime.UtcNow - TimeSpan.FromHours(4);
        var changedContentChangedAt = plexLibraries.Max(x => x.ContentChangedAt) + 1;
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = plexLibraries.ToApiLibraries(updatedTime, contentChangedAt: changedContentChangedAt),
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(serverCount);
        foreach (var rapport in result.Value)
        {
            rapport.GetGranted.Count.ShouldBe(0);
            rapport.GetUpdated.Count.ShouldBe(libraryCount);
            rapport.GetRevoked.Count.ShouldBe(0);
        }

        var plexLibrariesDb = IDbContext.PlexLibraries.ToList();
        plexLibrariesDb.Count.ShouldBe(serverCount * libraryCount);
        var plexAccountLibrariesDb = IDbContext.PlexAccountLibraries.ToList();
        plexAccountLibrariesDb.Count.ShouldBe(serverCount * libraryCount);

        foreach (var expectedPlexLibrary in plexLibraries)
        {
            var plexLibraryDb = plexLibrariesDb.Find(x => x.Uuid == expectedPlexLibrary.Uuid);
            plexLibraryDb.ShouldNotBeNull();
            plexLibraryDb.UpdatedAt.ShouldBe(updatedTime);
            plexLibraryDb.SyncedAt.ShouldBe(syncedAtDateTime);
            plexLibraryDb.Outdated.ShouldBeTrue();
            plexLibraryDb.DefaultDestinationId.ShouldBe(destinationFolderId);
            plexLibraryDb.MediaSize.ShouldBe(123_456_789);
            plexLibraryDb.MovieCount.ShouldBe(11);
            plexLibraryDb.TvShowCount.ShouldBe(12);
            plexLibraryDb.SeasonCount.ShouldBe(13);
            plexLibraryDb.EpisodeCount.ShouldBe(14);
            plexLibraryDb.ActorsCount.ShouldBe(15);
            plexLibraryDb.GenresCount.ShouldBe(16);
            plexLibraryDb.CountriesCount.ShouldBe(17);
        }

        foreach (var plexAccountLibrary in plexAccountLibrariesDb)
        {
            plexAccountLibrary.PlexAccountId.ShouldBe(plexAccount.Id);
            plexAccountLibrary.PlexServerId.ShouldBeInRange(1, serverCount);
            plexAccountLibrary.PlexLibraryId.ShouldBeInRange(1, serverCount * libraryCount);
        }

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<QueueLibrarySyncJobCommand>(command =>
                            command
                                .PlexLibraryIds.OrderBy(id => id)
                                .SequenceEqual(plexLibrariesDb.Select(y => y.Id).OrderBy(id => id))
                            && !command.ForceLibrarySync
                            && !command.ForceMediaRefresh
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldNotMarkLibraryOutdated_WhenContentChangeWasAlreadySynced()
    {
        // Arrange
        await SetupDatabase(
            33,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = await dbContext.PlexAccounts.FirstAsync(CancellationToken);
        var plexLibrary = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        var syncedAt = DateTime.UtcNow - TimeSpan.FromHours(1);
        var incomingUpdatedAt = syncedAt - TimeSpan.FromMinutes(5);
        plexLibrary.SyncedAt = syncedAt;
        plexLibrary.SyncedContentChangedAt = plexLibrary.ContentChangedAt + 1;
        plexLibrary.Outdated = false;
        await dbContext.SaveChangesAsync(CancellationToken);

        var incomingLibrary = new List<PlexLibrary> { plexLibrary }
            .ToApiLibraries(incomingUpdatedAt, contentChangedAt: plexLibrary.SyncedContentChangedAt)
            .Single();
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = [incomingLibrary],
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var updatedLibrary = await dbContext.PlexLibraries.AsNoTracking().SingleAsync(CancellationToken);
        updatedLibrary.UpdatedAt.ShouldBe(incomingUpdatedAt);
        updatedLibrary.SyncedAt.ShouldBe(syncedAt);
        updatedLibrary.ContentChangedAt.ShouldBe(incomingLibrary.ContentChangedAt);
        updatedLibrary.Outdated.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldMarkLibraryOutdated_WhenContentChangedAfterLastSync()
    {
        // Arrange
        await SetupDatabase(
            34,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = await dbContext.PlexAccounts.FirstAsync(CancellationToken);
        var plexLibrary = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        var syncedAt = DateTime.UtcNow - TimeSpan.FromHours(2);
        var incomingUpdatedAt = syncedAt + TimeSpan.FromMinutes(5);
        plexLibrary.SyncedAt = syncedAt;
        plexLibrary.Outdated = false;
        await dbContext.SaveChangesAsync(CancellationToken);

        var incomingLibrary = new List<PlexLibrary> { plexLibrary }
            .ToApiLibraries(incomingUpdatedAt, contentChangedAt: plexLibrary.ContentChangedAt + 1)
            .Single();
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = [incomingLibrary],
        };

        Mock.SetupCommand(() => It.IsAny<QueueLibrarySyncJobCommand>()).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var updatedLibrary = await dbContext.PlexLibraries.AsNoTracking().SingleAsync(CancellationToken);
        updatedLibrary.UpdatedAt.ShouldBe(incomingUpdatedAt);
        updatedLibrary.SyncedAt.ShouldBe(syncedAt);
        updatedLibrary.ContentChangedAt.ShouldBe(incomingLibrary.ContentChangedAt);
        updatedLibrary.Outdated.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<QueueLibrarySyncJobCommand>(command =>
                            command.PlexLibraryIds.SequenceEqual(new[] { updatedLibrary.Id })
                            && !command.ForceLibrarySync
                            && !command.ForceMediaRefresh
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldPreserveComputedLibraryMetrics_WhenIncomingPlexLibraryRefreshContainsDefaultMetrics()
    {
        // Arrange
        await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 3;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = await dbContext.PlexAccounts.FirstAsync(CancellationToken);
        var existingLibraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        existingLibraries.Count.ShouldBe(3);

        var expectedMetrics = new Dictionary<int, ExpectedLibraryMetrics>();
        for (var i = 0; i < existingLibraries.Count; i++)
        {
            var library = existingLibraries[i];
            var metrics = new ExpectedLibraryMetrics(
                MediaSize: 100_000_000 + i,
                MovieCount: 10 + i,
                TvShowCount: 20 + i,
                SeasonCount: 30 + i,
                EpisodeCount: 40 + i,
                ActorsCount: 50 + i,
                GenresCount: 60 + i,
                CountriesCount: 70 + i
            );
            expectedMetrics.Add(library.Id, metrics);

            await dbContext
                .PlexLibraries.Where(x => x.Id == library.Id)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(y => y.MediaSize, metrics.MediaSize)
                            .SetProperty(y => y.MovieCount, metrics.MovieCount)
                            .SetProperty(y => y.TvShowCount, metrics.TvShowCount)
                            .SetProperty(y => y.SeasonCount, metrics.SeasonCount)
                            .SetProperty(y => y.EpisodeCount, metrics.EpisodeCount)
                            .SetProperty(y => y.ActorsCount, metrics.ActorsCount)
                            .SetProperty(y => y.GenresCount, metrics.GenresCount)
                            .SetProperty(y => y.CountriesCount, metrics.CountriesCount),
                    CancellationToken
                );
        }

        var updatedTime = DateTime.UtcNow - TimeSpan.FromHours(3);
        var changedContentChangedAt = existingLibraries.Max(x => x.ContentChangedAt) + 1;
        var incomingLibraries = existingLibraries.ToApiLibraries(
            updatedTime,
            contentChangedAt: changedContentChangedAt
        );
        for (var i = 0; i < incomingLibraries.Count; i++)
        {
            var incomingLibrary = incomingLibraries[i];
            incomingLibrary.Title = $"Updated Plex Library {i}";
            incomingLibrary.Key = $"updated-key-{i}";
            incomingLibrary.CreatedAt = DateTime.UtcNow - TimeSpan.FromDays(10 + i);
            incomingLibrary.ScannedAt = DateTime.UtcNow - TimeSpan.FromDays(5 + i);
            incomingLibrary.Language = $"updated-language-{i}";
        }

        incomingLibraries.ShouldAllBe(x => x.MediaSize == 0);
        incomingLibraries.ShouldAllBe(x => x.MovieCount == 0);
        incomingLibraries.ShouldAllBe(x => x.TvShowCount == 0);
        incomingLibraries.ShouldAllBe(x => x.SeasonCount == 0);
        incomingLibraries.ShouldAllBe(x => x.EpisodeCount == 0);
        incomingLibraries.ShouldAllBe(x => x.ActorsCount == 0);
        incomingLibraries.ShouldAllBe(x => x.GenresCount == 0);
        incomingLibraries.ShouldAllBe(x => x.CountriesCount == 0);

        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = incomingLibraries,
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Single().GetUpdated.Count.ShouldBe(existingLibraries.Count);
        result.Value.Single().GetGranted.Count.ShouldBe(0);
        result.Value.Single().GetRevoked.Count.ShouldBe(0);

        var updatedLibraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        updatedLibraries.Count.ShouldBe(existingLibraries.Count);
        foreach (var updatedLibrary in updatedLibraries)
        {
            var metrics = expectedMetrics[updatedLibrary.Id];
            var incomingLibrary = incomingLibraries.Single(x => x.Id == updatedLibrary.Id);
            updatedLibrary.Title.ShouldBe(incomingLibrary.Title);
            updatedLibrary.Key.ShouldBe(incomingLibrary.Key);
            updatedLibrary.CreatedAt.ShouldBe(incomingLibrary.CreatedAt);
            updatedLibrary.UpdatedAt.ShouldBe(updatedTime);
            updatedLibrary.ScannedAt.ShouldBe(incomingLibrary.ScannedAt);
            updatedLibrary.ContentChangedAt.ShouldBe(changedContentChangedAt);
            updatedLibrary.Uuid.ShouldBe(incomingLibrary.Uuid);
            updatedLibrary.Language.ShouldBe(incomingLibrary.Language);
            updatedLibrary.Outdated.ShouldBeTrue();
            updatedLibrary.MediaSize.ShouldBe(metrics.MediaSize);
            updatedLibrary.MovieCount.ShouldBe(metrics.MovieCount);
            updatedLibrary.TvShowCount.ShouldBe(metrics.TvShowCount);
            updatedLibrary.SeasonCount.ShouldBe(metrics.SeasonCount);
            updatedLibrary.EpisodeCount.ShouldBe(metrics.EpisodeCount);
            updatedLibrary.ActorsCount.ShouldBe(metrics.ActorsCount);
            updatedLibrary.GenresCount.ShouldBe(metrics.GenresCount);
            updatedLibrary.CountriesCount.ShouldBe(metrics.CountriesCount);
        }
    }

    [Test]
    public async Task ShouldDeletePlexLibraryAccess_WhenThePlexServerHasNoPlexLibraries()
    {
        // Arrange
        var serverCount = 5;
        var libraryCount = 6;
        await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = serverCount;
                config.PlexMovieLibraryCount = libraryCount;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = dbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexLibraries = dbContext.PlexLibraries.AsTracking().ToList();

        // Remove even numbered plexLibraries
        for (var i = plexLibraries.Count - 1; i >= 0; i--)
            if (i % 2 == 0)
                plexLibraries.RemoveAt(i);

        var updatedTime = DateTime.UtcNow - TimeSpan.FromHours(2);
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = plexLibraries.ToApiLibraries(updatedTime),
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(serverCount);
        foreach (var rapport in result.Value)
        {
            rapport.GetGranted.Count.ShouldBe(0);
            rapport.GetUpdated.ShouldAllBe(x => x.PlexLibraryId % 2 == 0);
            rapport.GetRevoked.ShouldAllBe(x => x.PlexLibraryId % 2 != 0);
        }

        var plexLibrariesDb = IDbContext.PlexLibraries.ToList();
        plexLibrariesDb.Count.ShouldBe(serverCount * libraryCount);
        var plexAccountLibrariesDb = IDbContext.PlexAccountLibraries.ToList();
        plexAccountLibrariesDb.Count.ShouldBe(request.PlexLibraries.Count);

        plexAccountLibrariesDb.Select(x => x.PlexAccountId).ShouldAllBe(x => x == plexAccount.Id);
        plexAccountLibrariesDb.Select(x => x.PlexServerId).ShouldAllBe(x => x > 0 && x <= serverCount);
        plexAccountLibrariesDb.Select(x => x.PlexLibraryId).ShouldAllBe(x => x % 2 == 0);
    }

    [Test]
    public async Task ShouldBackfillGrantedHistoryEventOnlyOnce_WhenLibraryIsUpdatedAcrossMultipleRefreshes()
    {
        // Arrange
        await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = dbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexLibrary = dbContext.PlexLibraries.AsTracking().Single();
        var updatedTime = DateTime.UtcNow - TimeSpan.FromMinutes(30);
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = new List<PlexLibrary> { plexLibrary }.ToApiLibraries(updatedTime),
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var firstResult = await Sut.ExecuteAsync(request, CancellationToken);
        var secondResult = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        secondResult.IsSuccess.ShouldBeTrue();
        firstResult.Value.Single().GetUpdated.Count.ShouldBe(1);
        firstResult.Value.Single().GetGranted.Count.ShouldBe(0);
        secondResult.Value.Single().GetUpdated.Count.ShouldBe(1);
        secondResult.Value.Single().GetGranted.Count.ShouldBe(0);

        var historyEvents = await dbContext.PlexLibraryAccessHistoryEvents.ToListAsync(CancellationToken);
        historyEvents.Count.ShouldBe(1);

        var grantedEvent = historyEvents.Single();
        grantedEvent.State.ShouldBe(PlexAccessState.Granted);
        grantedEvent.PlexAccountId.ShouldBe(plexAccount.Id);
        grantedEvent.PlexServerId.ShouldBe(plexLibrary.PlexServerId);
        grantedEvent.PlexLibraryId.ShouldBe(plexLibrary.Id);
        grantedEvent.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);
    }

    [Test]
    public async Task ShouldNotBackfillGrantedHistoryEvent_WhenLibraryAlreadyHasAnyHistory()
    {
        // Arrange
        await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = dbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServer = dbContext.PlexServers.Single();
        var plexLibrary = dbContext.PlexLibraries.AsTracking().Single();

        await dbContext.PlexLibraryAccessHistoryEvents.AddAsync(
            new PlexLibraryAccessHistoryEvent
            {
                RefreshRunId = Guid.NewGuid(),
                PlexAccountId = plexAccount.Id,
                PlexAccountNameSnapshot = plexAccount.DisplayName,
                PlexServerId = plexServer.Id,
                PlexServerNameSnapshot = plexServer.Name,
                PlexLibraryId = plexLibrary.Id,
                PlexLibraryNameSnapshot = plexLibrary.Name,
                State = PlexAccessState.Revoked,
                CreatedAt = DateTime.UtcNow - TimeSpan.FromHours(1),
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var updatedTime = DateTime.UtcNow - TimeSpan.FromMinutes(30);
        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = new List<PlexLibrary> { plexLibrary }.ToApiLibraries(updatedTime),
        };

        Mock.SetupCommand(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>).ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var historyEvents = await dbContext.PlexLibraryAccessHistoryEvents.ToListAsync(CancellationToken);
        historyEvents.Count.ShouldBe(1);
        historyEvents.Single().State.ShouldBe(PlexAccessState.Revoked);
    }

    [Test]
    public async Task ShouldDeduplicateIncomingLibrariesByServerAndUuid_WhenPlexReturnsDuplicateRows()
    {
        // Arrange
        var seed = await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexAccount = dbContext.PlexAccounts.FirstOrDefault();
        plexAccount.ShouldNotBeNull();
        var plexServer = dbContext.PlexServers.Single();
        var plexLibrary = FakeData.GetPlexLibrary(seed).Generate();
        plexLibrary.PlexServerId = plexServer.Id;
        plexLibrary.Uuid = "duplicate-library-uuid";
        var duplicateLibrary = FakeData.GetPlexLibrary(seed).Generate();
        duplicateLibrary.PlexServerId = plexServer.Id;
        duplicateLibrary.Uuid = plexLibrary.Uuid;
        duplicateLibrary.Title = "Duplicate payload winner";
        duplicateLibrary.Key = "duplicate-payload-winner";

        var request = new AddOrUpdatePlexLibrariesCommand
        {
            PlexAccountId = plexAccount.Id,
            PlexLibraries = [plexLibrary, duplicateLibrary],
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var plexLibrariesDb = await dbContext.PlexLibraries.ToListAsync(CancellationToken);
        plexLibrariesDb.Count.ShouldBe(1);
        plexLibrariesDb.Single().Uuid.ShouldBe(plexLibrary.Uuid);
        plexLibrariesDb.Single().Title.ShouldBe(duplicateLibrary.Title);
        var plexAccountLibrariesDb = await dbContext.PlexAccountLibraries.ToListAsync(CancellationToken);
        plexAccountLibrariesDb.Count.ShouldBe(1);
        result.Value.Single().GetGranted.Count.ShouldBe(1);
    }

    private sealed record ExpectedLibraryMetrics(
        long MediaSize,
        int MovieCount,
        int TvShowCount,
        int SeasonCount,
        int EpisodeCount,
        int ActorsCount,
        int GenresCount,
        int CountriesCount
    );
}
