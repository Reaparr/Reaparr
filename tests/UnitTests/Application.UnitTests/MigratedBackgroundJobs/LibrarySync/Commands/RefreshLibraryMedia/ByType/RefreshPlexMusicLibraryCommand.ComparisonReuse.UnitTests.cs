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
