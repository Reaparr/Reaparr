namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskOtherVideosCommandUnitTests : BaseCommandUnitTest<GenerateDownloadTaskOtherVideosCommand>
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldPersistQueuedHierarchyAndUnresolvedDestinationMetadata_WhenVideoSelected(bool customDestination, bool keepCompleted)
    {
        // Arrange
        await SetupDatabase(62539, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 2;
        });
        var dbContext = IDbContext;
        var sources = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var source = sources[0];
        var data = source.MediaDataList.Single();
        sources[1].Id.ShouldNotBe(source.Id);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        var folder = (await dbContext.GetDestinationFolder(source.PlexLibraryId))!;
        var request = new CreateDownloadTasksRequest([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.OtherVideos, PlexServerId = source.PlexServerId,
                PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id, source.Id], Qualities = [],
                KeepCompletedInDownloadFolder = keepCompleted,
            },
        ], customDestinationFolderPath: customDestination ? "relative/other-video/../target" : string.Empty)
        { DestinationFolderPathId = folder.Id };
        var downloadRoot = (await dbContext.GetDownloadFolder(null)).DirectoryPath;

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand(request));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { OtherVideos = 1 });
        result.Value.Total.ShouldBe(1);
        var task = await dbContext.DownloadTaskOtherVideos.Include(x => x.Children).SingleAsync(CancellationToken);
        task.Id.ShouldNotBe(Guid.Empty);
        task.PlexApiRatingKey.ShouldBe(source.PlexApiRatingKey);
        task.PlexServerId.ShouldBe(source.PlexServerId);
        task.PlexLibraryId.ShouldBe(source.PlexLibraryId);
        task.Title.ShouldBe(source.Title);
        task.FullTitle.ShouldBe(source.FullTitle);
        task.Year.ShouldBe(source.Year);
        task.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        task.DataTotal.ShouldBe(data.Size);
        task.SonarrIntegrationId.ShouldBeNull();
        task.RadarrIntegrationId.ShouldBeNull();
        var file = task.Children.Single();
        file.Id.ShouldNotBe(Guid.Empty);
        file.ParentId.ShouldBe(task.Id);
        file.PlexServerId.ShouldBe(source.PlexServerId);
        file.PlexLibraryId.ShouldBe(source.PlexLibraryId);
        file.PlexApiRatingKey.ShouldBe(data.PlexApiRatingKey);
        file.PlexApiMediaId.ShouldBe(data.PlexApiMediaId);
        file.PlexApiPartId.ShouldBe(data.PlexApiPartId);
        file.FileName.ShouldBe(data.GetFileName);
        file.FileLocationUrl.ShouldBe(data.Key);
        file.FullTitle.ShouldBe($"{source.FullTitle}/{data.GetFileName}");
        file.Quality.ShouldBe(data.VideoResolution);
        file.DataTotal.ShouldBe(data.Size);
        file.DownloadClientType.ShouldBe(PlexDownloadClientType.Direct);
        file.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        file.DirectDownloadSnapshot.ShouldBeNull();
        file.SonarrIntegrationId.ShouldBeNull();
        file.RadarrIntegrationId.ShouldBeNull();
        file.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBe(keepCompleted);
        file.DirectoryMeta.DestinationRootPath.ShouldBe(request.CustomDestinationFolderPath);
        file.DirectoryMeta.DownloadRootPath.ShouldBe(downloadRoot);
        file.DirectoryMeta.OtherVideoFolder.ShouldBe(source.Title.SanitizeFolderName());
        file.DirectoryMeta.MovieFolder.ShouldBe(string.Empty);
        file.DirectoryMeta.TvShowFolder.ShouldBe(string.Empty);
        file.DirectoryMeta.SeasonFolder.ShouldBe(string.Empty);
        file.DirectoryMeta.MusicArtistFolder.ShouldBe(string.Empty);
        file.DirectoryMeta.MusicAlbumFolder.ShouldBe(string.Empty);
        file.DirectoryMeta.PhotoAlbumFolder.ShouldBe(string.Empty);
        file.DestinationFolderPathId.ShouldBe(folder.Id);
        var log = await dbContext.DownloadTaskOtherVideoFileLogs.SingleAsync(CancellationToken);
        log.DownloadTaskFileId.ShouldBe(file.Id);
        log.DownloadTaskOtherVideoId.ShouldBe(task.Id);
        log.Status.ShouldBe(DownloadStatus.Queued);
        log.LogLevel.ShouldBe(NotificationLevel.Information);
        log.Message.ShouldBe($"DownloadTask {file.FileName} was queued for downloading");
        (await dbContext.PlexOtherVideos.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(sources.Select(x => x.Id).Order());
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task ShouldPersistAllPartsOfRequestedOrAutomaticOriginal_WhenMultipleVersionsExist(int selector)
    {
        // Arrange
        await SetupDatabase(62540, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var original = source.MediaDataList.Single();
        var parts = FakeData.GetPlexOtherVideoMediaData(new Seed(62541))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexOtherVideoId, _ => source.Id)
            .RuleFor(x => x.PlexLibraryId, _ => source.PlexLibraryId)
            .RuleFor(x => x.PlexServerId, _ => source.PlexServerId)
            .RuleFor(x => x.PlexApiRatingKey, _ => source.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId + 1)
            .RuleFor(x => x.PartIndex, f => f.IndexFaker)
            .RuleFor(x => x.Quality, _ => VideoQuality.UHD_4K)
            .RuleFor(x => x.VideoResolution, _ => VideoQuality.UHD_4K)
            .RuleFor(x => x.Size, _ => 300).Generate(2);
        parts[0].UpdateInitProperty(nameof(BasePlexMediaData.PlexApiPartId), 71001);
        parts[1].UpdateInitProperty(nameof(BasePlexMediaData.PlexApiPartId), 71002);
        dbContext.PlexOtherVideoData.AddRange(parts);
        await dbContext.SaveChangesAsync(CancellationToken);
        var available = await dbContext.PlexOtherVideoData.Where(x => x.PlexOtherVideoId == source.Id).ToListAsync(CancellationToken);
        available.Count.ShouldBe(3);
        var expectedMediaId = selector == 1 ? parts[1].PlexApiMediaId : available.PickMediaQuality()!.PlexApiMediaId;
        var expected = available.Where(x => x.PlexApiMediaId == expectedMediaId).OrderBy(x => x.PlexApiPartId).ToList();
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.OtherVideos, PlexServerId = source.PlexServerId,
            PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id],
            Qualities = selector == 0 ? [] : [new PlexMediaQualityDTO
            {
                MediaId = source.Id, DataId = selector == 1 ? parts[1].Id : int.MaxValue,
                MediaDataType = PlexMediaType.OtherVideos, Quality = VideoQuality.UHD_4K,
            }],
        };
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand([selection]));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { OtherVideos = 1 });
        var task = await dbContext.DownloadTaskOtherVideos.Include(x => x.Children).SingleAsync(CancellationToken);
        task.DataTotal.ShouldBe(expected.Sum(x => x.Size));
        task.Children.OrderBy(x => x.PlexApiPartId)
            .Select(x => (x.ParentId, x.PlexApiMediaId, x.PlexApiPartId, x.DataTotal, x.FileName, x.FileLocationUrl, x.Quality))
            .ShouldBe(expected.Select(x => (task.Id, x.PlexApiMediaId, x.PlexApiPartId, x.Size, x.GetFileName, x.Key, x.VideoResolution)));
        var logs = await dbContext.DownloadTaskOtherVideoFileLogs.OrderBy(x => x.DownloadTaskFileId).ToListAsync(CancellationToken);
        logs.Select(x => (x.DownloadTaskFileId, x.DownloadTaskOtherVideoId, x.Status, x.LogLevel))
            .ShouldBe(task.Children.OrderBy(x => x.Id).Select(x => (x.Id, task.Id, DownloadStatus.Queued, NotificationLevel.Information)));
    }

    [Test]
    public async Task ShouldSkipExistingRootWithoutAddingFilesOrLogs_WhenDestinationChanges()
    {
        // Arrange
        await SetupDatabase(62545, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.SingleAsync(CancellationToken);
        var existing = source.MapToDownloadTask(null);
        existing.DownloadStatus = DownloadStatus.Completed;
        dbContext.DownloadTaskOtherVideos.Add(existing);
        await dbContext.SaveChangesAsync(CancellationToken);
        existing.Id.ShouldNotBe(Guid.Empty);
        (await dbContext.DownloadTaskOtherVideoFiles.CountAsync(CancellationToken)).ShouldBe(0);
        var request = new CreateDownloadTasksRequest([new DownloadMediaDTO
        {
            Type = PlexMediaType.OtherVideos, PlexServerId = source.PlexServerId,
            PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id], Qualities = [],
        }], customDestinationFolderPath: "a-new-destination");

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand(request));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport());
        var root = await dbContext.DownloadTaskOtherVideos.SingleAsync(CancellationToken);
        root.Id.ShouldBe(existing.Id);
        root.DownloadStatus.ShouldBe(DownloadStatus.Completed);
        (await dbContext.DownloadTaskOtherVideoFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task ShouldRejectWrongLibraryServerOrPartOwnershipWithoutPersisting_WhenOwnershipMismatches(int mismatch)
    {
        // Arrange
        await SetupDatabase(62542, config =>
        {
            config.PlexServerCount = 2;
            config.PlexOtherVideoLibraryCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var otherLibrary = await dbContext.PlexLibraries.Where(x => x.Id != source.PlexLibraryId && x.Type == PlexMediaType.OtherVideos)
            .OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var movieLibrary = await dbContext.PlexLibraries.Where(x => x.Type == PlexMediaType.Movie).OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var foreignServerId = await dbContext.PlexServers.Where(x => x.Id != source.PlexServerId).Select(x => x.Id).SingleAsync(CancellationToken);
        if (mismatch == 3)
        {
            await dbContext.PlexOtherVideoData.Where(x => x.PlexOtherVideoId == source.Id).ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.PlexServerId, foreignServerId), CancellationToken);
        }
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.OtherVideos,
            PlexServerId = mismatch == 0 ? foreignServerId : mismatch == 2 ? movieLibrary.PlexServerId : source.PlexServerId,
            PlexLibraryId = mismatch == 1 ? otherLibrary.Id : mismatch == 2 ? movieLibrary.Id : source.PlexLibraryId,
            MediaIds = [source.Id], Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand([selection]));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.PlexOtherVideos.SingleAsync(x => x.Id == source.Id, CancellationToken)).PlexLibraryId.ShouldBe(source.PlexLibraryId);
    }

    [Test]
    [Arguments(IntegrationType.Radarr)]
    [Arguments(IntegrationType.Sonarr)]
    public async Task ShouldRejectUnsupportedIntegrationWithoutPersisting_WhenIntegrationRequested(IntegrationType integrationType)
    {
        // Arrange
        await SetupDatabase(62538, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.SingleAsync(CancellationToken);
        var request = new CreateDownloadTasksRequest([new DownloadMediaDTO
        {
            Type = PlexMediaType.OtherVideos, PlexServerId = source.PlexServerId,
            PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id], Qualities = [],
        }], integration: new IntegrationIdentity(integrationType, Guid.NewGuid()));
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand(request));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnEmptyFailureWithoutTasks_WhenNoOtherVideoSelectionRequested()
    {
        // Arrange
        await SetupDatabase(62543, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.SingleAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.Movie, PlexServerId = source.PlexServerId,
            PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id], Qualities = [],
        };
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand([selection]));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnZeroReportWithoutTasks_WhenSelectedVideoHasNoMediaData()
    {
        // Arrange
        await SetupDatabase(62546, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.SingleAsync(CancellationToken);
        await dbContext.PlexOtherVideoData.Where(x => x.PlexOtherVideoId == source.Id).ExecuteDeleteAsync(CancellationToken);
        (await dbContext.PlexOtherVideoData.CountAsync(CancellationToken)).ShouldBe(0);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.OtherVideos, PlexServerId = source.PlexServerId,
            PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id], Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand([selection]));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldCancelWithoutPersisting_WhenTokenAlreadyCancelled()
    {
        // Arrange
        await SetupDatabase(62547, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.SingleAsync(CancellationToken);
        var command = new GenerateDownloadTaskOtherVideosCommand([new DownloadMediaDTO
        {
            Type = PlexMediaType.OtherVideos, PlexServerId = source.PlexServerId,
            PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id], Qualities = [],
        }]);
        var validation = await new GenerateDownloadTaskOtherVideosCommandValidator().ValidateAsync(command, CancellationToken);
        validation.IsValid.ShouldBeTrue();
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);

        // Act
        var result = await new GenerateDownloadTaskOtherVideosCommandHandler(Serilog.Log.Logger, dbContext)
            .ExecuteAsync(command, new CancellationToken(true));

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldIgnoreMovieSelection_WhenRequestIncludesOtherFamilies()
    {
        // Arrange
        await SetupDatabase(62544, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
        });
        var dbContext = IDbContext;
        var source = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var movie = await dbContext.PlexMovies.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var request = new CreateDownloadTasksRequest([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Movie, PlexServerId = movie.PlexServerId,
                PlexLibraryId = movie.PlexLibraryId, MediaIds = [movie.Id], Qualities = [],
            },
            new DownloadMediaDTO
            {
                Type = PlexMediaType.OtherVideos, PlexServerId = source.PlexServerId,
                PlexLibraryId = source.PlexLibraryId, MediaIds = [source.Id], Qualities = [],
            },
        ]);
        (await dbContext.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskOtherVideosCommand(request));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { OtherVideos = 1 });
        var root = await dbContext.DownloadTaskOtherVideos.Include(x => x.Children).SingleAsync(CancellationToken);
        root.PlexApiRatingKey.ShouldBe(source.PlexApiRatingKey);
        root.PlexServerId.ShouldBe(source.PlexServerId);
        root.PlexLibraryId.ShouldBe(source.PlexLibraryId);
        root.Children.Single().PlexApiPartId.ShouldBe(source.MediaDataList.Single().PlexApiPartId);
        (await dbContext.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(0);
        var log = await dbContext.DownloadTaskOtherVideoFileLogs.SingleAsync(CancellationToken);
        log.DownloadTaskOtherVideoId.ShouldBe(root.Id);
        log.DownloadTaskFileId.ShouldBe(root.Children.Single().Id);
    }
}
