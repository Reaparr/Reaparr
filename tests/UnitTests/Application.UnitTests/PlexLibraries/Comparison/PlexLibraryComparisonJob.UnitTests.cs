using Quartz;

namespace Reaparr.Application.UnitTests;

public class PlexLibraryComparisonJobUnitTests : BaseUnitTest<PlexLibraryComparisonJob>
{
    [Test]
    public async Task ShouldRunMusicWriterAndPublishScope_WhenEligibleMusicPairExecutes()
    {
        // Arrange
        await SetupDatabase(963520, config =>
        {
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1; config.MusicAlbumCount = 0;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var owned = libraries[1];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(owned.PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x
            .SetProperty(y => y.Outdated, false).SetProperty(y => y.SyncedAt, DateTime.UtcNow)
            .SetProperty(y => y.SyncedContentChangedAt, y => y.ContentChangedAt), CancellationToken);
        await dbContext.PlexArtists.ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzArtistId, "test-artist"), CancellationToken);
        var artists = await dbContext.PlexArtists.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(0);
        var context = new Mock<IJobExecutionContext>();
        context.SetupAllProperties();
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new PlexLibraryComparisonJobPayload(owned.Id, remote.Id).ToJobDataMap());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<CompareMusicPlexLibraryCommand>(c => c.OwnedPlexLibraryId == owned.Id && c.RemotePlexLibraryId == remote.Id), CancellationToken))
            .Returns<ICommand<Result>, CancellationToken>((cmd, ct) =>
                new CompareMusicPlexLibraryCommandHandler(IDbContext, Log)
                    .ExecuteAsync((CompareMusicPlexLibraryCommand)cmd, ct))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        ((BackgroundJobResult)context.Object.Result!).Status.ShouldBe(JobStatus.Completed);
        var scopes = await dbContext.PlexComparisonScopes.ToListAsync(CancellationToken);
        scopes.Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.MediaType))
            .ShouldBe([(remote.Id, owned.Id, PlexMediaType.MusicArtist)]);
        var hits = await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe([(artists[0].Id, artists[1].Id, PlexMediaComparisonMatchType.MusicBrainzArtistId)]);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<CompareMoviePlexLibraryCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<CompareTvShowPlexLibraryCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    [Arguments(PlexMediaType.Movie)]
    public async Task ShouldRejectUnsupportedOrMixedPairWithoutDispatch_WhenMusicCannotBeCompared(PlexMediaType ownedType)
    {
        // Arrange
        await SetupDatabase(963521, config => { config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1; });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, false);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, true);
        await dbContext.PlexLibraries.Where(x => x.Id == libraries[1].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, ownedType), CancellationToken);
        if (ownedType != PlexMediaType.Movie)
            await dbContext.PlexLibraries.Where(x => x.Id == libraries[0].Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Type, ownedType), CancellationToken);
        var context = new Mock<IJobExecutionContext>();
        context.SetupAllProperties();
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new PlexLibraryComparisonJobPayload(libraries[1].Id, libraries[0].Id).ToJobDataMap());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        ((BackgroundJobResult)context.Object.Result!).Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<CompareMusicPlexLibraryCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<CompareMoviePlexLibraryCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<CompareTvShowPlexLibraryCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(0);
    }
}
