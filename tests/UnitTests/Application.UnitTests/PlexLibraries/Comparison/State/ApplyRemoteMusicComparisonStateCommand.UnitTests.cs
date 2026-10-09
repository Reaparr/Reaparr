using Quartz;
using Quartz.Impl.Matchers;

namespace Reaparr.Application.UnitTests;

public class ApplyRemoteMusicComparisonStateCommandUnitTests : BaseCommandUnitTest<ApplyRemoteMusicComparisonStateCommand>
{
    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(true, false)]
    public async Task ShouldAggregateDistinctCoverageIncludingEmptyAlbums_WhenMultipleOwnedLibrariesMatch(bool coverEmptyAlbum, bool coverAllTracks)
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
        var artists = await dbContext.PlexArtists.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = artists.Where(x => x.PlexLibraryId == libraries[0].Id).ToList();
        var albums = await dbContext.PlexAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteAlbums = albums.Where(x => x.PlexArtistId == remote[0].Id).ToList();
        var tracks = await dbContext.PlexTracks.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteTracks = tracks.Where(x => x.PlexAlbumId == remoteAlbums[0].Id).ToList();
        remote.Count.ShouldBe(2);
        remoteAlbums.Count.ShouldBe(2);
        remoteTracks.Count.ShouldBe(3);
        remote[0].Id.ShouldBe(remoteAlbums[0].Id);
        remote[0].Id.ShouldBe(remoteTracks[0].Id);
        await dbContext.PlexTracks.Where(x => x.PlexAlbumId == remoteAlbums[1].Id).ExecuteDeleteAsync(CancellationToken);
        foreach (var owned in libraries.Skip(1))
        {
            var ownedArtist = artists.First(x => x.PlexLibraryId == owned.Id);
            var ownedAlbum = albums.First(x => x.PlexArtistId == ownedArtist.Id);
            dbContext.PlexComparisonScopes.Add(new PlexComparisonState
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = owned.Id,
                MediaType = PlexMediaType.MusicArtist, CompletedAt = DateTime.UtcNow,
            });
            dbContext.PlexMusicArtistComparisons.Add(new PlexMusicArtistComparison
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = owned.Id,
                RemotePlexMediaId = remote[0].Id, OwnedPlexMediaId = ownedArtist.Id,
                MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear, ComparedAt = DateTime.UtcNow,
            });
            dbContext.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = owned.Id,
                RemotePlexMediaId = remoteAlbums[0].Id, OwnedPlexMediaId = ownedAlbum.Id,
                MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear, ComparedAt = DateTime.UtcNow,
            });
            // Neither library covers the album alone; the middle track is duplicated across targets.
            var ownedTracks = tracks.Where(x => x.PlexAlbumId == ownedAlbum.Id).ToList();
            var matched = owned.Id == libraries[1].Id ? remoteTracks.Take(2) : remoteTracks.Skip(1).Take(coverAllTracks ? 2 : 1);
            foreach (var track in matched)
                dbContext.PlexMusicTrackComparisons.Add(new PlexMusicTrackComparison
                {
                    RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = owned.Id,
                    RemotePlexMediaId = track.Id, OwnedPlexMediaId = ownedTracks[remoteTracks.IndexOf(track)].Id,
                    MatchType = PlexMediaComparisonMatchType.ParentAndChildNumbers, ComparedAt = DateTime.UtcNow,
                });
            if (coverEmptyAlbum && owned.Id == libraries[2].Id)
                dbContext.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
                {
                    RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = owned.Id,
                    RemotePlexMediaId = remoteAlbums[1].Id, OwnedPlexMediaId = albums.Last(x => x.PlexArtistId == ownedArtist.Id).Id,
                    MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear, ComparedAt = DateTime.UtcNow,
                });
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
        items.Select(x => x.ComparisonId).ShouldBe(new[]
        {
            (coverEmptyAlbum && coverAllTracks ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Partial).ToComparisonId(),
            PlexMediaComparisonState.Missing.ToComparisonId(),
            (coverAllTracks ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Partial).ToComparisonId(),
            (coverEmptyAlbum ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Missing).ToComparisonId(),
            PlexMediaComparisonState.Owned.ToComparisonId(), PlexMediaComparisonState.Owned.ToComparisonId(),
        });
        Mock.Mock<IScheduler>().Verify(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task ShouldIgnoreStaleScopesAndResetStates_WhenPairIsNotCurrent(bool staleScope, bool queued, bool running)
    {
        // Arrange
        await SetupDatabase(87302, config =>
        {
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1; config.MusicAlbumCount = 1; config.MusicTrackCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        var artist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var album = await dbContext.PlexAlbums.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var track = await dbContext.PlexTracks.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        if (staleScope)
        {
            await AddCurrentScopeAsync(libraries[0], libraries[1]);
            await dbContext.PlexLibraries.Where(x => x.Id == libraries[1].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        }
        var key = PlexLibraryComparisonJob.GetJobKey(libraries[1].Id, libraries[0].Id);
        var items = new List<PlexMediaSlimDTO>
        {
            artist.ToSlimDTOMapper() with { ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId() },
            album.ToSlimDTOMapper() with { ComparisonId = PlexMediaComparisonState.Partial.ToComparisonId() },
            track.ToSlimDTOMapper() with { ComparisonId = PlexMediaComparisonState.Missing.ToComparisonId() },
        };
        var executing = new Mock<IJobExecutionContext>();
        executing.SetupGet(x => x.JobDetail).Returns(JobBuilder.Create<PlexLibraryComparisonJob>().WithIdentity(key).Build())
            .Verifiable(running ? Times.Once() : Times.Never());
        Mock.Mock<IScheduler>().Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(running ? new[] { executing.Object } : Array.Empty<IJobExecutionContext>()).Verifiable(Times.Once());
        Mock.Mock<IScheduler>().Setup(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queued && !running ? new[] { key } : Array.Empty<JobKey>()).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Select(x => x.ComparisonId).ShouldBe(Enumerable.Repeat(
            (queued ? PlexMediaComparisonState.Pending : PlexMediaComparisonState.NotCompared).ToComparisonId(), 3));
        Mock.Mock<IScheduler>().Verify();
        executing.Verify();
    }

    [Test]
    public async Task ShouldMarkMissing_WhenCurrentScopeHasNoHits()
    {
        // Arrange
        await SetupDatabase(87303, config =>
        {
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1; config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var artist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
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
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1; config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var artist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == libraries[0].Id);
        var items = new List<PlexMediaSlimDTO> { artist.ToSlimDTOMapper() with { ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId() } };
        if (disabled)
            await dbContext.PlexLibraries.Where(x => x.Id == libraries[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        else
            await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, libraries[0].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Single().ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
        Mock.Mock<IScheduler>().Verify(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldResetToNotCompared_WhenSourceMusicLibraryIsOutdated(bool ownedSource)
    {
        // Arrange
        await SetupDatabase(87305, config =>
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
        var source = libraries[ownedSource ? 1 : 0];
        await dbContext.PlexLibraries.Where(x => x.Id == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        var artist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == source.Id, CancellationToken);
        var items = new List<PlexMediaSlimDTO>
        {
            artist.ToSlimDTOMapper() with { ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId() },
        };

        // Act
        var result = ownedSource
            ? await new ApplyOwnedMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<IScheduler>().Object)
                .ExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(items, source.Id), CancellationToken)
            : await TestHandlerExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, source.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items.Single().ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(1);
        Mock.Mock<IScheduler>().Verify(
            x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()), Times.Never());
    }
}
