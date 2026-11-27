using Quartz;

namespace Reaparr.Application.UnitTests;

public class ApplyRemoteMusicComparisonStateCommandUnitTests
    : BaseCommandUnitTest<ApplyRemoteMusicComparisonStateCommand>
{
    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(true, false)]
    public async Task ShouldAggregateDistinctCoverageIncludingEmptyAlbums_WhenMultipleOwnedLibrariesMatch(
        bool coverEmptyAlbum,
        bool coverAllTracks
    )
    {
        // Arrange
        await SetupDatabase(87301, config =>
        {
            config.PlexServerCount = 3;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 2;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 3;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await SetOwnedOverrideAsync(libraries[2].PlexServerId, true);
        var artists = await dbContext.PlexMusicArtists.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = artists.Where(x => x.PlexLibraryId == libraries[0].Id).ToList();
        var albums = await dbContext.PlexMusicAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteAlbums = albums.Where(x => x.PlexArtistId == remote[0].Id).ToList();
        var tracks = await dbContext.PlexMusicTracks.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteTracks = tracks.Where(x => x.PlexAlbumId == remoteAlbums[0].Id).ToList();
        remote.Count.ShouldBe(2);
        remoteAlbums.Count.ShouldBe(2);
        remoteTracks.Count.ShouldBe(3);
        remote[0].Id.ShouldBe(remoteAlbums[0].Id);
        remote[0].Id.ShouldBe(remoteTracks[0].Id);
        await dbContext
            .PlexMusicTracks.Where(x => x.PlexAlbumId == remoteAlbums[1].Id)
            .ExecuteDeleteAsync(CancellationToken);
        foreach (var owned in libraries.Skip(1))
        {
            var ownedArtist = artists.First(x => x.PlexLibraryId == owned.Id);
            var ownedAlbum = albums.First(x => x.PlexArtistId == ownedArtist.Id);
            dbContext.PlexComparisonScopes.Add(
                new PlexComparisonState
                {
                    RemotePlexLibraryId = libraries[0].Id,
                    OwnedPlexLibraryId = owned.Id,
                    MediaType = PlexMediaType.MusicArtist,
                    CompletedAt = DateTime.UtcNow,
                }
            );
            dbContext.PlexMusicArtistComparisons.Add(
                new PlexMusicArtistComparison
                {
                    RemotePlexLibraryId = libraries[0].Id,
                    OwnedPlexLibraryId = owned.Id,
                    RemotePlexMediaId = remote[0].Id,
                    OwnedPlexMediaId = ownedArtist.Id,
                    MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear,
                    ComparedAt = DateTime.UtcNow,
                }
            );
            dbContext.PlexMusicAlbumComparisons.Add(
                new PlexMusicAlbumComparison
                {
                    RemotePlexLibraryId = libraries[0].Id,
                    OwnedPlexLibraryId = owned.Id,
                    RemotePlexMediaId = remoteAlbums[0].Id,
                    OwnedPlexMediaId = ownedAlbum.Id,
                    MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear,
                    ComparedAt = DateTime.UtcNow,
                }
            );
            // Neither library covers the album alone; the middle track is duplicated across targets.
            var ownedTracks = tracks.Where(x => x.PlexAlbumId == ownedAlbum.Id).ToList();
            var matched =
                owned.Id == libraries[1].Id ? remoteTracks.Take(2) : remoteTracks.Skip(1).Take(coverAllTracks ? 2 : 1);
            foreach (var track in matched)
                dbContext.PlexMusicTrackComparisons.Add(
                    new PlexMusicTrackComparison
                    {
                        RemotePlexLibraryId = libraries[0].Id,
                        OwnedPlexLibraryId = owned.Id,
                        RemotePlexMediaId = track.Id,
                        OwnedPlexMediaId = ownedTracks[remoteTracks.IndexOf(track)].Id,
                        MatchType = PlexMediaComparisonMatchType.ParentAndChildNumbers,
                        ComparedAt = DateTime.UtcNow,
                    }
                );
            if (coverEmptyAlbum && owned.Id == libraries[2].Id)
                dbContext.PlexMusicAlbumComparisons.Add(
                    new PlexMusicAlbumComparison
                    {
                        RemotePlexLibraryId = libraries[0].Id,
                        OwnedPlexLibraryId = owned.Id,
                        RemotePlexMediaId = remoteAlbums[1].Id,
                        OwnedPlexMediaId = albums.Last(x => x.PlexArtistId == ownedArtist.Id).Id,
                        MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear,
                        ComparedAt = DateTime.UtcNow,
                    }
                );
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var items = new List<PlexMediaSlimDTO>
        {
            remote[0].ToSlimDTOMapper(),
            remote[1].ToSlimDTOMapper(),
            remoteAlbums[0].ToSlimDTOMapper(),
            remoteAlbums[1].ToSlimDTOMapper(),
            remoteTracks[0].ToSlimDTOMapper(),
            remoteTracks[1].ToSlimDTOMapper(),
        };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items
            .Select(x => x.ComparisonId)
            .ShouldBe(
                new[]
                {
                    (
                        coverEmptyAlbum && coverAllTracks
                            ? PlexMediaComparisonState.Owned
                            : PlexMediaComparisonState.Partial
                    ).ToComparisonId(),
                    PlexMediaComparisonState.Missing.ToComparisonId(),
                    (
                        coverAllTracks ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Partial
                    ).ToComparisonId(),
                    (
                        coverEmptyAlbum ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Missing
                    ).ToComparisonId(),
                    PlexMediaComparisonState.Owned.ToComparisonId(),
                    PlexMediaComparisonState.Owned.ToComparisonId(),
                }
            );
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ShouldSetPendingOrNotCompared_WhenNoStoredScopeExists(bool queued, bool running)
    {
        // Arrange
        await SetupDatabase(87302, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        var artist = await dbContext.PlexMusicArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var album = await dbContext.PlexMusicAlbums.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var track = await dbContext.PlexMusicTracks.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(0);
        var key = PlexLibraryComparisonJob.GetJobKey(libraries[1].Id, libraries[0].Id);
        var items = new List<PlexMediaSlimDTO>
        {
            artist.ToSlimDTOMapper() with
            {
                ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId(),
            },
            album.ToSlimDTOMapper() with
            {
                ComparisonId = PlexMediaComparisonState.Partial.ToComparisonId(),
            },
            track.ToSlimDTOMapper() with
            {
                ComparisonId = PlexMediaComparisonState.Missing.ToComparisonId(),
            },
        };
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(
                (FireInstanceQuery _, CancellationToken _) =>
                    new ValueTask<PagedResult<FireInstance>>(
                        new PagedResult<FireInstance>(
                            running
                                ?
                                [
                                    new FireInstance(
                                        "music-fire",
                                        new TriggerKey(key.Name, key.Group),
                                        key,
                                        "test-scheduler",
                                        FireInstanceState.Executing,
                                        DateTimeOffset.UtcNow,
                                        null,
                                        null
                                    ),
                                ]
                                : [],
                            false
                        )
                    )
            );
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()))
            .Returns(
                (JobQuery _, CancellationToken _) =>
                    new ValueTask<PagedResult<JobHeader>>(
                        new PagedResult<JobHeader>(
                            queued && !running
                                ?
                                [
                                    new JobHeader(
                                        key,
                                        null,
                                        typeof(PlexLibraryComparisonJob).FullName!,
                                        false,
                                        true,
                                        false,
                                        true
                                    ),
                                ]
                                : [],
                            false
                        )
                    )
            );

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items
            .Select(x => x.ComparisonId)
            .ShouldBe(
                Enumerable.Repeat(
                    (
                        (queued || running) ? PlexMediaComparisonState.Pending : PlexMediaComparisonState.NotCompared
                    ).ToComparisonId(),
                    3
                )
            );
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldUseStoredComparisons_WhenEitherLibraryIsOutdated()
    {
        // Arrange
        await SetupDatabase(87303, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var remoteArtist = await dbContext.PlexMusicArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var ownedArtist = await dbContext.PlexMusicArtists.SingleAsync(x => x.PlexLibraryId == libraries[1].Id);
        dbContext.PlexMusicArtistComparisons.Add(
            new PlexMusicArtistComparison
            {
                RemotePlexLibraryId = libraries[0].Id,
                OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteArtist.Id,
                OwnedPlexMediaId = ownedArtist.Id,
                MatchType = PlexMediaComparisonMatchType.PlexArtistGuid,
                ComparedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        var items = new List<PlexMediaSlimDTO> { remoteArtist.ToSlimDTOMapper() };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Single().ComparisonId.ShouldBe(PlexMediaComparisonState.Owned.ToComparisonId());
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldMarkMissing_WhenCurrentScopeHasNoHits()
    {
        // Arrange
        await SetupDatabase(87303, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var artist = await dbContext.PlexMusicArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var items = new List<PlexMediaSlimDTO> { artist.ToSlimDTOMapper() };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Single().ComparisonId.ShouldBe(PlexMediaComparisonState.Missing.ToComparisonId());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldLeaveNotCompared_WhenSourceLibraryIsDisabledOrNoLongerRemote(bool disabled)
    {
        // Arrange
        await SetupDatabase(87304, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var artist = await dbContext.PlexMusicArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var items = new List<PlexMediaSlimDTO>
        {
            artist.ToSlimDTOMapper() with
            {
                ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId(),
            },
        };
        if (disabled)
            await dbContext
                .PlexLibraries.Where(x => x.Id == libraries[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        else
            await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Single().ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldProjectStoredHit_WhenEitherLibraryIsOutdated(bool staleRemoteLibrary)
    {
        // Arrange
        await SetupDatabase(87305, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var remoteArtist = await dbContext.PlexMusicArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var ownedArtist = await dbContext.PlexMusicArtists.SingleAsync(x => x.PlexLibraryId == libraries[1].Id);
        var remoteAlbum = await dbContext.PlexMusicAlbums.SingleAsync(x => x.PlexArtistId == remoteArtist.Id);
        var ownedAlbum = await dbContext.PlexMusicAlbums.SingleAsync(x => x.PlexArtistId == ownedArtist.Id);
        var remoteTrack = await dbContext.PlexMusicTracks.SingleAsync(x => x.PlexAlbumId == remoteAlbum.Id);
        var ownedTrack = await dbContext.PlexMusicTracks.SingleAsync(x => x.PlexAlbumId == ownedAlbum.Id);
        dbContext.PlexMusicArtistComparisons.Add(
            new PlexMusicArtistComparison
            {
                RemotePlexLibraryId = libraries[0].Id,
                OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteArtist.Id,
                OwnedPlexMediaId = ownedArtist.Id,
                MatchType = PlexMediaComparisonMatchType.PlexArtistGuid,
                ComparedAt = DateTime.UtcNow,
            }
        );
        dbContext.PlexMusicAlbumComparisons.Add(
            new PlexMusicAlbumComparison
            {
                RemotePlexLibraryId = libraries[0].Id,
                OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteAlbum.Id,
                OwnedPlexMediaId = ownedAlbum.Id,
                MatchType = PlexMediaComparisonMatchType.PlexAlbumGuid,
                ComparedAt = DateTime.UtcNow,
            }
        );
        dbContext.PlexMusicTrackComparisons.Add(
            new PlexMusicTrackComparison
            {
                RemotePlexLibraryId = libraries[0].Id,
                OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteTrack.Id,
                OwnedPlexMediaId = ownedTrack.Id,
                HitState = PlexMediaComparisonHitState.HigherQuality,
                MatchType = PlexMediaComparisonMatchType.PlexTrackGuid,
                ComparedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        if (staleRemoteLibrary)
            await dbContext
                .PlexLibraries.Where(x => x.Id == libraries[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        else
            await dbContext
                .PlexLibraries.Where(x => x.Id == libraries[1].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        var items = new List<PlexMediaSlimDTO>
        {
            remoteArtist.ToSlimDTOMapper(),
            remoteAlbum.ToSlimDTOMapper(),
            remoteTrack.ToSlimDTOMapper(),
        };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items
            .Select(x => (x.Id, x.ComparisonId))
            .ShouldBe([
                (remoteArtist.Id, PlexMediaComparisonState.HigherQuality.ToComparisonId()),
                (remoteAlbum.Id, PlexMediaComparisonState.HigherQuality.ToComparisonId()),
                (remoteTrack.Id, PlexMediaComparisonState.HigherQuality.ToComparisonId()),
            ]);
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task ShouldProjectHigherQualityThroughMusicHierarchy_WhenAnotherOwnedScopeIsMatched(
        int higherQualityIndex
    )
    {
        // Arrange
        await SetupDatabase(87306, config =>
        {
            config.PlexServerCount = 3;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await SetOwnedOverrideAsync(libraries[2].PlexServerId, true);
        var artists = await dbContext.PlexMusicArtists.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var albums = await dbContext.PlexMusicAlbums.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var tracks = await dbContext.PlexMusicTracks.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var remoteArtist = artists.Single(x => x.PlexLibraryId == libraries[0].Id);
        var remoteAlbum = albums.Single(x => x.PlexArtistId == remoteArtist.Id);
        var remoteTrack = tracks.Single(x => x.PlexAlbumId == remoteAlbum.Id);
        for (var index = 0; index < 2; index++)
        {
            var ownedLibrary = libraries[index + 1];
            var ownedArtist = artists.Single(x => x.PlexLibraryId == ownedLibrary.Id);
            var ownedAlbum = albums.Single(x => x.PlexArtistId == ownedArtist.Id);
            var ownedTrack = tracks.Single(x => x.PlexAlbumId == ownedAlbum.Id);
            await AddCurrentScopeAsync(libraries[0], ownedLibrary);
            dbContext.PlexMusicArtistComparisons.Add(
                new PlexMusicArtistComparison
                {
                    RemotePlexLibraryId = libraries[0].Id,
                    OwnedPlexLibraryId = ownedLibrary.Id,
                    RemotePlexMediaId = remoteArtist.Id,
                    OwnedPlexMediaId = ownedArtist.Id,
                    MatchType = PlexMediaComparisonMatchType.PlexArtistGuid,
                    ComparedAt = DateTime.UtcNow,
                }
            );
            dbContext.PlexMusicAlbumComparisons.Add(
                new PlexMusicAlbumComparison
                {
                    RemotePlexLibraryId = libraries[0].Id,
                    OwnedPlexLibraryId = ownedLibrary.Id,
                    RemotePlexMediaId = remoteAlbum.Id,
                    OwnedPlexMediaId = ownedAlbum.Id,
                    MatchType = PlexMediaComparisonMatchType.PlexAlbumGuid,
                    ComparedAt = DateTime.UtcNow,
                }
            );
            dbContext.PlexMusicTrackComparisons.Add(
                new PlexMusicTrackComparison
                {
                    RemotePlexLibraryId = libraries[0].Id,
                    OwnedPlexLibraryId = ownedLibrary.Id,
                    RemotePlexMediaId = remoteTrack.Id,
                    OwnedPlexMediaId = ownedTrack.Id,
                    HitState =
                        index == higherQualityIndex
                            ? PlexMediaComparisonHitState.HigherQuality
                            : PlexMediaComparisonHitState.Matched,
                    MatchType = PlexMediaComparisonMatchType.PlexTrackGuid,
                    ComparedAt = DateTime.UtcNow,
                }
            );
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var hits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.OwnedPlexLibraryId)
            .ToListAsync(CancellationToken);
        hits.Select(x => (x.OwnedPlexLibraryId, x.RemotePlexMediaId, x.HitState))
            .ShouldBe([
                (
                    libraries[1].Id,
                    remoteTrack.Id,
                    higherQualityIndex == 0
                        ? PlexMediaComparisonHitState.HigherQuality
                        : PlexMediaComparisonHitState.Matched
                ),
                (
                    libraries[2].Id,
                    remoteTrack.Id,
                    higherQualityIndex == 1
                        ? PlexMediaComparisonHitState.HigherQuality
                        : PlexMediaComparisonHitState.Matched
                ),
            ]);
        var items = new List<PlexMediaSlimDTO>
        {
            remoteArtist.ToSlimDTOMapper() with
            {
                ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId(),
            },
            remoteAlbum.ToSlimDTOMapper() with
            {
                ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId(),
            },
            remoteTrack.ToSlimDTOMapper() with
            {
                ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId(),
            },
        };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items
            .Select(x => (x.Id, x.Type, x.ComparisonId))
            .ShouldBe([
                (remoteArtist.Id, PlexMediaType.MusicArtist, PlexMediaComparisonState.HigherQuality.ToComparisonId()),
                (remoteAlbum.Id, PlexMediaType.MusicAlbum, PlexMediaComparisonState.HigherQuality.ToComparisonId()),
                (remoteTrack.Id, PlexMediaType.MusicTrack, PlexMediaComparisonState.HigherQuality.ToComparisonId()),
            ]);
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IScheduler>()
            .Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
