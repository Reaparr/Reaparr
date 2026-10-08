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
        var dbContext = IDbContext;
        var track = await dbContext.PlexTracks.Include(x => x.PlexAlbum).ThenInclude(x => x!.PlexArtist)
            .Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var original = track.MediaDataList.Single();
        var secondPart = FakeData.GetPlexMusicTrackMediaData(new Seed(62606))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexTrackId, _ => track.Id)
            .RuleFor(x => x.PlexServerId, _ => track.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => track.PlexLibraryId)
            .RuleFor(x => x.PlexApiRatingKey, _ => track.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId)
            .RuleFor(x => x.PartIndex, _ => original.PartIndex + 1)
            .Generate();
        dbContext.PlexTrackData.Add(secondPart);
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexTrackData.Where(x => x.PlexTrackId == track.Id).OrderBy(x => x.Id)
            .Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([original.Id, secondPart.Id]);
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
        var artistTask = await dbContext.DownloadTaskMusicArtists.Include(x => x.Children)
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
        var logs = await dbContext.DownloadTaskTrackFileLogs.OrderBy(x => x.DownloadTaskFileId).ToListAsync(CancellationToken);
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
        var dbContext = IDbContext;
        var track = await dbContext.PlexTracks.Include(x => x.PlexAlbum).ThenInclude(x => x!.PlexArtist)
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
        dbContext.PlexTrackData.Add(alternate);
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexTrackData.Where(x => x.PlexTrackId == track.Id).OrderBy(x => x.Id)
            .Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([original.Id, alternate.Id]);
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
        first.Errors.Count.ShouldBe(0);
        first.Value.ShouldBe(new DownloadTaskCreationReport { MusicTracks = 1 });

        // Act
        var repeated = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicTracksCommand(new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/two"))
        );

        // Assert
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Errors.Count.ShouldBe(0);
        repeated.Value.ShouldBe(new DownloadTaskCreationReport());
        var files = await dbContext.DownloadTaskMusicTrackFiles.ToListAsync(CancellationToken);
        files.Count.ShouldBe(1);
        files.Single().PlexApiMediaId.ShouldBe(alternate.PlexApiMediaId);
        files.Single().DirectoryMeta.DestinationRootPath.ShouldBe("/one");
        (await dbContext.DownloadTaskTrackFileLogs.Select(x => x.DownloadTaskFileId).ToListAsync(CancellationToken))
            .ShouldBe([files.Single().Id]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldHonorFirstSelectorAndLocalFallback_WhenSelectorMediaDataTypeDiffers(
        bool firstDataIdFromAnotherTrack
    )
    {
        // Arrange
        await SetupDatabase(62615, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 2;
        });
        var dbContext = IDbContext;
        var tracks = await dbContext.PlexTracks.Include(x => x.PlexAlbum).ThenInclude(x => x!.PlexArtist)
            .Include(x => x.MediaDataList).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var target = tracks[0];
        var control = tracks[1];
        target.Id.ShouldNotBe(control.Id);
        var automatic = target.MediaDataList.Single();
        var foreignOriginal = control.MediaDataList.Single();
        await dbContext.PlexTrackData.Where(x => x.Id == automatic.Id).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.Quality, VideoQuality.SD), CancellationToken);
        var requested = FakeData.GetPlexMusicTrackMediaData(new Seed(62616))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexTrackId, _ => target.Id)
            .RuleFor(x => x.PlexServerId, _ => target.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => target.PlexLibraryId)
            .RuleFor(x => x.PlexApiRatingKey, _ => target.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => automatic.PlexApiMediaId + 1)
            .RuleFor(x => x.Quality, _ => VideoQuality.Unknown)
            .Generate();
        dbContext.PlexTrackData.Add(requested);
        await dbContext.SaveChangesAsync(CancellationToken);
        var originals = await dbContext.PlexTrackData.Where(x => x.PlexTrackId == target.Id)
            .OrderBy(x => x.Id).ToListAsync(CancellationToken);
        originals.Select(x => (x.Id, x.PlexTrackId, x.Quality)).ShouldBe(
            [(automatic.Id, target.Id, VideoQuality.SD), (requested.Id, target.Id, VideoQuality.Unknown)]);
        foreignOriginal.PlexTrackId.ShouldBe(control.Id);
        (await dbContext.DownloadTaskMusicTrackFiles.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        var expected = firstDataIdFromAnotherTrack ? automatic : requested;
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicTrack,
            PlexServerId = target.PlexServerId,
            PlexLibraryId = target.PlexLibraryId,
            MediaIds = [target.Id],
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaId = control.Id,
                    DataId = automatic.Id,
                    MediaDataType = PlexMediaType.MusicTrack,
                    Quality = VideoQuality.Unknown,
                },
                new PlexMediaQualityDTO
                {
                    MediaId = target.Id,
                    DataId = firstDataIdFromAnotherTrack ? foreignOriginal.Id : requested.Id,
                    MediaDataType = PlexMediaType.MusicArtist,
                    Quality = VideoQuality.Unknown,
                },
                new PlexMediaQualityDTO
                {
                    MediaId = target.Id,
                    DataId = firstDataIdFromAnotherTrack ? requested.Id : automatic.Id,
                    MediaDataType = PlexMediaType.MusicTrack,
                    Quality = VideoQuality.Unknown,
                },
            ],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicTracksCommand(new CreateDownloadTasksRequest([selection])));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { MusicTracks = 1 });
        var artistTask = await dbContext.DownloadTaskMusicArtists.Include(x => x.Children)
            .ThenInclude(x => x.Children).ThenInclude(x => x.Children).SingleAsync(CancellationToken);
        artistTask.PlexApiRatingKey.ShouldBe(target.PlexAlbum!.PlexArtist!.PlexApiRatingKey);
        var albumTask = artistTask.Children.Single();
        albumTask.PlexApiRatingKey.ShouldBe(target.PlexAlbum.PlexApiRatingKey);
        var trackTask = albumTask.Children.Single();
        trackTask.PlexApiRatingKey.ShouldBe(target.PlexApiRatingKey);
        var file = await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken);
        trackTask.Children.Select(x => x.Id).ShouldBe([file.Id]);
        (file.ParentId, file.PlexServerId, file.PlexLibraryId, file.PlexApiRatingKey, file.PlexApiMediaId, file.PlexApiPartId)
            .ShouldBe((trackTask.Id, target.PlexServerId, target.PlexLibraryId, target.PlexApiRatingKey,
                expected.PlexApiMediaId, expected.PlexApiPartId));
        file.FileLocationUrl.ShouldBe(expected.Key);
        file.DataTotal.ShouldBe(expected.Size);
        file.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        var logs = await dbContext.DownloadTaskTrackFileLogs.ToListAsync(CancellationToken);
        logs.Select(x => (x.DownloadTaskFileId, x.DownloadTaskTrackId, x.DownloadTaskAlbumId, x.DownloadTaskArtistId, x.Status))
            .ShouldBe([(file.Id, trackTask.Id, albumTask.Id, artistTask.Id, DownloadStatus.Queued)]);
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
