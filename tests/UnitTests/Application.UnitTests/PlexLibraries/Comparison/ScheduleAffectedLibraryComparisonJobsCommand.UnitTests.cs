using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class ScheduleAffectedLibraryComparisonJobsCommandUnitTests
    : BaseCommandUnitTest<ScheduleAffectedLibraryComparisonJobsCommand>
{
    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.TvShow, false)]
    [Arguments(PlexMediaType.MusicArtist, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.TvShow, true)]
    [Arguments(PlexMediaType.MusicArtist, true)]
    public async Task ShouldScheduleOnlySameTypeOppositeRolePairs_WhenEitherSideChanges(PlexMediaType type, bool owned)
    {
        // Arrange
        await SetupDatabase(91112, config =>
        {
            config.PlexServerCount = 5;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(5);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, type), CancellationToken);
        await dbContext
            .PlexLibraries.Where(x => x.Id == libraries[3].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, PlexMediaType.OtherVideos), CancellationToken);
        await dbContext.PlexServers.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.OwnedOverride, !owned),
            CancellationToken
        );
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, owned);
        await SetOwnedOverrideAsync(libraries[2].PlexServerId, owned);
        var expectedPayloads = new[] { libraries[1], libraries[4] }
            .Select(target =>
                owned
                    ? new PlexLibraryComparisonJobPayload(libraries[0].Id, target.Id)
                    : new PlexLibraryComparisonJobPayload(target.Id, libraries[0].Id)
            )
            .ToList();
        var expectedKeys = expectedPayloads
            .Select(payload =>
                PlexLibraryComparisonJob.GetJobKey(payload.OwnedPlexLibraryId, payload.RemotePlexLibraryId)
            )
            .ToList();
        IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>? scheduled = null;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(query =>
                        query.Group != null
                        && query.Group.CompareToValue == nameof(JobTypes.LibraryComparisonJob)
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([], false))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.Exists(It.Is<JobKey>(key => expectedKeys.Contains(key)), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.ScheduleJobs(
                    It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                    It.Is<ScheduleJobOptions>(options => !options.Replace),
                    CancellationToken
                )
            )
            .Callback<
                IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>,
                ScheduleJobOptions,
                CancellationToken
            >((jobs, _, _) => scheduled = jobs)
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduled.ShouldNotBeNull();
        scheduled.Keys.Select(x => x.Key).ShouldBe(expectedKeys);
        scheduled
            .Keys.Select(x => x.JobDataMap.GetPayload<PlexLibraryComparisonJobPayload>())
            .ShouldBe(expectedPayloads);
        scheduled.Values.ShouldAllBe(x => x.Count == 1);
        scheduled.Values.SelectMany(x => x).Select(x => x.JobKey).ShouldBe(expectedKeys);
        scheduler.Verify();
    }

    [Test]
    [Arguments(PlexMediaType.OtherVideos)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    public async Task ShouldReturnSuccessWithoutScheduling_WhenLibraryTypeDoesNotSupportComparison(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(91117, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, type), CancellationToken);
        await dbContext.PlexServers.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.OwnedOverride, false),
            CancellationToken
        );
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);
        var ownership = await dbContext
            .PlexLibraries.OrderBy(x => x.Id)
            .SelectOwnership()
            .ToListAsync(CancellationToken);
        ownership.ShouldBe([
            new PlexLibraryOwnership(libraries[0].Id, type, true),
            new PlexLibraryOwnership(libraries[1].Id, type, false),
        ]);

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>().Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>()
            .Verify(
                x =>
                    x.ScheduleJobs(
                        It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                        It.Is<ScheduleJobOptions>(options => !options.Replace),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnFailure_WhenLibraryIsMissingOrDisabled(bool disabled)
    {
        // Arrange
        await SetupDatabase(91118, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var libraryId = library.Id;
        if (disabled)
        {
            var updated = await dbContext
                .PlexLibraries.IgnoreIsEnabledFilter()
                .Where(x => x.Id == libraryId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
            updated.ShouldBe(1);
            (
                await dbContext.PlexLibraries.IgnoreIsEnabledFilter().SingleAsync(CancellationToken)
            ).IsEnabled.ShouldBeFalse();
        }
        else
        {
            libraryId = int.MaxValue;
            (
                await dbContext
                    .PlexLibraries.IgnoreIsEnabledFilter()
                    .AnyAsync(x => x.Id == libraryId, CancellationToken)
            ).ShouldBeFalse();
        }

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraryId));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>().Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>()
            .Verify(
                x =>
                    x.ScheduleJobs(
                        It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                        It.Is<ScheduleJobOptions>(options => !options.Replace),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldSkipCurrentMusicScopesAndScheduleOnlyMissingOrInvalidPairs(
        bool sourceOwned,
        bool sourceOutdated
    )
    {
        // Arrange
        var fixture = await ArrangeMusicScopeFixtureAsync(91115, sourceOwned, sourceOutdated);
        var dbContext = fixture.DbContext;
        var source = fixture.Libraries[0];
        var expectedTargetIndexes = sourceOutdated ? new[] { 1, 2, 3, 4, 5 } : new[] { 2, 3, 4, 5 };
        var expectedPayloads = expectedTargetIndexes
            .Select(index =>
                sourceOwned
                    ? new PlexLibraryComparisonJobPayload(source.Id, fixture.Libraries[index].Id)
                    : new PlexLibraryComparisonJobPayload(fixture.Libraries[index].Id, source.Id)
            )
            .ToList();
        var expectedKeys = expectedPayloads
            .Select(x => PlexLibraryComparisonJob.GetJobKey(x.OwnedPlexLibraryId, x.RemotePlexLibraryId))
            .ToList();
        IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>? scheduled = null;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(query =>
                        query.Group != null
                        && query.Group.CompareToValue == nameof(JobTypes.LibraryComparisonJob)
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([], false))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.Exists(It.Is<JobKey>(key => expectedKeys.Contains(key)), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Exactly(expectedKeys.Count));
        scheduler
            .Setup(x =>
                x.ScheduleJobs(
                    It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                    It.Is<ScheduleJobOptions>(options => !options.Replace),
                    CancellationToken
                )
            )
            .Callback<
                IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>,
                ScheduleJobOptions,
                CancellationToken
            >((jobs, _, _) => scheduled = jobs)
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        var beforeScheduling = DateTimeOffset.UtcNow;

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(source.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduled.ShouldNotBeNull();
        scheduled.Keys.Select(x => x.Key).ShouldBe(expectedKeys);
        scheduled
            .Keys.Select(x => x.JobDataMap.GetPayload<PlexLibraryComparisonJobPayload>())
            .ShouldBe(expectedPayloads);
        var triggers = scheduled.Values.SelectMany(x => x).ToList();
        triggers.Select(x => (x.Key.Name, x.Key.Group)).ShouldBe(expectedKeys.Select(x => (x.Name, x.Group)));
        triggers.Select(x => x.JobKey).ShouldBe(expectedKeys);
        triggers.ShouldAllBe(x => ((ISimpleTrigger)x).RepeatCount == 0);
        triggers.ShouldAllBe(x => ((ISimpleTrigger)x).MisfireInstruction == SimpleTriggerMisfireInstruction.FireNow);
        triggers.ShouldAllBe(x => x.StartTimeUtc >= beforeScheduling.AddMinutes(1));
        triggers.ShouldAllBe(x => x.StartTimeUtc <= DateTimeOffset.UtcNow.AddMinutes(3));
        scheduled.Values.ShouldAllBe(x => x.Count == 1);
        var after = await ReadMusicStateAsync(dbContext);
        after
            .Scopes.Select(x => (x.Id, x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.MediaType, x.CompletedAt))
            .ShouldBe(
                fixture.Scopes.Select(x =>
                    (x.Id, x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.MediaType, x.CompletedAt)
                )
            );
        after
            .Hits.Select(x =>
                (
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.RemotePlexMediaId,
                    x.OwnedPlexMediaId,
                    x.MatchType,
                    x.ComparedAt
                )
            )
            .ShouldBe(
                fixture.Hits.Select(x =>
                    (
                        x.Id,
                        x.RemotePlexLibraryId,
                        x.OwnedPlexLibraryId,
                        x.RemotePlexMediaId,
                        x.OwnedPlexMediaId,
                        x.MatchType,
                        x.ComparedAt
                    )
                )
            );
        (await dbContext.PlexMusicArtists.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldBe(
            fixture.ArtistIds
        );
        scheduler.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldNotSchedule_WhenEveryMusicPairHasCurrentScope(bool sourceOwned)
    {
        // Arrange
        var fixture = await ArrangeMusicLibrariesAsync(91116, sourceOwned, false, 4);
        var dbContext = fixture.DbContext;
        var libraries = fixture.Libraries;
        var source = libraries[0];
        foreach (var target in libraries.Skip(1))
            dbContext.PlexComparisonScopes.Add(
                CreateComparisonScope(
                    sourceOwned ? target : source,
                    sourceOwned ? source : target,
                    PlexMediaType.MusicArtist,
                    fixture.CompletedAt
                )
            );
        var artists = await dbContext.PlexMusicArtists.ToListAsync(CancellationToken);
        var pair = sourceOwned ? (Remote: libraries[1], Owned: source) : (Remote: source, Owned: libraries[1]);
        dbContext.PlexMusicArtistComparisons.Add(
            CreateArtistComparison(
                artists.Single(x => x.PlexLibraryId == pair.Remote.Id),
                artists.Single(x => x.PlexLibraryId == pair.Owned.Id),
                fixture.CompletedAt
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = await ReadMusicStateAsync(dbContext);
        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        var listeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            commandSubscriptions.Add(
                listener
                    .Select(entry => entry.Value!)
                    .OfType<CommandExecutedEventData>()
                    .Where(command => command.Context?.Database.GetConnectionString() == connectionString)
                    .Subscribe(command => commands.Enqueue(command.Command.CommandText))
            )
        );
        using var subscriptions = new System.Reactive.Disposables.CompositeDisposable(listeners, commandSubscriptions);

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(source.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        commands.Count(x => x.Contains("PlexComparisonScopes", StringComparison.Ordinal)).ShouldBe(1);
        var after = await ReadMusicStateAsync(dbContext);
        after
            .Scopes.Select(x => (x.Id, x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.MediaType, x.CompletedAt))
            .ShouldBe(
                before.Scopes.Select(x =>
                    (x.Id, x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.MediaType, x.CompletedAt)
                )
            );
        after
            .Hits.Select(x =>
                (
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.RemotePlexMediaId,
                    x.OwnedPlexMediaId,
                    x.MatchType,
                    x.ComparedAt
                )
            )
            .ShouldBe(
                before.Hits.Select(x =>
                    (
                        x.Id,
                        x.RemotePlexLibraryId,
                        x.OwnedPlexLibraryId,
                        x.RemotePlexMediaId,
                        x.OwnedPlexMediaId,
                        x.MatchType,
                        x.ComparedAt
                    )
                )
            );
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>().Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>()
            .Verify(
                x =>
                    x.ScheduleJobs(
                        It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                        It.Is<ScheduleJobOptions>(options => !options.Replace),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.MusicArtist)]
    public async Task ShouldNotScheduleDuplicatePair_WhenJobAlreadyExists(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(91113, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, type), CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        var key = PlexLibraryComparisonJob.GetJobKey(libraries[1].Id, libraries[0].Id);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(query =>
                        query.Group != null
                        && query.Group.CompareToValue == nameof(JobTypes.LibraryComparisonJob)
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                new PagedResult<JobHeader>(
                    [new JobHeader(key, null, typeof(PlexLibraryComparisonJob).FullName!, false, true, false, true)],
                    false
                )
            )
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify();
        scheduler.Verify(
            x =>
                x.ScheduleJobs(
                    It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                    It.Is<ScheduleJobOptions>(options => !options.Replace),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never()
        );
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    public async Task ShouldNotSchedule_WhenNoEligiblePairExists(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(91114, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, type), CancellationToken);
        await dbContext.PlexServers.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.OwnedOverride, false),
            CancellationToken
        );

        // Act
        var result = await TestHandlerExecuteAsync(new ScheduleAffectedLibraryComparisonJobsCommand(libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>()
            .Verify(
                x =>
                    x.ScheduleJobs(
                        It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                        It.Is<ScheduleJobOptions>(options => !options.Replace),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    private async Task<MusicScopeFixture> ArrangeMusicScopeFixtureAsync(int seed, bool sourceOwned, bool sourceOutdated)
    {
        var fixture = await ArrangeMusicLibrariesAsync(seed, sourceOwned, sourceOutdated, 6);
        var dbContext = fixture.DbContext;
        var source = fixture.Libraries[0];
        var pairs = fixture
            .Libraries.Skip(1)
            .Select(target => sourceOwned ? (Remote: target, Owned: source) : (Remote: source, Owned: target))
            .ToArray();
        dbContext.PlexComparisonScopes.Add(
            CreateComparisonScope(pairs[0].Remote, pairs[0].Owned, PlexMediaType.MusicArtist, fixture.CompletedAt)
        );
        var artists = await dbContext.PlexMusicArtists.ToListAsync(CancellationToken);
        dbContext.PlexMusicArtistComparisons.Add(
            CreateArtistComparison(
                artists.Single(x => x.PlexLibraryId == pairs[0].Remote.Id),
                artists.Single(x => x.PlexLibraryId == pairs[0].Owned.Id),
                fixture.CompletedAt
            )
        );
        var outdatedTarget = fixture.Libraries[3];
        await dbContext
            .PlexLibraries.Where(x => x.Id == outdatedTarget.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        dbContext.PlexComparisonScopes.Add(
            CreateComparisonScope(pairs[2].Remote, pairs[2].Owned, PlexMediaType.MusicArtist, fixture.CompletedAt)
        );
        dbContext.PlexComparisonScopes.Add(
            CreateComparisonScope(pairs[3].Remote, pairs[3].Owned, PlexMediaType.Movie, fixture.CompletedAt)
        );
        dbContext.PlexComparisonScopes.Add(
            CreateComparisonScope(pairs[4].Owned, pairs[4].Remote, PlexMediaType.MusicArtist, fixture.CompletedAt)
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var state = await ReadMusicStateAsync(dbContext);
        var artistIds = artists.Select(x => x.Id).Order().ToList();
        return new MusicScopeFixture(dbContext, fixture.Libraries, state.Scopes, state.Hits, artistIds);
    }

    private async Task<(List<MusicScopeRow> Scopes, List<MusicHitRow> Hits)> ReadMusicStateAsync(
        IReaparrDbContext dbContext
    ) =>
        (
            await dbContext
                .PlexComparisonScopes.OrderBy(x => x.Id)
                .Select(x => new MusicScopeRow(
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.MediaType,
                    x.CompletedAt
                ))
                .ToListAsync(CancellationToken),
            await dbContext
                .PlexMusicArtistComparisons.OrderBy(x => x.Id)
                .Select(x => new MusicHitRow(
                    x.Id,
                    x.RemotePlexLibraryId,
                    x.OwnedPlexLibraryId,
                    x.RemotePlexMediaId,
                    x.OwnedPlexMediaId,
                    x.MatchType,
                    x.ComparedAt
                ))
                .ToListAsync(CancellationToken)
        );

    private sealed record MusicScopeFixture(
        IReaparrDbContext DbContext,
        PlexLibrary[] Libraries,
        List<MusicScopeRow> Scopes,
        List<MusicHitRow> Hits,
        List<int> ArtistIds
    );

    private sealed record MusicScopeRow(
        int Id,
        int RemotePlexLibraryId,
        int OwnedPlexLibraryId,
        PlexMediaType MediaType,
        DateTime CompletedAt
    );

    private sealed record MusicHitRow(
        int Id,
        int RemotePlexLibraryId,
        int OwnedPlexLibraryId,
        int RemotePlexMediaId,
        int OwnedPlexMediaId,
        PlexMediaComparisonMatchType MatchType,
        DateTime ComparedAt
    );

    private async Task<(
        IReaparrDbContext DbContext,
        PlexLibrary[] Libraries,
        DateTime CompletedAt
    )> ArrangeMusicLibrariesAsync(int seed, bool sourceOwned, bool sourceOutdated, int libraryCount)
    {
        await SetupDatabase(seed, config =>
        {
            config.PlexServerCount = libraryCount;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var completedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var libraries = (await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken)).ToArray();
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.Outdated, false)
                    .SetProperty(y => y.SyncedAt, completedAt)
                    .SetProperty(y => y.SyncedContentChangedAt, y => y.ContentChangedAt),
            CancellationToken
        );
        await dbContext.PlexServers.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.OwnedOverride, !sourceOwned),
            CancellationToken
        );
        await dbContext
            .PlexServers.Where(x => x.Id == libraries[0].PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, sourceOwned), CancellationToken);
        if (sourceOutdated)
            await dbContext
                .PlexLibraries.Where(x => x.Id == libraries[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        return (dbContext, libraries, completedAt);
    }

    private static PlexComparisonState CreateComparisonScope(
        PlexLibrary remote,
        PlexLibrary owned,
        PlexMediaType mediaType,
        DateTime completedAt
    ) =>
        new()
        {
            RemotePlexLibraryId = remote.Id,
            OwnedPlexLibraryId = owned.Id,
            MediaType = mediaType,
            CompletedAt = completedAt,
        };

    private static PlexMusicArtistComparison CreateArtistComparison(
        PlexMusicArtist remote,
        PlexMusicArtist owned,
        DateTime comparedAt
    ) =>
        new()
        {
            RemotePlexLibraryId = remote.PlexLibraryId,
            OwnedPlexLibraryId = owned.PlexLibraryId,
            RemotePlexMediaId = remote.Id,
            OwnedPlexMediaId = owned.Id,
            MatchType = PlexMediaComparisonMatchType.MusicBrainzArtistId,
            ComparedAt = comparedAt,
        };
}
