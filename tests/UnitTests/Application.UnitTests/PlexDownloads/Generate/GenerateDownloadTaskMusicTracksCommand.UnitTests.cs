namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskMusicTracksCommandUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskMusicTracksCommand>
{
    [Test]
    public async Task ShouldCreateMissingHierarchyAndEverySelectedPartWithLog_WhenTrackIsSelected()
    {
        // Arrange
        await SetupDatabase(62605, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var track = await IDbContext.PlexTracks.Include(x => x.PlexAlbum).ThenInclude(x => x!.PlexArtist)
            .Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var original = track.MediaDataList.Single();
        var secondPart = FakeData.GetPlexMusicTrackMediaData(new Seed(62606))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexTrackId, _ => track.Id)
            .RuleFor(x => x.PlexServerId, _ => track.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => track.PlexLibraryId)
            .RuleFor(x => x.PlexApiRatingKey, _ => track.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId)
            .Generate();
        IDbContext.PlexTrackData.Add(secondPart);
        await IDbContext.SaveChangesAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicTrack,
            PlexServerId = track.PlexServerId,
            PlexLibraryId = track.PlexLibraryId,
            MediaIds = [track.Id],
            Qualities = [],
            KeepCompletedInDownloadFolder = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicTracksCommand(
                new CreateDownloadTasksRequest([selection], 9, "/music")
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { MusicTracks = 1 });
        var artistTask = await IDbContext.DownloadTaskMusicArtists.Include(x => x.Children)
            .ThenInclude(x => x.Children).ThenInclude(x => x.Children).SingleAsync(CancellationToken);
        var albumTask = artistTask.Children.Single();
        var trackTask = albumTask.Children.Single();
        trackTask.PlexApiRatingKey.ShouldBe(track.PlexApiRatingKey);
        trackTask.Children.Select(x => (x.PlexApiMediaId, x.PlexApiPartId)).OrderBy(x => x.PlexApiPartId)
            .ShouldBe(new[] { original, secondPart }.Select(x => (x.PlexApiMediaId, x.PlexApiPartId)).OrderBy(x => x.PlexApiPartId));
        trackTask.Children.ShouldAllBe(x =>
            x.DestinationFolderPathId == 9
            && x.DirectoryMeta.DestinationRootPath == "/music"
            && x.DirectoryMeta.KeepCompletedInDownloadFolder
        );
        var logs = await IDbContext.DownloadTaskTrackFileLogs.OrderBy(x => x.DownloadTaskFileId).ToListAsync(CancellationToken);
        logs.Select(x => (x.DownloadTaskFileId, x.DownloadTaskTrackId, x.DownloadTaskAlbumId, x.DownloadTaskArtistId))
            .ShouldBe(trackTask.Children.OrderBy(x => x.Id).Select(x => (x.Id, trackTask.Id, albumTask.Id, artistTask.Id)));
    }

    [Test]
    public async Task ShouldUseRequestedOriginalAndSkipDuplicateAtNewDestination_WhenTrackAlreadyExists()
    {
        // Arrange
        await SetupDatabase(62607, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var track = await IDbContext.PlexTracks.Include(x => x.PlexAlbum).ThenInclude(x => x!.PlexArtist)
            .Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var original = track.MediaDataList.Single();
        var alternate = FakeData.GetPlexMusicTrackMediaData(new Seed(62608))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexTrackId, _ => track.Id)
            .RuleFor(x => x.PlexServerId, _ => track.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => track.PlexLibraryId)
            .RuleFor(x => x.PlexApiRatingKey, _ => track.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId + 1)
            .Generate();
        IDbContext.PlexTrackData.Add(alternate);
        await IDbContext.SaveChangesAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicTrack,
            PlexServerId = track.PlexServerId,
            PlexLibraryId = track.PlexLibraryId,
            MediaIds = [track.Id],
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaId = track.Id,
                    DataId = alternate.Id,
                    MediaDataType = PlexMediaType.MusicTrack,
                    Quality = VideoQuality.Unknown,
                },
            ],
        };
        var first = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicTracksCommand(new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/one"))
        );
        first.IsSuccess.ShouldBeTrue();

        // Act
        var repeated = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicTracksCommand(new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/two"))
        );

        // Assert
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Errors.Count.ShouldBe(0);
        repeated.Value.ShouldBe(new DownloadTaskCreationReport());
        var files = await IDbContext.DownloadTaskMusicTrackFiles.ToListAsync(CancellationToken);
        files.Count.ShouldBe(1);
        files.Single().PlexApiMediaId.ShouldBe(alternate.PlexApiMediaId);
        files.Single().DirectoryMeta.DestinationRootPath.ShouldBe("/one");
        (await IDbContext.DownloadTaskTrackFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Test]
    public async Task ShouldFallBackToAutomaticOriginal_WhenRequestedDataIdIsMissing()
    {
        // Arrange
        await SetupDatabase(62611, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var track = await IDbContext.PlexTracks.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var automatic = track.MediaDataList.PickMediaQuality()!;
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicTrack,
            PlexServerId = track.PlexServerId,
            PlexLibraryId = track.PlexLibraryId,
            MediaIds = [track.Id],
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaId = track.Id,
                    DataId = int.MaxValue,
                    MediaDataType = PlexMediaType.MusicTrack,
                    Quality = VideoQuality.Unknown,
                },
            ],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicTracksCommand(new CreateDownloadTasksRequest([selection]))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { MusicTracks = 1 });
        (await IDbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken)).PlexApiMediaId.ShouldBe(
            automatic.PlexApiMediaId
        );
    }

    [Test]
    public async Task ShouldRejectTrackFromAnotherLibraryWithoutCreatingHierarchy_WhenOwnershipMismatches()
    {
        // Arrange
        await SetupDatabase(62609, config =>
        {
            config.PlexMusicLibraryCount = 2;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var tracks = await IDbContext.PlexTracks.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicTrack,
            PlexServerId = tracks[0].PlexServerId,
            PlexLibraryId = tracks[1].PlexLibraryId,
            MediaIds = [tracks[0].Id],
            Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicTracksCommand(new CreateDownloadTasksRequest([selection]))
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await IDbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(0);
        (await IDbContext.DownloadTaskMusicTrackFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await IDbContext.DownloadTaskTrackFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }
}
