using Quartz;

namespace Reaparr.Application.UnitTests;

public class GetMusicMediaComparisonDetailsCommandUnitTests
    : BaseCommandUnitTest<GetMusicMediaComparisonDetailsCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnPartialHierarchyWithMissingEmptyAlbum_WhenOnlySomeTracksAreOwned(bool ownedView)
    {
        // Arrange
        await SetupDatabase(963501, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 3;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var owned = libraries[1];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(owned.PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, false), CancellationToken);
        var remoteArtist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == remote.Id, CancellationToken);
        var ownedArtist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == owned.Id, CancellationToken);
        var remoteAlbums = await dbContext.PlexAlbums.Where(x => x.PlexArtistId == remoteArtist.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var ownedAlbum = await dbContext.PlexAlbums.Where(x => x.PlexArtistId == ownedArtist.Id).OrderBy(x => x.Id).FirstAsync(CancellationToken);
        await dbContext.PlexTracks.Where(x => x.PlexAlbumId == remoteAlbums[1].Id).ExecuteDeleteAsync(CancellationToken);
        var remoteTracks = await dbContext.PlexTracks.Where(x => x.PlexAlbumId == remoteAlbums[0].Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var ownedTrack = await dbContext.PlexTracks.Where(x => x.PlexAlbumId == ownedAlbum.Id).OrderBy(x => x.Id).FirstAsync(CancellationToken);
        remoteTracks.Count.ShouldBe(3);
        (await dbContext.PlexTracks.CountAsync(x => x.PlexAlbumId == remoteAlbums[1].Id, CancellationToken)).ShouldBe(0);
        dbContext.PlexComparisonScopes.Add(new PlexComparisonState
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            MediaType = PlexMediaType.MusicArtist, CompletedAt = DateTime.UtcNow,
        });
        dbContext.PlexMusicArtistComparisons.Add(new PlexMusicArtistComparison
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            RemotePlexMediaId = remoteArtist.Id, OwnedPlexMediaId = ownedArtist.Id,
            MatchType = PlexMediaComparisonMatchType.MusicBrainzArtistId, ComparedAt = DateTime.UtcNow,
        });
        dbContext.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            RemotePlexMediaId = remoteAlbums[0].Id, OwnedPlexMediaId = ownedAlbum.Id,
            MatchType = PlexMediaComparisonMatchType.MusicBrainzReleaseId, ComparedAt = DateTime.UtcNow,
        });
        dbContext.PlexMusicTrackComparisons.Add(new PlexMusicTrackComparison
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            RemotePlexMediaId = remoteTracks[0].Id, OwnedPlexMediaId = ownedTrack.Id,
            MatchType = PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId, ComparedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync(CancellationToken);
        var target = ownedView ? ownedArtist : remoteArtist;
        var executor = Mock.Mock<ICommandExecutor>();
        executor.Setup(x => x.Send(It.Is<ApplyComparisonStateCommand>(c => c.MediaType == PlexMediaType.MusicArtist && c.PlexLibraryId == target.PlexLibraryId), CancellationToken))
            .Returns<ICommand<Result>, CancellationToken>((cmd, ct) =>
                new ApplyComparisonStateCommandHandler(IDbContext, executor.Object).ExecuteAsync((ApplyComparisonStateCommand)cmd, ct))
            .Verifiable(Times.Once());
        executor.Setup(x => x.Send(It.IsAny<ApplyRemoteMusicComparisonStateCommand>(), CancellationToken))
            .Returns<ICommand<Result>, CancellationToken>((cmd, ct) =>
                new ApplyRemoteMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<IScheduler>().Object).ExecuteAsync((ApplyRemoteMusicComparisonStateCommand)cmd, ct))
            .Verifiable(ownedView ? Times.Never() : Times.Once());
        executor.Setup(x => x.Send(It.IsAny<ApplyOwnedMusicComparisonStateCommand>(), CancellationToken))
            .Returns<ICommand<Result>, CancellationToken>((cmd, ct) =>
                new ApplyOwnedMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<IScheduler>().Object).ExecuteAsync((ApplyOwnedMusicComparisonStateCommand)cmd, ct))
            .Verifiable(ownedView ? Times.Once() : Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(new GetMusicMediaComparisonDetailsCommand(target.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.PlexMediaId, result.Value.Type, result.Value.State).ShouldBe((target.Id, PlexMediaType.MusicArtist, PlexMediaComparisonState.Partial));
        result.Value.Rows.Select(x => (x.PlexMediaId, x.Type, x.State, x.PlexLibraryId, x.PlexServerId)).OrderBy(x => x.PlexMediaId).ShouldBe(
            new[]
            {
                (remoteAlbums[0].Id, PlexMediaType.MusicAlbum, PlexMediaComparisonState.Partial, remote.Id, remote.PlexServerId),
                (remoteAlbums[1].Id, PlexMediaType.MusicAlbum, PlexMediaComparisonState.Missing, remote.Id, remote.PlexServerId),
            }.OrderBy(x => x.Item1));
        var albumRow = result.Value.Rows.Single(x => x.PlexMediaId == remoteAlbums[0].Id);
        albumRow.Children.Select(x => (x.PlexMediaId, x.Type, x.State)).OrderBy(x => x.PlexMediaId).ShouldBe(
            remoteTracks.Select(x => (x.Id, PlexMediaType.MusicTrack, x.Id == remoteTracks[0].Id ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Missing)));
        result.Value.Rows.Single(x => x.PlexMediaId == remoteAlbums[1].Id).Children.ShouldBeEmpty();
        result.Value.Rows.Concat(albumRow.Children).ShouldAllBe(x => x.RemoteQuality == VideoQuality.None && x.OwnedQuality == VideoQuality.None);
        albumRow.Children.ShouldAllBe(x => x.PlexLibraryId == remote.Id && x.PlexServerId == remote.PlexServerId && x.Children.Count == 0);
        executor.Verify();
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    [Arguments(false, false, true)]
    [Arguments(true, false, true)]
    public async Task ShouldNotInferOwnedFromEmptyRows_WhenScopeIsAbsentOrEmpty(bool ownedView, bool scopeExists, bool pending)
    {
        // Arrange
        await SetupDatabase(963502, config =>
        {
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1; config.MusicAlbumCount = 0;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, false), CancellationToken);
        var target = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == libraries[ownedView ? 1 : 0].Id, CancellationToken);
        if (scopeExists)
        {
            dbContext.PlexComparisonScopes.Add(new PlexComparisonState
            {
                RemotePlexLibraryId = libraries[0].Id, OwnedPlexLibraryId = libraries[1].Id,
                MediaType = PlexMediaType.MusicArtist, CompletedAt = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync(CancellationToken);
        }
        var executor = Mock.Mock<ICommandExecutor>();
        executor.Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), CancellationToken))
            .Returns<ICommand<Result>, CancellationToken>((cmd, ct) => ownedView
                ? new ApplyOwnedMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<IScheduler>().Object).ExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(((ApplyComparisonStateCommand)cmd).Items, target.PlexLibraryId), ct)
                : new ApplyRemoteMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<IScheduler>().Object).ExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(((ApplyComparisonStateCommand)cmd).Items, target.PlexLibraryId), ct))
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>().Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken)).ReturnsAsync([]);
        var pairKey = PlexLibraryComparisonJob.GetJobKey(libraries[1].Id, libraries[0].Id);
        Mock.Mock<IScheduler>().Setup(x => x.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup(), CancellationToken))
            .ReturnsAsync(pending ? [pairKey] : []).Verifiable(scopeExists ? Times.Never() : Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(new GetMusicMediaComparisonDetailsCommand(target.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Rows.ShouldBeEmpty();
        result.Value.State.ShouldBe(pending ? PlexMediaComparisonState.Pending
            : !scopeExists ? PlexMediaComparisonState.NotCompared
            : ownedView ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Missing);
        executor.Verify();
        Mock.Mock<IScheduler>().Verify();
    }
    [Test]
    [Arguments(0)]
    [Arguments(int.MaxValue)]
    public async Task ShouldRejectInvalidOrMissingArtistWithoutProjection_WhenArtistCannotBeLoaded(int id)
    {
        // Arrange
        await SetupDatabase(963504, config => { config.PlexMusicLibraryCount = 1; config.MusicArtistCount = 0; });

        // Act
        var result = await TestHandlerExecuteAsync<PlexMediaComparisonDetailsDTO>(new GetMusicMediaComparisonDetailsCommand(id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(id == 0 ? 2 : 1);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

}
