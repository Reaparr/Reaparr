using Quartz;
using Quartz.Impl.Matchers;

namespace Reaparr.Application.UnitTests;

public class RefreshPlexMusicLibraryComparisonReuseCommandUnitTests
    : BaseCommandUnitTest<RefreshPlexMusicLibraryCommand>
{
    [Test]
    public async Task ShouldPreserveMusicComparisonHitsAndScope_WhenSyncReportsNoChanges()
    {
        // Arrange
        var fixture = await SetupMusicComparisonFixtureAsync(625631);
        var dbContext = fixture.DbContext;
        await SeedMusicComparisonRowsAsync(dbContext, fixture.Remote, fixture.Target, fixture.Control);
        var before = await CaptureMusicComparisonSnapshotAsync(dbContext);
        var response = await CreateRefreshResponseAsync(dbContext, fixture.Target);
        ConfigureComparisonInvalidation(fixture, dbContext, shouldInvalidate: false);
        ConfigureRefreshPipeline(fixture, response, Result.Ok(new CrudMusicReport()), force: false);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.Id, result.Value.Type).ShouldBe((fixture.Target.Id, PlexMediaType.MusicArtist));
        var after = await CaptureMusicComparisonSnapshotAsync(dbContext);
        after.Artists.ShouldBe(before.Artists);
        after.Albums.ShouldBe(before.Albums);
        after.Tracks.ShouldBe(before.Tracks);
        after.Scopes.ShouldBe(before.Scopes);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldInvalidateTargetMusicComparisonRows_WhenChangesOrForceRequested(bool force)
    {
        // Arrange
        var fixture = await SetupMusicComparisonFixtureAsync(force ? 625633 : 625632);
        var dbContext = fixture.DbContext;
        await SeedMusicComparisonRowsAsync(dbContext, fixture.Remote, fixture.Target, fixture.Control);
        var controlBefore = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Control.Id);
        var response = await CreateRefreshResponseAsync(dbContext, fixture.Target);
        var report = force ? new CrudMusicReport() : new CrudMusicReport { UpdatedArtists = 1 };
        ConfigureComparisonInvalidation(fixture, dbContext, shouldInvalidate: true);
        ConfigureRefreshPipeline(fixture, response, Result.Ok(report), force);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response, force));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var targetAfter = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Target.Id);
        targetAfter.Artists.ShouldBeEmpty();
        targetAfter.Albums.ShouldBeEmpty();
        targetAfter.Tracks.ShouldBeEmpty();
        targetAfter.Scopes.ShouldBeEmpty();
        var controlAfter = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Control.Id);
        controlAfter.Artists.ShouldBe(controlBefore.Artists);
        controlAfter.Albums.ShouldBe(controlBefore.Albums);
        controlAfter.Tracks.ShouldBe(controlBefore.Tracks);
        controlAfter.Scopes.ShouldBe(controlBefore.Scopes);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreserveMusicComparisonState_WhenRefreshSyncFailsOrCancels(bool cancelled)
    {
        // Arrange
        var fixture = await SetupMusicComparisonFixtureAsync(cancelled ? 625635 : 625634);
        var dbContext = fixture.DbContext;
        await SeedMusicComparisonRowsAsync(dbContext, fixture.Remote, fixture.Target, fixture.Control);
        var before = await CaptureMusicComparisonSnapshotAsync(dbContext);
        var response = await CreateRefreshResponseAsync(dbContext, fixture.Target);
        var syncResult = cancelled
            ? ResultExtensions.TaskIsCancelled("music sync").ToResult<CrudMusicReport>()
            : Result.Fail("music sync failed").ToResult<CrudMusicReport>();
        ConfigureComparisonInvalidation(fixture, dbContext, shouldInvalidate: false);
        ConfigureRefreshPipeline(fixture, response, syncResult, force: false);
        if (!cancelled)
        {
            Mock.Mock<ILibrarySyncProgressStore>()
                .Setup(x =>
                    x.UpdateErrorAsync(fixture.Target.Id, It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1))
                )
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once());
        }

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.Count.ShouldBe(1);
        var after = await CaptureMusicComparisonSnapshotAsync(dbContext);
        after.Artists.ShouldBe(before.Artists);
        after.Albums.ShouldBe(before.Albums);
        after.Tracks.ShouldBe(before.Tracks);
        after.Scopes.ShouldBe(before.Scopes);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    public async Task ShouldInvalidatePersistedMusicComparisonOnZeroChangeRetry_WhenInitialInvalidationFails()
    {
        // Arrange
        var fixture = await SetupMusicComparisonFixtureAsync(625636);
        var dbContext = fixture.DbContext;
        await SeedMusicComparisonRowsAsync(dbContext, fixture.Remote, fixture.Target, fixture.Control);
        var targetBefore = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Target.Id);
        var controlBefore = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Control.Id);
        var firstLibrary = await dbContext.PlexLibraries.AsNoTracking().SingleAsync(x => x.Id == fixture.Target.Id, CancellationToken);
        var firstResponse = await CreateRefreshResponseAsync(dbContext, firstLibrary);
        var changedArtist = firstResponse.PlexLibrary.Music.Single();
        var changedArtistUpdatedAt = changedArtist.UpdatedAt.GetValueOrDefault().AddSeconds(1);
        changedArtist.Title += " changed";
        changedArtist.UpdatedAt = changedArtistUpdatedAt;
        var refreshResponses = new Queue<InsertMediaMetaDataCommandResponse>();
        refreshResponses.Enqueue(firstResponse);

        var executor = Mock.Mock<ICommandExecutor>();
        executor
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary.Id == fixture.Target.Id && c.MediaType == PlexMediaType.MusicAlbum
                    ),
                    CancellationToken
                )
            )
            .Returns((ICommand<Result<LibraryMetadata>> command, CancellationToken _) =>
                Task.FromResult(Result.Ok(new LibraryMetadata(((GetLibraryMediaFromPlexApiCommand)command).PlexLibrary)))
            )
            .Verifiable(Times.Exactly(2));
        executor
            .Setup(x =>
                x.Send(
                    It.Is<InsertMediaMetaDataCommand>(c => c.LibraryMetadata.Library.Id == fixture.Target.Id),
                    CancellationToken
                )
            )
            .Returns(() =>
                Task.FromResult<Result<InsertMediaMetaDataCommandResponse>>(
                    Result.Ok(refreshResponses.Dequeue())
                )
            )
            .Verifiable(Times.Exactly(2));
        executor
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary.Id == fixture.Target.Id && c.MediaType == PlexMediaType.MusicTrack
                    ),
                    CancellationToken
                )
            )
            .Returns((ICommand<Result<LibraryMetadata>> command, CancellationToken _) =>
                Task.FromResult(Result.Ok(new LibraryMetadata(((GetLibraryMediaFromPlexApiCommand)command).PlexLibrary)))
            )
            .Verifiable(Times.Exactly(2));
        var syncValidator = new SyncPlexMusicCommandValidator();
        executor
            .Setup(x =>
                x.Send(
                    It.Is<SyncPlexMusicCommand>(c => c.LibraryMetadata.PlexLibraryId == fixture.Target.Id),
                    CancellationToken
                )
            )
            .Returns(
                (ICommand<Result<CrudMusicReport>> command, CancellationToken ct) =>
                {
                    var sync = (SyncPlexMusicCommand)command;
                    var syncHandler = Mock.Create<SyncPlexMusicCommandHandler>();
                    return new ValidationPipeline<SyncPlexMusicCommand, Result<CrudMusicReport>>([syncValidator]).ExecuteAsync(
                        sync,
                        () => syncHandler.ExecuteAsync(sync, ct),
                        ct
                    );
                }
            )
            .Verifiable(Times.Exactly(2));

        var optimizationChangeCounts = new List<int>();
        executor
            .Setup(x =>
                x.Send(It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>())
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (command, _) => optimizationChangeCounts.Add(((ScheduleOptimizeDatabaseJobCommand)command).ChangedItemCount)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));
        executor
            .Setup(x =>
                x.Send(
                    It.Is<QueueMediaOverviewRebuildCommand>(c => c.MediaType == PlexMediaType.MusicArtist),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        var progressStore = Mock.Mock<ILibrarySyncProgressStore>();
        progressStore
            .Setup(x =>
                x.UpdateErrorAsync(fixture.Target.Id, It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1))
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        progressStore
            .Setup(x =>
                x.UpdateItemAsync(
                    fixture.Target.Id,
                    It.Is<LibraryProgressItem>(p => p.IsComplete && p.TimeRemaining == TimeSpan.Zero)
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .SetupSequence(
                x =>
                    x.GetJobKeys(
                        GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibraryComparisonJob)),
                        CancellationToken
                    )
            )
            .ThrowsAsync(new InvalidOperationException("comparison scheduler unavailable"))
            .ReturnsAsync([])
            .ReturnsAsync([]);
        scheduler
            .Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken))
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        executor
            .Setup(x =>
                x.Send(
                    It.Is<InvalidateLibraryComparisonJobsCommand>(c =>
                        c.PlexLibraryIds.SequenceEqual(new[] { fixture.Target.Id })
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (ICommand<Result> command, CancellationToken ct) =>
                    new InvalidateLibraryComparisonJobsCommandHandler(
                        Mock.Container.Resolve<ILogger>(),
                        dbContext,
                        scheduler.Object
                    ).ExecuteAsync((InvalidateLibraryComparisonJobsCommand)command, ct)
            )
            .Verifiable(Times.Exactly(2));

        // Act
        var firstResult = await TestHandlerExecuteAsync<PlexLibrary>(
            new RefreshPlexMusicLibraryCommand(firstResponse)
        );

        // Assert
        firstResult.IsFailed.ShouldBeTrue();
        firstResult.IsCancelled.ShouldBeFalse();
        firstResult.Errors.Count.ShouldBe(1);
        var persistedArtistAfterFailure = await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == fixture.Target.Id)
            .Select(x => new { x.Title, x.UpdatedAt })
            .SingleAsync(CancellationToken);
        (persistedArtistAfterFailure.Title, persistedArtistAfterFailure.UpdatedAt).ShouldBe(
            (changedArtist.Title, (DateTime?)changedArtistUpdatedAt)
        );
        var libraryAfterFailure = await dbContext
            .PlexLibraries.Where(x => x.Id == fixture.Target.Id)
            .Select(x => new { x.Outdated, x.ContentChangedAt, x.SyncedContentChangedAt })
            .SingleAsync(CancellationToken);
        (
            libraryAfterFailure.Outdated,
            libraryAfterFailure.ContentChangedAt,
            libraryAfterFailure.SyncedContentChangedAt
        ).ShouldBe((true, 1, (long?)1));
        var targetAfterFailure = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Target.Id);
        targetAfterFailure.Artists.ShouldBe(targetBefore.Artists);
        targetAfterFailure.Albums.ShouldBe(targetBefore.Albums);
        targetAfterFailure.Tracks.ShouldBe(targetBefore.Tracks);
        targetAfterFailure.Scopes.ShouldBe(targetBefore.Scopes);
        var controlAfterFailure = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Control.Id);
        controlAfterFailure.Artists.ShouldBe(controlBefore.Artists);
        controlAfterFailure.Albums.ShouldBe(controlBefore.Albums);
        controlAfterFailure.Tracks.ShouldBe(controlBefore.Tracks);
        controlAfterFailure.Scopes.ShouldBe(controlBefore.Scopes);

        var retryLibrary = await dbContext.PlexLibraries.AsNoTracking().SingleAsync(x => x.Id == fixture.Target.Id, CancellationToken);
        var retryResponse = await CreateRefreshResponseAsync(dbContext, retryLibrary);
        refreshResponses.Enqueue(retryResponse);

        var secondResult = await TestHandlerExecuteAsync<PlexLibrary>(
            new RefreshPlexMusicLibraryCommand(retryResponse)
        );

        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Errors.Count.ShouldBe(0);
        optimizationChangeCounts.ShouldBe([1, 0]);
        var targetAfterRetry = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Target.Id);
        targetAfterRetry.Artists.ShouldBeEmpty();
        targetAfterRetry.Albums.ShouldBeEmpty();
        targetAfterRetry.Tracks.ShouldBeEmpty();
        targetAfterRetry.Scopes.ShouldBeEmpty();
        var controlAfterRetry = await CaptureMusicComparisonSnapshotAsync(dbContext, fixture.Control.Id);
        controlAfterRetry.Artists.ShouldBe(controlBefore.Artists);
        controlAfterRetry.Albums.ShouldBe(controlBefore.Albums);
        controlAfterRetry.Tracks.ShouldBe(controlBefore.Tracks);
        controlAfterRetry.Scopes.ShouldBe(controlBefore.Scopes);

        await dbContext
            .PlexLibraries.Where(x => x.Id == fixture.Target.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, false), CancellationToken);
        IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>? scheduledJobs = null;
        var expectedJobKey = PlexLibraryComparisonJob.GetJobKey(fixture.Target.Id, fixture.Remote.Id);
        scheduler
            .Setup(x => x.CheckExists(It.Is<JobKey>(key => key.Equals(expectedJobKey)), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.ScheduleJobs(
                    It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                    false,
                    CancellationToken
                )
            )
            .Callback<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>, bool, CancellationToken>(
                (jobs, _, _) => scheduledJobs = jobs
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        var scheduleResult = await Mock.Create<ScheduleAffectedLibraryComparisonJobsCommandHandler>().ExecuteAsync(
            new ScheduleAffectedLibraryComparisonJobsCommand(fixture.Target.Id),
            CancellationToken
        );

        scheduleResult.IsSuccess.ShouldBeTrue();
        scheduleResult.Errors.Count.ShouldBe(0);
        scheduledJobs.ShouldNotBeNull();
        scheduledJobs.Keys.Select(x => x.Key).ShouldBe([expectedJobKey]);
        scheduledJobs
            .Keys.Select(x => x.JobDataMap.GetPayload<PlexLibraryComparisonJobPayload>())
            .ShouldBe([new PlexLibraryComparisonJobPayload(fixture.Target.Id, fixture.Remote.Id)]);
        executor.Verify();
        progressStore.Verify();
        scheduler.Verify();
    }

    private void ConfigureComparisonInvalidation(
        (IReaparrDbContext DbContext, PlexLibrary Remote, PlexLibrary Target, PlexLibrary Control) fixture,
        IReaparrDbContext dbContext,
        bool shouldInvalidate
    )
    {
        var scheduler = Mock.Mock<IScheduler>();
        var times = shouldInvalidate ? Times.Once() : Times.Never();
        scheduler
            .Setup(x =>
                x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibraryComparisonJob)), CancellationToken)
            )
            .ReturnsAsync([])
            .Verifiable(times);
        scheduler.Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken)).ReturnsAsync([]).Verifiable(times);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<InvalidateLibraryComparisonJobsCommand>(c =>
                        c.PlexLibraryIds.SequenceEqual(new[] { fixture.Target.Id })
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (ICommand<Result> command, CancellationToken ct) =>
                    new InvalidateLibraryComparisonJobsCommandHandler(
                        Mock.Container.Resolve<ILogger>(),
                        dbContext,
                        scheduler.Object
                    ).ExecuteAsync((InvalidateLibraryComparisonJobsCommand)command, ct)
            )
            .Verifiable(times);
    }

    private void ConfigureRefreshPipeline(
        (IReaparrDbContext DbContext, PlexLibrary Remote, PlexLibrary Target, PlexLibrary Control) fixture,
        InsertMediaMetaDataCommandResponse response,
        Result<CrudMusicReport> syncResult,
        bool force
    )
    {
        var executor = Mock.Mock<ICommandExecutor>();
        executor
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == fixture.Target && c.MediaType == PlexMediaType.MusicAlbum
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(fixture.Target)))
            .Verifiable(Times.Once());
        executor
            .Setup(x =>
                x.Send(
                    It.Is<InsertMediaMetaDataCommand>(c => c.LibraryMetadata.Library == fixture.Target),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(response))
            .Verifiable(Times.Once());
        executor
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == fixture.Target && c.MediaType == PlexMediaType.MusicTrack
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(fixture.Target)))
            .Verifiable(Times.Once());
        executor
            .Setup(x =>
                x.Send(
                    It.Is<SyncPlexMusicCommand>(c => c.LibraryMetadata == response && c.ForceMediaRefresh == force),
                    CancellationToken
                )
            )
            .ReturnsAsync(syncResult)
            .Verifiable(Times.Once());
        ConfigureRefreshMaintenance(fixture.Target, syncResult);
    }

    private void ConfigureRefreshMaintenance(PlexLibrary library, Result<CrudMusicReport> syncResult)
    {
        var maintenanceTimes = syncResult.IsSuccess ? Times.Once() : Times.Never();
        var expectedChanges = syncResult.IsSuccess ? syncResult.Value.ChangedItemCount : 0;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ScheduleOptimizeDatabaseJobCommand>(c => c.ChangedItemCount == expectedChanges),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(maintenanceTimes);
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand(PlexMediaType.MusicArtist))
            .ReturnsAsync(Result.Ok())
            .Verifiable(maintenanceTimes);
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(
                    library.Id,
                    It.Is<LibraryProgressItem>(p => p.Received == 1 && p.Total == 1 && p.TimeRemaining == TimeSpan.Zero)
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(syncResult.IsSuccess ? Times.Exactly(3) : Times.Never());
    }

    private async Task<(
        IReaparrDbContext DbContext,
        PlexLibrary Remote,
        PlexLibrary Target,
        PlexLibrary Control
    )> SetupMusicComparisonFixtureAsync(int seed)
    {
        await SetupDatabase(
            seed,
            x =>
            {
                x.PlexServerCount = 3;
                x.PlexMusicLibraryCount = 1;
                x.MusicArtistCount = 1;
                x.MusicAlbumCount = 1;
                x.MusicTrackCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var target = libraries[1];
        var control = libraries[2];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(target.PlexServerId, true);
        await SetOwnedOverrideAsync(control.PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.Outdated, false)
                    .SetProperty(y => y.IsEnabled, true)
                    .SetProperty(y => y.ContentChangedAt, 1)
                    .SetProperty(y => y.SyncedContentChangedAt, (long?)1)
                    .SetProperty(y => y.SyncedAt, DateTime.UtcNow),
            CancellationToken
        );
        return (dbContext, remote, target, control);
    }

    private async Task SeedMusicComparisonRowsAsync(
        IReaparrDbContext dbContext,
        PlexLibrary remote,
        PlexLibrary target,
        PlexLibrary control
    )
    {
        var artists = await dbContext.PlexMusicArtists.ToListAsync(CancellationToken);
        var albums = await dbContext.PlexMusicAlbums.ToListAsync(CancellationToken);
        var tracks = await dbContext.PlexMusicTracks.ToListAsync(CancellationToken);
        var pairs = new[]
        {
            (Owned: target, CompletedAt: new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
            (Owned: control, CompletedAt: new DateTime(2026, 1, 3, 4, 5, 6, DateTimeKind.Utc)),
        };
        foreach (var (owned, completedAt) in pairs)
        {
            dbContext.PlexComparisonScopes.Add(
                new PlexComparisonState
                {
                    RemotePlexLibraryId = remote.Id,
                    OwnedPlexLibraryId = owned.Id,
                    MediaType = PlexMediaType.MusicArtist,
                    CompletedAt = completedAt,
                }
            );
            dbContext.PlexMusicArtistComparisons.Add(
                new PlexMusicArtistComparison
                {
                    RemotePlexLibraryId = remote.Id,
                    OwnedPlexLibraryId = owned.Id,
                    RemotePlexMediaId = artists.Single(x => x.PlexLibraryId == remote.Id).Id,
                    OwnedPlexMediaId = artists.Single(x => x.PlexLibraryId == owned.Id).Id,
                    MatchType = PlexMediaComparisonMatchType.PlexArtistGuid,
                    ComparedAt = completedAt,
                }
            );
            dbContext.PlexMusicAlbumComparisons.Add(
                new PlexMusicAlbumComparison
                {
                    RemotePlexLibraryId = remote.Id,
                    OwnedPlexLibraryId = owned.Id,
                    RemotePlexMediaId = albums.Single(x => x.PlexLibraryId == remote.Id).Id,
                    OwnedPlexMediaId = albums.Single(x => x.PlexLibraryId == owned.Id).Id,
                    MatchType = PlexMediaComparisonMatchType.PlexAlbumGuid,
                    ComparedAt = completedAt,
                }
            );
            dbContext.PlexMusicTrackComparisons.Add(
                new PlexMusicTrackComparison
                {
                    RemotePlexLibraryId = remote.Id,
                    OwnedPlexLibraryId = owned.Id,
                    RemotePlexMediaId = tracks.Single(x => x.PlexLibraryId == remote.Id).Id,
                    OwnedPlexMediaId = tracks.Single(x => x.PlexLibraryId == owned.Id).Id,
                    MatchType = PlexMediaComparisonMatchType.PlexTrackGuid,
                    ComparedAt = completedAt,
                }
            );
        }
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private async Task<InsertMediaMetaDataCommandResponse> CreateRefreshResponseAsync(
        IReaparrDbContext dbContext,
        PlexLibrary library
    )
    {
        var artists = await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == library.Id)
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var albums = artists.SelectMany(x => x.Albums).ToList();
        library.Music.Clear();
        library.Music.AddRange(artists);
        library.Albums.Clear();
        library.Albums.AddRange(albums);
        library.Tracks.Clear();
        library.Tracks.AddRange(albums.SelectMany(x => x.Tracks));
        return new InsertMediaMetaDataCommandResponse(library);
    }

    private async Task<MusicComparisonSnapshot> CaptureMusicComparisonSnapshotAsync(
        IReaparrDbContext dbContext,
        int? ownedLibraryId = null
    ) =>
        new(
            await dbContext
                .PlexMusicArtistComparisons.Where(x => ownedLibraryId == null || x.OwnedPlexLibraryId == ownedLibraryId)
                .OrderBy(x => x.Id)
                .Select(x => new ValueTuple<int, int, int, int, int, DateTime>(
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.RemotePlexMediaId,
                    x.OwnedPlexMediaId,
                    x.ComparedAt
                ))
                .ToListAsync(CancellationToken),
            await dbContext
                .PlexMusicAlbumComparisons.Where(x => ownedLibraryId == null || x.OwnedPlexLibraryId == ownedLibraryId)
                .OrderBy(x => x.Id)
                .Select(x => new ValueTuple<int, int, int, int, int, DateTime>(
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.RemotePlexMediaId,
                    x.OwnedPlexMediaId,
                    x.ComparedAt
                ))
                .ToListAsync(CancellationToken),
            await dbContext
                .PlexMusicTrackComparisons.Where(x => ownedLibraryId == null || x.OwnedPlexLibraryId == ownedLibraryId)
                .OrderBy(x => x.Id)
                .Select(x => new ValueTuple<int, int, int, int, int, DateTime>(
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.RemotePlexMediaId,
                    x.OwnedPlexMediaId,
                    x.ComparedAt
                ))
                .ToListAsync(CancellationToken),
            await dbContext
                .PlexComparisonScopes.Where(x => ownedLibraryId == null || x.OwnedPlexLibraryId == ownedLibraryId)
                .OrderBy(x => x.Id)
                .Select(x => new ValueTuple<int, int, int, PlexMediaType, DateTime>(
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.MediaType,
                    x.CompletedAt
                ))
                .ToListAsync(CancellationToken)
        );

    private sealed record MusicComparisonSnapshot(
        List<(
            int Id,
            int RemotePlexLibraryId,
            int OwnedPlexLibraryId,
            int RemotePlexMediaId,
            int OwnedPlexMediaId,
            DateTime ComparedAt
        )> Artists,
        List<(
            int Id,
            int RemotePlexLibraryId,
            int OwnedPlexLibraryId,
            int RemotePlexMediaId,
            int OwnedPlexMediaId,
            DateTime ComparedAt
        )> Albums,
        List<(
            int Id,
            int RemotePlexLibraryId,
            int OwnedPlexLibraryId,
            int RemotePlexMediaId,
            int OwnedPlexMediaId,
            DateTime ComparedAt
        )> Tracks,
        List<(
            int Id,
            int RemotePlexLibraryId,
            int OwnedPlexLibraryId,
            PlexMediaType MediaType,
            DateTime CompletedAt
        )> Scopes
    );
}
