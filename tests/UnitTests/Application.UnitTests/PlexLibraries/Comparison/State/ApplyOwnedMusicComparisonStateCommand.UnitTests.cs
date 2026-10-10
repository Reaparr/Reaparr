using Quartz;
using Quartz.Impl.Matchers;

namespace Reaparr.Application.UnitTests;

public class ApplyOwnedMusicComparisonStateCommandUnitTests : BaseCommandUnitTest<ApplyOwnedMusicComparisonStateCommand>
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task ShouldProjectMissingRemoteDescendants_WhenOwnedMusicHasMatchedArtistAndEdition(bool matchArtist, bool matchTrack, bool matchEmptyAlbum)
    {
        // Arrange
        await SetupDatabase(87401, config =>
        {
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1; config.MusicAlbumCount = 2; config.MusicTrackCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var artists = await dbContext.PlexArtists.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var albums = await dbContext.PlexAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var tracks = await dbContext.PlexTracks.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteArtist = artists.Single(x => x.PlexLibraryId == libraries[0].Id);
        var ownedArtist = artists.Single(x => x.PlexLibraryId == libraries[1].Id);
        var remoteAlbum = albums.First(x => x.PlexArtistId == remoteArtist.Id);
        var ownedAlbum = albums.First(x => x.PlexArtistId == ownedArtist.Id);
        var ownedExtraAlbum = albums.Last(x => x.PlexArtistId == ownedArtist.Id);
        var remoteTrack = tracks.Single(x => x.PlexAlbumId == remoteAlbum.Id);
        var ownedTrack = tracks.Single(x => x.PlexAlbumId == ownedAlbum.Id);
        // Unmatched empty remote albums still make the artist Partial even if all tracks match.
        var emptyRemoteAlbumId = albums.Last(y => y.PlexArtistId == remoteArtist.Id).Id;
        await dbContext.PlexTracks.Where(x => x.PlexAlbumId == emptyRemoteAlbumId)
            .ExecuteDeleteAsync(CancellationToken);
        if (matchArtist)
        {
            dbContext.PlexMusicArtistComparisons.Add(new PlexMusicArtistComparison
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteArtist.Id, OwnedPlexMediaId = ownedArtist.Id,
                MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear, ComparedAt = DateTime.UtcNow,
            });
            dbContext.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteAlbum.Id, OwnedPlexMediaId = ownedAlbum.Id,
                MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear, ComparedAt = DateTime.UtcNow,
            });
        }
        if (matchTrack)
            dbContext.PlexMusicTrackComparisons.Add(new PlexMusicTrackComparison
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteTrack.Id, OwnedPlexMediaId = ownedTrack.Id,
                MatchType = PlexMediaComparisonMatchType.ParentAndChildNumbers, ComparedAt = DateTime.UtcNow,
            });
        if (matchEmptyAlbum)
            dbContext.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = emptyRemoteAlbumId, OwnedPlexMediaId = ownedExtraAlbum.Id,
                MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear, ComparedAt = DateTime.UtcNow,
            });
        await dbContext.SaveChangesAsync(CancellationToken);
        var items = new List<PlexMediaSlimDTO>
        {
            ownedArtist.ToSlimDTOMapper(), ownedAlbum.ToSlimDTOMapper(), ownedExtraAlbum.ToSlimDTOMapper(), ownedTrack.ToSlimDTOMapper(),
        };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(items, libraries[1].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Select(x => x.ComparisonId).ShouldBe(new[]
        {
            (matchArtist && (!matchTrack || !matchEmptyAlbum) ? PlexMediaComparisonState.Partial : PlexMediaComparisonState.Owned).ToComparisonId(),
            (matchArtist && !matchTrack ? PlexMediaComparisonState.Partial : PlexMediaComparisonState.Owned).ToComparisonId(),
            PlexMediaComparisonState.Owned.ToComparisonId(), PlexMediaComparisonState.Owned.ToComparisonId(),
        });
        Mock.Mock<IScheduler>().Verify(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldCountDistinctRemoteTracksWithinMatchedParents_WhenArtistHasMultipleOwnedAlbumMatches(bool matchSecondTrack)
    {
        // Arrange
        await SetupDatabase(87403, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 2;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 2;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        var artists = await dbContext.PlexArtists.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var albums = await dbContext.PlexAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var tracks = await dbContext.PlexTracks.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteArtist = artists.First(x => x.PlexLibraryId == libraries[0].Id);
        var ownedArtist = artists.First(x => x.PlexLibraryId == libraries[1].Id);
        var otherOwnedArtist = artists.Last(x => x.PlexLibraryId == libraries[1].Id);
        var remoteAlbum = albums.First(x => x.PlexArtistId == remoteArtist.Id);
        var ownedAlbums = albums.Where(x => x.PlexArtistId == ownedArtist.Id).ToList();
        var remoteTracks = tracks.Where(x => x.PlexAlbumId == remoteAlbum.Id).ToList();
        var firstOwnedTrack = tracks.First(x => x.PlexAlbumId == ownedAlbums[0].Id);
        var duplicateOwnedTrack = tracks.Last(x => x.PlexAlbumId == ownedAlbums[0].Id);
        var secondOwnedTrack = tracks.First(x => x.PlexAlbumId == ownedAlbums[1].Id);
        var unrelatedOwnedTrack = tracks.First(x => albums.Single(y => y.Id == x.PlexAlbumId).PlexArtistId == otherOwnedArtist.Id);
        remoteTracks.Count.ShouldBe(2);
        ownedAlbums.Count.ShouldBe(2);
        await dbContext.PlexAlbums
            .Where(x => x.PlexArtistId == remoteArtist.Id && x.Id != remoteAlbum.Id)
            .ExecuteDeleteAsync(CancellationToken);

        dbContext.PlexMusicArtistComparisons.Add(new PlexMusicArtistComparison
        {
            RemotePlexLibraryId = libraries[0].Id,
            OwnedPlexLibraryId = libraries[1].Id,
            RemotePlexMediaId = remoteArtist.Id,
            OwnedPlexMediaId = ownedArtist.Id,
            MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear,
            ComparedAt = DateTime.UtcNow,
        });
        foreach (var ownedAlbum in ownedAlbums)
        {
            dbContext.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
            {
                RemotePlexLibraryId = libraries[0].Id,
                OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = remoteAlbum.Id,
                OwnedPlexMediaId = ownedAlbum.Id,
                MatchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear,
                ComparedAt = DateTime.UtcNow,
            });
        }
        var trackMatches = new[]
        {
            (RemoteId: remoteTracks[0].Id, OwnedId: firstOwnedTrack.Id),
            (RemoteId: remoteTracks[0].Id, OwnedId: duplicateOwnedTrack.Id),
            (RemoteId: remoteTracks[1].Id, OwnedId: matchSecondTrack ? secondOwnedTrack.Id : unrelatedOwnedTrack.Id),
        };
        foreach (var match in trackMatches)
        {
            dbContext.PlexMusicTrackComparisons.Add(new PlexMusicTrackComparison
            {
                RemotePlexLibraryId = libraries[0].Id,
                OwnedPlexLibraryId = libraries[1].Id,
                RemotePlexMediaId = match.RemoteId,
                OwnedPlexMediaId = match.OwnedId,
                MatchType = PlexMediaComparisonMatchType.ParentAndChildNumbers,
                ComparedAt = DateTime.UtcNow,
            });
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var items = new List<PlexMediaSlimDTO>
        {
            ownedArtist.ToSlimDTOMapper(),
            ownedAlbums[0].ToSlimDTOMapper(),
            ownedAlbums[1].ToSlimDTOMapper(),
            firstOwnedTrack.ToSlimDTOMapper(),
        };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(items, libraries[1].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Select(x => x.ComparisonId).ShouldBe(new[]
        {
            (matchSecondTrack ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Partial).ToComparisonId(),
            PlexMediaComparisonState.Partial.ToComparisonId(),
            PlexMediaComparisonState.Partial.ToComparisonId(),
            PlexMediaComparisonState.Owned.ToComparisonId(),
        });
        Mock.Mock<IScheduler>().Verify(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>().Verify(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldNotDefaultOwned_WhenCompletedScopeIsStale(bool queued)
    {
        // Arrange
        await SetupDatabase(87402, config =>
        {
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1; config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await AddCurrentScopeAsync(libraries[0], libraries[1]);
        await dbContext.PlexLibraries.Where(x => x.Id == libraries[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
        var artist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == libraries[1].Id);
        var items = new List<PlexMediaSlimDTO> { artist.ToSlimDTOMapper() with { ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId() } };
        var key = PlexLibraryComparisonJob.GetJobKey(libraries[1].Id, libraries[0].Id);
        Mock.Mock<IScheduler>().Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IJobExecutionContext>()).Verifiable(Times.Once());
        Mock.Mock<IScheduler>().Setup(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queued ? new[] { key } : Array.Empty<JobKey>()).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(items, libraries[1].Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        items.Single().ComparisonId.ShouldBe((queued ? PlexMediaComparisonState.Pending : PlexMediaComparisonState.NotCompared).ToComparisonId());
        Mock.Mock<IScheduler>().Verify();
    }
}
