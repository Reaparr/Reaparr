using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTasksUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldAssignExactHierarchyRelationships_WithoutChangingControl_WhenMappingNewDownloadTasks(
        bool music
    )
    {
        // Arrange
        await SetupDatabase(
            62537,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        DownloadTaskFileBase target = music
            ? await FakeData.AddMusicTask(dbContext, 1)
            : await FakeData.AddOtherVideoTask(dbContext, 1);
        DownloadTaskFileBase control = music
            ? await FakeData.AddMusicTask(dbContext, 2)
            : await FakeData.AddOtherVideoTask(dbContext, 2);
        DownloadTaskBase[] hierarchy = music
            ?
            [
                ((DownloadTaskMusicTrackFile)target).Parent!.Parent!.Parent!,
                ((DownloadTaskMusicTrackFile)target).Parent!.Parent!,
                ((DownloadTaskMusicTrackFile)target).Parent!,
                target,
            ]
            : [((DownloadTaskOtherVideoFile)target).Parent!, target];
        var beforeIds = hierarchy.Select(x => x.Id).ToArray();
        var controlScope = (control.PlexServerId, control.PlexLibraryId);
        hierarchy.ShouldAllBe(x =>
            x.PlexServerId == controlScope.PlexServerId && x.PlexLibraryId == controlScope.PlexLibraryId
        );
        ICollection<DownloadTaskBase> roots = [hierarchy[0]];

        // Act
        roots.SetRelationshipIds(99, 88);

        // Assert
        hierarchy.Select(x => (x.Id, x.PlexServerId, x.PlexLibraryId)).ShouldBe(beforeIds.Select(id => (id, 99, 88)));
        (control.PlexServerId, control.PlexLibraryId).ShouldBe(controlScope);
        target.ToParentKey()!.Id.ShouldBe(hierarchy[^2].Id);
    }

    [Test]
    public async Task ShouldPersistExactMusicAndOtherVideoLogs_WhenUsingExistingBatchLogWorkflow()
    {
        // Arrange
        await SetupDatabase(
            62536,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var music = await FakeData.AddMusicTask(dbContext, 1);
        var video = await FakeData.AddOtherVideoTask(dbContext, 1);
        (await dbContext.DownloadTaskTrackFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        var createdAt = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        DownloadTaskLogBase[] logs =
        [
            new DownloadTaskTrackFileLog
            {
                DownloadTaskFileId = music.Id,
                DownloadTaskTrackId = music.ParentId,
                DownloadTaskAlbumId = music.Parent!.ParentId,
                DownloadTaskArtistId = music.Parent.Parent!.ParentId,
                Message = "music-batch",
                Status = DownloadStatus.Downloading,
                LogLevel = NotificationLevel.Information,
                CreatedAt = createdAt,
            },
            new DownloadTaskOtherVideoFileLog
            {
                DownloadTaskFileId = video.Id,
                DownloadTaskOtherVideoId = video.ParentId,
                Message = "video-batch",
                Status = DownloadStatus.Downloading,
                LogLevel = NotificationLevel.Information,
                CreatedAt = createdAt,
            },
        ];

        // Act
        await dbContext.CreateDownloadClientLogs(logs, CancellationToken);

        // Assert
        var musicLog = await dbContext.DownloadTaskTrackFileLogs.SingleAsync(CancellationToken);
        (
            musicLog.DownloadTaskFileId,
            musicLog.DownloadTaskTrackId,
            musicLog.DownloadTaskAlbumId,
            musicLog.DownloadTaskArtistId
        ).ShouldBe((music.Id, music.ParentId, music.Parent.ParentId, music.Parent.Parent.ParentId));
        (musicLog.Message, musicLog.Status, musicLog.LogLevel, musicLog.CreatedAt).ShouldBe(
            ("music-batch", DownloadStatus.Downloading, NotificationLevel.Information, createdAt)
        );
        var videoLog = await dbContext.DownloadTaskOtherVideoFileLogs.SingleAsync(CancellationToken);
        (
            videoLog.DownloadTaskFileId,
            videoLog.DownloadTaskOtherVideoId,
            videoLog.Message,
            videoLog.Status,
            videoLog.LogLevel,
            videoLog.CreatedAt
        ).ShouldBe(
            (
                video.Id,
                video.ParentId,
                "video-batch",
                DownloadStatus.Downloading,
                NotificationLevel.Information,
                createdAt
            )
        );
        (await dbContext.DownloadTaskMovieFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskTvShowEpisodeFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public async Task ShouldRemoveOnlyEmptyNewMediaAncestors_WhenCleaningByServerOrGlobally(bool music, bool byServer)
    {
        // Arrange
        await SetupDatabase(
            62533,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        DownloadTaskFileBase target = music
            ? await FakeData.AddMusicTask(dbContext, 1)
            : await FakeData.AddOtherVideoTask(dbContext, 1);
        DownloadTaskFileBase control = music
            ? await FakeData.AddMusicTask(dbContext, 2)
            : await FakeData.AddOtherVideoTask(dbContext, 2);
        var targetRoot = (
            await dbContext.GetRootDownloadTaskKeyAsync(target.ToKey(), cancellationToken: CancellationToken)
        )!;
        var controlRoot = (
            await dbContext.GetRootDownloadTaskKeyAsync(control.ToKey(), cancellationToken: CancellationToken)
        )!;
        (await dbContext.GetDownloadTaskKeysAsync([targetRoot.Id, controlRoot.Id], CancellationToken))
            .Select(x => x.Id)
            .Order()
            .ShouldBe(new[] { targetRoot.Id, controlRoot.Id }.Order());
        if (music)
            await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == target.Id)
                .ExecuteDeleteAsync(CancellationToken);
        else
            await dbContext
                .DownloadTaskOtherVideoFiles.Where(x => x.Id == target.Id)
                .ExecuteDeleteAsync(CancellationToken);

        // Act
        var count = byServer
            ? await dbContext.DeleteOrphanedParentTasksByServerIdAsync(target.PlexServerId, CancellationToken)
            : await dbContext.DeleteOrphanedParentTasksAsync(CancellationToken);

        // Assert
        count.ShouldBe(music ? 3 : 1);
        (await dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken))
            .Select(x => x.Id)
            .ShouldBe([controlRoot.Id]);
        (await dbContext.GetDownloadTaskKeyAsync(targetRoot.Id, CancellationToken)).ShouldBeNull();
        (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!.Id.ShouldBe(control.Id);
        if (music)
        {
            (
                await dbContext
                    .DownloadTaskMusicTracks.Select(x => new { x.Id, x.ParentId })
                    .ToListAsync(CancellationToken)
            )
                .Select(x => (x.Id, x.ParentId))
                .ShouldBe([
                    (
                        ((DownloadTaskMusicTrackFile)control).ParentId,
                        ((DownloadTaskMusicTrackFile)control).Parent!.ParentId
                    ),
                ]);
            (await dbContext.DownloadTaskMusicAlbums.Select(x => x.ParentId).ToListAsync(CancellationToken)).ShouldBe([
                controlRoot.Id,
            ]);
        }
    }

    [Test]
    [Arguments(DownloadTaskType.MusicTrackData)]
    [Arguments(DownloadTaskType.MusicTrackPart)]
    [Arguments(DownloadTaskType.OtherVideoData)]
    [Arguments(DownloadTaskType.OtherVideoPart)]
    public async Task ShouldLeaveExactFileStatesUnchanged_WhenStatusUpdateIsCancelled(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(
            62534,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var music = type is DownloadTaskType.MusicTrackData or DownloadTaskType.MusicTrackPart;
        DownloadTaskFileBase target = music
            ? await FakeData.AddMusicTask(dbContext, 1)
            : await FakeData.AddOtherVideoTask(dbContext, 1);
        DownloadTaskFileBase control = music
            ? await FakeData.AddMusicTask(dbContext, 2)
            : await FakeData.AddOtherVideoTask(dbContext, 2);
        var key = target.ToKey() with { Type = type };
        (await dbContext.GetDownloadStatusAsync(key)).ShouldBe(DownloadStatus.Queued);
        (await dbContext.GetDownloadStatusAsync(control.ToKey())).ShouldBe(DownloadStatus.Queued);

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() =>
            dbContext.SetDownloadStatus(key, DownloadStatus.Error)
        );

        // Assert
        (await dbContext.GetDownloadStatusAsync(key)).ShouldBe(DownloadStatus.Queued);
        (await dbContext.GetDownloadStatusAsync(control.ToKey())).ShouldBe(DownloadStatus.Queued);
        (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!
            .ToParentKey()!
            .Id.ShouldBe(target.ToParentKey()!.Id);
    }

    [Test]
    [Arguments(DownloadTaskType.MusicTrackData)]
    [Arguments(DownloadTaskType.MusicTrackPart)]
    [Arguments(DownloadTaskType.OtherVideoData)]
    [Arguments(DownloadTaskType.OtherVideoPart)]
    public async Task ShouldPersistOnlySelectedFileAndAggregateItsAncestors_WhenUpdatingNewMediaProgress(
        DownloadTaskType type
    )
    {
        // Arrange
        await SetupDatabase(
            62531,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var music = type is DownloadTaskType.MusicTrackData or DownloadTaskType.MusicTrackPart;
        DownloadTaskFileBase target = music
            ? await FakeData.AddMusicTask(dbContext, 1)
            : await FakeData.AddOtherVideoTask(dbContext, 1);
        DownloadTaskFileBase control = music
            ? await FakeData.AddMusicTask(dbContext, 2)
            : await FakeData.AddOtherVideoTask(dbContext, 2);
        var key = target.ToKey() with { Type = type };
        var canonicalKey = target.ToKey();
        var rootKey = music
            ? ((DownloadTaskMusicTrackFile)target).Parent!.Parent!.Parent!.ToKey()
            : ((DownloadTaskOtherVideoFile)target).Parent!.ToKey();
        var ancestorKeys = music
            ? new[]
            {
                ((DownloadTaskMusicTrackFile)target).Parent!.ToKey(),
                ((DownloadTaskMusicTrackFile)target).Parent!.Parent!.ToKey(),
                rootKey,
            }
            : new[] { rootKey };
        (await dbContext.GetDownloadTaskFileAsync(canonicalKey, CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!.DataReceived.ShouldBe(0);
        target.DataTotal = 1000;
        target.DataReceived = 250;
        target.DownloadSpeed = 50;
        target.Percentage = 25;
        target.TimeRemaining = 15;
        target.FileTransferSpeed = 100;
        target.FileDataTransferred = 500;
        target.CurrentFileTransferBytesOffset = 500;
        var snapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 25,
            Status = 1,
            Urls = ["/file"],
            TotalFileSize = 1000,
            FileName = target.FileName,
            DownloadingFileExtension = ".part",
            Chunks = [],
            IsSupportDownloadInRange = true,
        };

        // Act
        await dbContext.UpdateDownloadProgress(key, target, snapshot, CancellationToken);
        await dbContext.UpdateDownloadFileTransferProgress(key, target, CancellationToken);
        await dbContext.SetDownloadStatus(key, DownloadStatus.Error);
        var changed = await dbContext.DetermineDownloadStatus(key, CancellationToken);

        // Assert
        changed.ShouldBe(ancestorKeys);
        (await dbContext.GetDownloadTaskKeyAsync(target.Id, CancellationToken)).ShouldBe(canonicalKey);
        (await dbContext.GetDownloadTaskTypeAsync(target.Id, CancellationToken)).ShouldBe(canonicalKey.Type);
        (await dbContext.GetRootDownloadTaskKeyAsync(key, cancellationToken: CancellationToken)).ShouldBe(rootKey);
        (await dbContext.GetAffectedRootDownloadTaskIdsAsync([target.Id], CancellationToken)).ShouldBe([rootKey.Id]);
        (await dbContext.GetDownloadableChildTaskKeys(rootKey, CancellationToken)).ShouldBe([canonicalKey]);
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        (persisted.DataReceived, persisted.DataTotal, persisted.DownloadSpeed, persisted.Percentage).ShouldBe(
            (250L, 1000L, 50L, 50m)
        );
        (
            persisted.FileTransferSpeed,
            persisted.FileDataTransferred,
            persisted.CurrentFileTransferBytesOffset,
            persisted.TimeRemaining
        ).ShouldBe((100L, 500L, 500L, 15));
        persisted.DirectDownloadSnapshot.ShouldNotBeNull();
        (
            persisted.DirectDownloadSnapshot.SaveProgress,
            persisted.DirectDownloadSnapshot.TotalFileSize,
            persisted.DirectDownloadSnapshot.FileName
        ).ShouldBe((25d, 1000L, target.FileName));
        persisted.DirectDownloadSnapshot.Urls.ShouldBe(["/file"]);
        foreach (var ancestor in ancestorKeys)
        {
            (await dbContext.GetDownloadStatusAsync(ancestor)).ShouldBe(DownloadStatus.Error);
            (await dbContext.GetDownloadTaskStatusAsync(ancestor, CancellationToken)).ShouldBe(DownloadStatus.Error);
        }
        var detail = (await dbContext.GetDownloadTaskAsync(rootKey, CancellationToken))!;
        detail.DataTotal.ShouldBe(1000);
        detail.DataReceived.ShouldBe(250);
        var progress = (
            await dbContext.GetDownloadProgressTasksByServerAsync(target.PlexServerId, CancellationToken)
        ).Single(x => x.Id == rootKey.Id);
        (progress.DataTotal, progress.DataReceived, progress.DownloadStatus).ShouldBe(
            (1000L, 250L, DownloadStatus.Error)
        );
        var beforeReset = persisted;
        await dbContext.ClearDownloadSpeed(key, CancellationToken);
        persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        (persisted.DownloadSpeed, persisted.TimeRemaining, persisted.FileTransferSpeed).ShouldBe((0L, 0, 100L));
        var reset = await dbContext.ResetDownloadTaskProgress(key, DownloadStatus.Stopped, CancellationToken);
        reset.IsSuccess.ShouldBeTrue();
        reset.Errors.Count.ShouldBe(0);
        persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        (persisted.DataReceived, persisted.DownloadSpeed, persisted.Percentage, persisted.TimeRemaining).ShouldBe(
            (0L, 0L, 0m, 0)
        );
        (persisted.FileTransferSpeed, persisted.FileDataTransferred, persisted.CurrentFileTransferBytesOffset).ShouldBe(
            (0L, 0L, 0L)
        );
        persisted.DirectDownloadSnapshot.ShouldBeNull();
        (
            persisted.Id,
            persisted.ToParentKey()!.Id,
            persisted.FileName,
            persisted.FileLocationUrl,
            persisted.DataTotal,
            persisted.DownloadStatus
        ).ShouldBe(
            (
                target.Id,
                beforeReset.ToParentKey()!.Id,
                beforeReset.FileName,
                beforeReset.FileLocationUrl,
                1000L,
                DownloadStatus.Stopped
            )
        );
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        (
            retained.Id,
            retained.ToParentKey()!.Id,
            retained.DataReceived,
            retained.DownloadStatus,
            retained.DirectDownloadSnapshot
        ).ShouldBe((control.Id, control.ToParentKey()!.Id, 0L, DownloadStatus.Queued, (DirectDownloadSnapshot?)null));
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldResolveEveryHierarchyKeyAndDeleteOnlySelectedEmptyRoot_WhenCleaningNewMedia(bool music)
    {
        // Arrange
        await SetupDatabase(
            62532,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        DownloadTaskFileBase target = music
            ? await FakeData.AddMusicTask(dbContext, 1)
            : await FakeData.AddOtherVideoTask(dbContext, 1);
        DownloadTaskFileBase control = music
            ? await FakeData.AddMusicTask(dbContext, 2)
            : await FakeData.AddOtherVideoTask(dbContext, 2);
        var root = music
            ? ((DownloadTaskMusicTrackFile)target).Parent!.Parent!.Parent!.ToKey()
            : ((DownloadTaskOtherVideoFile)target).Parent!.ToKey();
        var controlRoot = music
            ? ((DownloadTaskMusicTrackFile)control).Parent!.Parent!.Parent!.ToKey()
            : ((DownloadTaskOtherVideoFile)control).Parent!.ToKey();
        var keys = music
            ? new[]
            {
                root,
                ((DownloadTaskMusicTrackFile)target).Parent!.Parent!.ToKey(),
                ((DownloadTaskMusicTrackFile)target).Parent!.ToKey(),
                target.ToKey(),
            }
            : new[] { root, target.ToKey() };
        var found = await dbContext.GetDownloadTaskKeysAsync(keys.Select(x => x.Id).ToList(), CancellationToken);
        found.OrderBy(x => x.Id).ShouldBe(keys.OrderBy(x => x.Id));
        foreach (var key in keys)
        {
            (await dbContext.GetRootDownloadTaskKeyAsync(key, cancellationToken: CancellationToken)).ShouldBe(root);
            (await dbContext.GetDownloadTaskAsync(key, CancellationToken))!.Id.ShouldBe(key.Id);
            (await dbContext.GetDownloadTaskKeyAsync(key.Id, CancellationToken)).ShouldBe(key);
        }
        if (music)
            await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == target.Id)
                .ExecuteDeleteAsync(CancellationToken);
        else
            await dbContext
                .DownloadTaskOtherVideoFiles.Where(x => x.Id == target.Id)
                .ExecuteDeleteAsync(CancellationToken);

        // Act
        var deleted = await dbContext.DeleteOrphanedParentTasksByRootIdsAsync([root.Id], CancellationToken);

        // Assert
        deleted.ShouldBe(music ? 3 : 1);
        (await dbContext.GetDownloadTaskKeyAsync(root.Id, CancellationToken)).ShouldBeNull();
        (await dbContext.GetDownloadTaskKeyAsync(controlRoot.Id, CancellationToken)).ShouldBe(controlRoot);
        (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!
            .ToParentKey()!
            .Id.ShouldBe(control.ToParentKey()!.Id);
        (await dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken))
            .Select(x => x.Id)
            .ShouldBe([controlRoot.Id]);
    }

    [Test]
    [Arguments(DownloadTaskType.MoviePart, false)]
    [Arguments(DownloadTaskType.MoviePart, true)]
    [Arguments(DownloadTaskType.EpisodePart, false)]
    [Arguments(DownloadTaskType.EpisodePart, true)]
    public async Task ShouldPersistPartStatusOnlyWhenNotCancelled_WhenSettingStatus(
        DownloadTaskType type,
        bool cancelled
    )
    {
        // Arrange
        await SetupDatabase(
            226687,
            config =>
            {
                config.MovieDownloadTasksCount = 2;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        using var dbContext = IDbContext;
        DownloadTaskFileBase target =
            type == DownloadTaskType.MoviePart
                ? await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).FirstAsync(CancellationToken)
                : await dbContext.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var statuses = dbContext
            .DownloadTaskMovieFile.Select(x => new { x.Id, x.DownloadStatus })
            .Concat(dbContext.DownloadTaskTvShowEpisodeFile.Select(x => new { x.Id, x.DownloadStatus }))
            .OrderBy(x => x.Id);
        var before = await statuses.ToListAsync(CancellationToken);
        before.Single(x => x.Id == target.Id).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        var key = target.ToKey() with { Type = type };
        var token = cancelled ? new CancellationToken(true) : CancellationToken;

        // Act
        if (cancelled)
            await Should.ThrowAsync<OperationCanceledException>(() =>
                dbContext.SetDownloadStatus(key, DownloadStatus.Error)
            );
        else
            await dbContext.SetDownloadStatus(key, DownloadStatus.Error);

        // Assert
        var after = await statuses.ToListAsync(CancellationToken);
        after
            .Select(x => (x.Id, x.DownloadStatus))
            .ShouldBe(
                before.Select(x => (x.Id, !cancelled && x.Id == target.Id ? DownloadStatus.Error : x.DownloadStatus))
            );
    }

    [Test]
    public async Task ShouldSetTheDownloadTaskParentOfTypeMovieDataToDownloadFinished_WhenTheMovieDataIsDownloadStatusIsDownloadFinished()
    {
        // Arrange
        await SetupDatabase(
            77674,
            config =>
            {
                config.MovieDownloadTasksCount = 5;
            }
        );

        using var dbContext = IDbContext;
        var downloadTasks = await dbContext.DownloadTaskMovie.Include(x => x.Children).ToListAsync(CancellationToken);
        var targetMovie = downloadTasks.First();
        var testDownloadTask = targetMovie.Children.First();
        var before = downloadTasks.Select(x => (x.Id, x.DownloadStatus)).OrderBy(x => x.Id).ToArray();
        await dbContext.SetDownloadStatus(testDownloadTask.ToKey(), DownloadStatus.DownloadFinished);

        // Act
        var changedKeys = await dbContext.DetermineDownloadStatus(testDownloadTask.ToKey(), CancellationToken);

        // Assert
        changedKeys.ShouldBe([targetMovie.ToKey()]);
        var after = await dbContext
            .DownloadTaskMovie.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.DownloadStatus })
            .ToListAsync(CancellationToken);
        after
            .Select(x => (x.Id, x.DownloadStatus))
            .ShouldBe(
                before.Select(x => (x.Id, x.Id == targetMovie.Id ? DownloadStatus.DownloadFinished : x.DownloadStatus))
            );
    }

    [Test]
    public async Task ShouldSetTheDownloadTaskParentOfTypeEpisodeDataToError_WhenTheEpisodeDataIsDownloadStatusIsError()
    {
        // Arrange
        await SetupDatabase(
            864828,
            config =>
            {
                config.TvShowDownloadTasksCount = 5;
                config.TvShowSeasonDownloadTasksCount = 5;
                config.TvShowEpisodeDownloadTasksCount = 5;
            }
        );

        using var dbContext = IDbContext;
        var downloadTasks = await dbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        var targetShow = downloadTasks.ElementAt(3);
        var targetSeason = targetShow.Children.ElementAt(2);
        var targetEpisode = targetSeason.Children.ElementAt(3);
        var downloadTaskTvShowEpisodeFile = targetEpisode.Children.ElementAt(0);
        var before = downloadTasks
            .Cast<DownloadTaskBase>()
            .Concat(downloadTasks.SelectMany(x => x.Children))
            .Concat(downloadTasks.SelectMany(x => x.Children).SelectMany(x => x.Children))
            .Select(x => (x.Id, x.DownloadStatus))
            .OrderBy(x => x.Id)
            .ToArray();
        var expectedChangedKeys = new[] { targetEpisode.ToKey(), targetSeason.ToKey(), targetShow.ToKey() };
        await dbContext.SetDownloadStatus(downloadTaskTvShowEpisodeFile.ToKey(), DownloadStatus.Error);

        // Act
        var changedKeys = await dbContext.DetermineDownloadStatus(
            downloadTaskTvShowEpisodeFile.ToKey(),
            CancellationToken
        );

        // Assert
        changedKeys.ShouldBe(expectedChangedKeys);
        var after = await dbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        after
            .Cast<DownloadTaskBase>()
            .Concat(after.SelectMany(x => x.Children))
            .Concat(after.SelectMany(x => x.Children).SelectMany(x => x.Children))
            .Select(x => (x.Id, x.DownloadStatus))
            .OrderBy(x => x.Id)
            .ShouldBe(
                before.Select(x =>
                    (x.Id, expectedChangedKeys.Any(key => key.Id == x.Id) ? DownloadStatus.Error : x.DownloadStatus)
                )
            );
    }

    [Test]
    public async Task ShouldReturnDownloadTaskTypeMovie_WhenTheGuidIsOfTypeDownloadTaskMovie()
    {
        // Arrange
        await SetupDatabase(81434, config => config.MovieDownloadTasksCount = 5);
        var downloadTasks = await IDbContext.DownloadTaskMovie.ToListAsync(CancellationToken);
        var testDownloadTask = downloadTasks[2];

        // Act
        var downloadTaskType = await IDbContext.GetDownloadTaskTypeAsync(testDownloadTask.Id, CancellationToken);

        // Assert
        downloadTaskType.ShouldBe(DownloadTaskType.Movie);
    }

    [Test]
    public async Task ShouldReturnDownloadTaskTypeTvShow_WhenTheGuidIsOfTypeDownloadTaskTvShow()
    {
        // Arrange
        await SetupDatabase(91671, config => config.TvShowDownloadTasksCount = 2);
        var downloadTasks = await IDbContext.DownloadTaskTvShow.ToListAsync(CancellationToken);
        var testDownloadTask = downloadTasks[1];

        // Act
        var downloadTaskType = await IDbContext.GetDownloadTaskTypeAsync(testDownloadTask.Id, CancellationToken);

        // Assert
        downloadTaskType.ShouldBe(DownloadTaskType.TvShow);
    }

    [Test]
    public async Task ShouldReturnDownloadTaskTypeSeason_WhenTheGuidIsOfTypeDownloadTaskTvShowSeason()
    {
        // Arrange
        await SetupDatabase(48398, config => config.TvShowDownloadTasksCount = 3);
        var downloadTasks = await IDbContext.DownloadTaskTvShowSeason.ToListAsync(CancellationToken);
        var testDownloadTask = downloadTasks[2];

        // Act
        var downloadTaskType = await IDbContext.GetDownloadTaskTypeAsync(testDownloadTask.Id, CancellationToken);

        // Assert
        downloadTaskType.ShouldBe(DownloadTaskType.Season);
    }

    [Test]
    public async Task ShouldReturnDownloadTaskTypeEpisode_WhenTheGuidIsOfTypeDownloadTaskTvShowEpisode()
    {
        // Arrange
        await SetupDatabase(74950, config => config.TvShowDownloadTasksCount = 2);
        var downloadTasks = await IDbContext.DownloadTaskTvShowEpisode.ToListAsync(CancellationToken);
        var testDownloadTask = downloadTasks[0];

        // Act
        var downloadTaskType = await IDbContext.GetDownloadTaskTypeAsync(testDownloadTask.Id, CancellationToken);

        // Assert
        downloadTaskType.ShouldBe(DownloadTaskType.Episode);
    }

    [Test]
    [Arguments(DownloadTaskType.Movie)]
    [Arguments(DownloadTaskType.TvShow)]
    [Arguments(DownloadTaskType.Season)]
    [Arguments(DownloadTaskType.Episode)]
    public async Task ShouldReturnError_WhenUnsupportedDownloadTaskTypeProvided(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(
            81238,
            config =>
            {
                config.MovieDownloadTasksCount = 5;
                config.TvShowDownloadTasksCount = 5;
                config.TvShowSeasonDownloadTasksCount = 5;
                config.TvShowEpisodeDownloadTasksCount = 5;
            }
        );

        var dbContext = IDbContext;
        DownloadTaskKey? key;
        switch (type)
        {
            case DownloadTaskType.Movie:
                key = await dbContext.DownloadTaskMovie.ProjectToKey().FirstOrDefaultAsync(CancellationToken);
                break;
            case DownloadTaskType.TvShow:
                key = await dbContext.DownloadTaskTvShow.ProjectToKey().FirstOrDefaultAsync(CancellationToken);
                break;
            case DownloadTaskType.Season:
                key = await dbContext.DownloadTaskTvShowSeason.ProjectToKey().FirstOrDefaultAsync(CancellationToken);
                break;
            case DownloadTaskType.Episode:
                key = await dbContext.DownloadTaskTvShowEpisode.ProjectToKey().FirstOrDefaultAsync(CancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        key.ShouldNotBeNull();

        // Act
        var result = await dbContext.ResetDownloadTaskProgress(key, DownloadStatus.Stopped, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Message.Contains("not supported"));
    }

    [Test]
    public async Task ShouldReturnResetDownloadTaskTypeMovieFile_WhenResetDownloadTaskProgressCalled()
    {
        // Arrange
        await SetupDatabase(43481, config => config.MovieDownloadTasksCount = 5);
        var dbContext = IDbContext;
        var downloadTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        var testDownloadTask = downloadTasks[2];

        testDownloadTask.DataTotal = 5000;
        testDownloadTask.DownloadSpeed = 50;
        testDownloadTask.DataReceived = 50;
        testDownloadTask.FileTransferSpeed = 50;
        testDownloadTask.FileDataTransferred = 50;
        testDownloadTask.CurrentFileTransferBytesOffset = 50;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var resetResult = await IDbContext.ResetDownloadTaskProgress(
            testDownloadTask.ToKey(),
            DownloadStatus.Stopped,
            CancellationToken
        );

        // Assert
        resetResult.IsSuccess.ShouldBeTrue();
        var downloadTaskDb = await dbContext.GetDownloadTaskFileAsync(testDownloadTask.ToKey(), CancellationToken);
        downloadTaskDb.ShouldNotBeNull();
        downloadTaskDb.DownloadSpeed.ShouldBe(0);
        downloadTaskDb.DataReceived.ShouldBe(0);
        downloadTaskDb.FileTransferSpeed.ShouldBe(0);
        downloadTaskDb.FileDataTransferred.ShouldBe(0);
        downloadTaskDb.CurrentFileTransferBytesOffset.ShouldBe(0);
        downloadTaskDb.DataTotal.ShouldBe(5000);
        downloadTaskDb.DownloadStatus.ShouldBe(DownloadStatus.Stopped);

        downloadTaskDb.FileName.ShouldBe(testDownloadTask.FileName);
        downloadTaskDb.DownloadDirectory.ShouldBe(testDownloadTask.DownloadDirectory);
        downloadTaskDb.DestinationDirectory.ShouldBe(testDownloadTask.DestinationDirectory);
        downloadTaskDb.FileLocationUrl.ShouldBe(testDownloadTask.FileLocationUrl);
        downloadTaskDb.CreatedAt.ShouldBe(testDownloadTask.CreatedAt);
        downloadTaskDb.Quality.ShouldBe(testDownloadTask.Quality);
        downloadTaskDb.IsDownloadable.ShouldBe(testDownloadTask.IsDownloadable);
        downloadTaskDb.MediaType.ShouldBe(testDownloadTask.MediaType);
        downloadTaskDb.FullTitle.ShouldBe(testDownloadTask.FullTitle);
        downloadTaskDb.PlexServerId.ShouldBe(testDownloadTask.PlexServerId);
        downloadTaskDb.PlexLibraryId.ShouldBe(testDownloadTask.PlexLibraryId);
    }

    [Test]
    public async Task ShouldReturnResetDownloadTaskTypeEpisodeFile_WhenResetDownloadTaskProgressCalled()
    {
        // Arrange
        await SetupDatabase(
            8231434,
            config =>
            {
                config.TvShowDownloadTasksCount = 2;
                config.TvShowSeasonDownloadTasksCount = 2;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var downloadTasks = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().ToListAsync(CancellationToken);
        var testDownloadTask = downloadTasks[4];

        testDownloadTask.DataTotal = 5000;
        testDownloadTask.DownloadSpeed = 50;
        testDownloadTask.DataReceived = 50;
        testDownloadTask.FileTransferSpeed = 50;
        testDownloadTask.FileDataTransferred = 50;
        testDownloadTask.CurrentFileTransferBytesOffset = 50;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var resetResult = await IDbContext.ResetDownloadTaskProgress(
            testDownloadTask.ToKey(),
            DownloadStatus.Stopped,
            CancellationToken
        );

        // Assert
        resetResult.IsSuccess.ShouldBeTrue();
        var downloadTaskDb = await dbContext.GetDownloadTaskFileAsync(testDownloadTask.ToKey(), CancellationToken);
        downloadTaskDb.ShouldNotBeNull();
        downloadTaskDb.DownloadSpeed.ShouldBe(0);
        downloadTaskDb.DataReceived.ShouldBe(0);
        downloadTaskDb.FileTransferSpeed.ShouldBe(0);
        downloadTaskDb.FileDataTransferred.ShouldBe(0);
        downloadTaskDb.CurrentFileTransferBytesOffset.ShouldBe(0);
        downloadTaskDb.DataTotal.ShouldBe(5000);

        downloadTaskDb.FileName.ShouldBe(testDownloadTask.FileName);
        downloadTaskDb.DownloadDirectory.ShouldBe(testDownloadTask.DownloadDirectory);
        downloadTaskDb.DestinationDirectory.ShouldBe(testDownloadTask.DestinationDirectory);
        downloadTaskDb.FileLocationUrl.ShouldBe(testDownloadTask.FileLocationUrl);
        downloadTaskDb.CreatedAt.ShouldBe(testDownloadTask.CreatedAt);
        downloadTaskDb.Quality.ShouldBe(testDownloadTask.Quality);
        downloadTaskDb.IsDownloadable.ShouldBe(testDownloadTask.IsDownloadable);
        downloadTaskDb.MediaType.ShouldBe(testDownloadTask.MediaType);
        downloadTaskDb.FullTitle.ShouldBe(testDownloadTask.FullTitle);
        downloadTaskDb.PlexServerId.ShouldBe(testDownloadTask.PlexServerId);
        downloadTaskDb.PlexLibraryId.ShouldBe(testDownloadTask.PlexLibraryId);
    }

    [Test]
    public async Task ShouldBuildDownloadUrl_WithHttpsConnectionAndToken()
    {
        // Arrange
        var seed = await SetupDatabase(
            561231,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 0;
                config.PlexAccountCount = 1;
            }
        );

        var db = IDbContext;
        var server = await db.PlexServers.FirstAsync(CancellationToken);

        // Create an HTTPS online connection
        var conn = FakeData.GetPlexServerConnections(seed).Generate();
        conn = new PlexServerConnection
        {
            Protocol = "https",
            Address = conn.Address,
            Port = conn.Port,
            Url = $"https://{conn.Address}:{conn.Port}",
            Local = false,
            Relay = false,
            IPv4 = true,
            IPv6 = false,
            IsCustom = false,
            PlexServerId = server.Id,
        };
        conn.LatestConnectionStatus = FakeData.GetPlexServerStatus(seed).Generate();
        conn.LatestConnectionStatus.PlexServerId = server.Id;
        conn.LatestConnectionStatus.PlexServerConnectionId = conn.Id;
        db.PlexServerConnections.Add(conn);
        await db.SaveChangesAsync(CancellationToken);

        var access = await db.PlexAccountServers.FirstAsync(CancellationToken);
        var fileLocationUrl = "/library/parts/123/file.mkv";

        // Act
        var result = await db.GetDownloadUrl(server.Id, fileLocationUrl, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe($"{conn.Url}{fileLocationUrl}?X-Plex-Token={access.AuthToken}");
    }

    [Test]
    public async Task ShouldFail_GetDownloadUrl_WhenNoConnectionsAvailable()
    {
        // Arrange
        await SetupDatabase(
            992341,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 0;
                config.PlexAccountCount = 1;
            }
        );
        var db = IDbContext;
        var server = await db.PlexServers.FirstAsync(CancellationToken);

        // Act
        var result = await db.GetDownloadUrl(server.Id, "/file", CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldFail_GetDownloadUrl_WhenNoTokenAvailable()
    {
        // Arrange
        var seed = await SetupDatabase(
            335522,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexServerConnectionPerServerCount = 0;
                config.PlexAccountCount = 0;
            }
        );

        var db = IDbContext;
        var server = await db.PlexServers.FirstAsync(CancellationToken);
        var conn = FakeData.GetPlexServerConnections(seed).Generate();
        conn = new PlexServerConnection
        {
            Protocol = "http",
            Address = conn.Address,
            Port = conn.Port,
            Url = $"http://{conn.Address}:{conn.Port}",
            Local = false,
            Relay = false,
            IPv4 = true,
            IPv6 = false,
            IsCustom = false,
            PlexServerId = server.Id,
        };
        conn.LatestConnectionStatus = FakeData.GetPlexServerStatus(seed).Generate();
        conn.LatestConnectionStatus.PlexServerId = server.Id;
        conn.LatestConnectionStatus.PlexServerConnectionId = conn.Id;
        db.PlexServerConnections.Add(conn);
        await db.SaveChangesAsync(CancellationToken);

        // Act
        var result = await db.GetDownloadUrl(server.Id, "/file", CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnNullDownloadTaskKey_WhenGuidEmpty()
    {
        // Arrange
        await SetupDatabase(114455);

        // Act
        var key = await IDbContext.GetDownloadTaskKeyAsync(Guid.Empty, CancellationToken);

        // Assert
        key.ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnDownloadTask_WhenTypeIsNoneAndIdMatchesMovieFile()
    {
        // Arrange
        await SetupDatabase(
            226677,
            config =>
            {
                config.MovieDownloadTasksCount = 3;
            }
        );
        var db = IDbContext;
        var movieFile = await db.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        // Act
        var generic = await db.GetDownloadTaskAsync(movieFile.Id, DownloadTaskType.None, CancellationToken);

        // Assert
        generic.ShouldNotBeNull();
        generic.Id.ShouldBe(movieFile.Id);
        generic.DownloadTaskType.ShouldBe(DownloadTaskType.MovieData);
        generic.PlexServer.ShouldNotBeNull();
        generic.PlexLibrary.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldReturnMovieRoot_WhenRadarrOwnedMovieFileHasNoIntegrationFilter()
    {
        // Arrange
        await SetupDatabase(
            226678,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );
        var dbContext = IDbContext;
        var movie = await dbContext.DownloadTaskMovie.SingleAsync(CancellationToken);
        var movieFile = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);

        // Act
        var rootKey = await dbContext.GetRootDownloadTaskKeyAsync(
            movieFile.ToKey(),
            cancellationToken: CancellationToken
        );

        // Assert
        movieFile.RadarrIntegrationId.ShouldNotBeNull();
        rootKey.ShouldNotBeNull();
        rootKey.Id.ShouldBe(movie.Id);
        rootKey.Type.ShouldBe(DownloadTaskType.Movie);
        rootKey.PlexServerId.ShouldBe(movieFile.PlexServerId);
        rootKey.PlexLibraryId.ShouldBe(movieFile.PlexLibraryId);
    }

    [Test]
    public async Task ShouldReturnTvShowRoot_WhenRadarrOwnedEpisodeFileHasNoIntegrationFilter()
    {
        // Arrange
        await SetupDatabase(
            226679,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );
        var dbContext = IDbContext;
        var tvShow = await dbContext.DownloadTaskTvShow.SingleAsync(CancellationToken);
        var episodeFile = await dbContext.DownloadTaskTvShowEpisodeFile.SingleAsync(CancellationToken);

        // Act
        var rootKey = await dbContext.GetRootDownloadTaskKeyAsync(
            episodeFile.ToKey(),
            cancellationToken: CancellationToken
        );

        // Assert
        episodeFile.RadarrIntegrationId.ShouldNotBeNull();
        rootKey.ShouldNotBeNull();
        rootKey.Id.ShouldBe(tvShow.Id);
        rootKey.Type.ShouldBe(DownloadTaskType.TvShow);
        rootKey.PlexServerId.ShouldBe(episodeFile.PlexServerId);
        rootKey.PlexLibraryId.ShouldBe(episodeFile.PlexLibraryId);
    }

    [Test]
    public async Task ShouldReturnNull_WhenRadarrOwnedMovieFileHasDifferentIntegrationFilter()
    {
        // Arrange
        await SetupDatabase(
            226680,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );
        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var differentIntegration = new IntegrationIdentity(
            IntegrationType.Radarr,
            Guid.Parse("3ad7b0be-b78e-43f2-ab07-5a24649667f3")
        );

        // Act
        var rootKey = await dbContext.GetRootDownloadTaskKeyAsync(
            movieFile.ToKey(),
            differentIntegration,
            CancellationToken
        );

        // Assert
        movieFile.RadarrIntegrationId.ShouldNotBeNull();
        movieFile.RadarrIntegrationId.ShouldNotBe(differentIntegration.Id);
        rootKey.ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnOwnedAndUnownedTasks_WhenIntegrationFilterIsNull()
    {
        // Arrange
        await SetupDatabase(
            226681,
            config =>
            {
                config.MovieDownloadTasksCount = 2;
                config.RadarrIntegrationCount = 1;
            }
        );
        var dbContext = IDbContext;
        var integrationId = await dbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var movieFiles = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        movieFiles[0].RadarrIntegrationId = integrationId;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await dbContext
            .DownloadTaskMovieFile.WhereIntegrationIs(null)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(movieFiles[0].Id);
        result.ShouldContain(movieFiles[1].Id);
    }

    [Test]
    public async Task ShouldReturnOnlyUnownedTasks_WhenOwnershipMatchIsNull()
    {
        // Arrange
        await SetupDatabase(
            226682,
            config =>
            {
                config.MovieDownloadTasksCount = 2;
                config.RadarrIntegrationCount = 1;
            }
        );
        var dbContext = IDbContext;
        var integrationId = await dbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var movieFiles = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        movieFiles[0].RadarrIntegrationId = integrationId;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await dbContext
            .DownloadTaskMovieFile.WhereIntegrationOwnershipMatches(null)
            .SingleAsync(CancellationToken);

        // Assert
        result.Id.ShouldBe(movieFiles[1].Id);
        result.SonarrIntegrationId.ShouldBeNull();
        result.RadarrIntegrationId.ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnOnlyMatchingRadarrTasks_WhenIntegrationIsSpecified()
    {
        // Arrange
        await SetupDatabase(
            226683,
            config =>
            {
                config.MovieDownloadTasksCount = 4;
                config.RadarrIntegrationCount = 2;
                config.SonarrIntegrationCount = 1;
            }
        );
        var dbContext = IDbContext;
        var radarrIds = await dbContext
            .RadarrIntegrations.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var sonarrId = await dbContext.SonarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var movieFiles = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        movieFiles[0].RadarrIntegrationId = radarrIds[0];
        movieFiles[1].RadarrIntegrationId = radarrIds[1];
        movieFiles[2].SonarrIntegrationId = sonarrId;
        await dbContext.SaveChangesAsync(CancellationToken);
        var identity = new IntegrationIdentity(IntegrationType.Radarr, radarrIds[0]);

        // Act
        var result = await dbContext.DownloadTaskMovieFile.WhereIntegrationIs(identity).SingleAsync(CancellationToken);

        // Assert
        result.Id.ShouldBe(movieFiles[0].Id);
        result.RadarrIntegrationId.ShouldBe(radarrIds[0]);
        result.SonarrIntegrationId.ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnOnlyMatchingSonarrTasks_WhenIntegrationIsSpecified()
    {
        // Arrange
        await SetupDatabase(
            226684,
            config =>
            {
                config.MovieDownloadTasksCount = 3;
                config.RadarrIntegrationCount = 1;
                config.SonarrIntegrationCount = 1;
            }
        );
        var dbContext = IDbContext;
        var radarrId = await dbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var sonarrId = await dbContext.SonarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var movieFiles = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        movieFiles[0].RadarrIntegrationId = radarrId;
        movieFiles[1].SonarrIntegrationId = sonarrId;
        await dbContext.SaveChangesAsync(CancellationToken);
        var identity = new IntegrationIdentity(IntegrationType.Sonarr, sonarrId);

        // Act
        var result = await dbContext.DownloadTaskMovieFile.WhereIntegrationIs(identity).SingleAsync(CancellationToken);

        // Assert
        result.Id.ShouldBe(movieFiles[1].Id);
        result.SonarrIntegrationId.ShouldBe(sonarrId);
        result.RadarrIntegrationId.ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnMatchingAndUnownedTasks_WhenIntegrationOrUnownedIsRequested()
    {
        // Arrange
        await SetupDatabase(
            226685,
            config =>
            {
                config.MovieDownloadTasksCount = 4;
                config.RadarrIntegrationCount = 2;
                config.SonarrIntegrationCount = 1;
            }
        );
        var dbContext = IDbContext;
        var radarrIds = await dbContext
            .RadarrIntegrations.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var sonarrId = await dbContext.SonarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var movieFiles = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        movieFiles[0].RadarrIntegrationId = radarrIds[0];
        movieFiles[1].RadarrIntegrationId = radarrIds[1];
        movieFiles[2].SonarrIntegrationId = sonarrId;
        await dbContext.SaveChangesAsync(CancellationToken);
        var identity = new IntegrationIdentity(IntegrationType.Radarr, radarrIds[0]);

        // Act
        var result = await dbContext
            .DownloadTaskMovieFile.WhereIntegrationIsOrUnowned(identity)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(movieFiles[0].Id);
        result.ShouldContain(movieFiles[3].Id);
        result.ShouldNotContain(movieFiles[1].Id);
        result.ShouldNotContain(movieFiles[2].Id);
    }

    [Test]
    public async Task ShouldNotMatchRadarrTask_WhenSonarrIdentityUsesSameId()
    {
        // Arrange
        await SetupDatabase(
            226686,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );
        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var radarrId = movieFile.RadarrIntegrationId.ShouldNotBeNull();
        var wrongTypeIdentity = new IntegrationIdentity(IntegrationType.Sonarr, radarrId);

        // Act
        var rootKey = await dbContext.GetRootDownloadTaskKeyAsync(
            movieFile.ToKey(),
            wrongTypeIdentity,
            CancellationToken
        );

        // Assert
        movieFile.SonarrIntegrationId.ShouldBeNull();
        movieFile.RadarrIntegrationId.ShouldBe(radarrId);
        rootKey.ShouldBeNull();
    }
}
