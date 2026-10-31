namespace Reaparr.Data.UnitTests;

public class ReaparrDbContextExtensionsGetDownloadProgressTasksByServerAsyncUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ShouldPreserveEveryTypedHierarchyAndProgress_WhenRetrievingAllOrOneServer(
        bool filterServer,
        bool includeAdditionalPhotoRoot
    )
    {
        // Arrange
        await SetupDatabase(
            129887,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistDownloadTasksCount = 1;
                config.MusicAlbumDownloadTasksCount = 1;
                config.MusicTrackDownloadTasksCount = 1;
                config.MusicTrackFileDownloadTasksCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoFolderDownloadTasksCount = 1;
                config.PhotoImageDownloadTasksCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        Guid? additionalPhotoRootId = null;
        if (includeAdditionalPhotoRoot)
        {
            var library = await dbContext
                .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
                .OrderByDescending(x => x.PlexServerId)
                .FirstAsync(CancellationToken);
            var additionalRoot = FakeData
                .GetDownloadTaskPhotoFolder(new Seed(129888), config => config.PhotoImageDownloadTasksCount = 2)
                .Generate();
            additionalRoot.DownloadRootPath = $"/downloads/{additionalRoot.Id:N}";
            additionalRoot.DestinationRootPath = $"/destination/{additionalRoot.Id:N}";
            foreach (var file in additionalRoot.Files)
            {
                file.DirectoryMeta.DownloadRootPath = additionalRoot.DownloadRootPath;
                file.DirectoryMeta.DestinationRootPath = additionalRoot.DestinationRootPath;
            }
            new List<DownloadTaskPhotoFolder> { additionalRoot }.SetRelationshipIds(library.PlexServerId, library.Id);
            var files = additionalRoot.Files.OrderBy(x => x.Id).ToList();
            files[0].DataTotal = 100;
            files[0].DataReceived = 25;
            files[0].Percentage = 25;
            files[0].DownloadStatus = DownloadStatus.Downloading;
            files[1].DataTotal = 200;
            files[1].DataReceived = 200;
            files[1].Percentage = 100;
            files[1].DownloadStatus = DownloadStatus.Completed;
            additionalRoot.DownloadStatus = DownloadStatus.Downloading;
            dbContext.DownloadTaskPhotoFolders.Add(additionalRoot);
            await dbContext.SaveChangesAsync(CancellationToken);
            additionalPhotoRootId = additionalRoot.Id;
        }
        var allTasks = await dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var serverIds = await dbContext
            .PlexServers.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToArrayAsync(CancellationToken);
        serverIds.Length.ShouldBe(2);
        DownloadTaskType[] rootTypes =
        [
            DownloadTaskType.Movie,
            DownloadTaskType.TvShow,
            DownloadTaskType.MusicArtist,
            DownloadTaskType.PhotoFolder,
            DownloadTaskType.OtherVideo,
        ];
        var expectedRootTypes = includeAdditionalPhotoRoot ? rootTypes.Append(DownloadTaskType.PhotoFolder) : rootTypes;
        allTasks.Select(x => x.DownloadTaskType).Order().ShouldBe(expectedRootTypes.Order());
        if (additionalPhotoRootId is Guid rootId)
        {
            var additionalRoot = allTasks.Single(x => x.Id == rootId);
            additionalRoot.ParentId.ShouldBe(Guid.Empty);
            (additionalRoot.DataTotal, additionalRoot.DataReceived, additionalRoot.Percentage).ShouldBe(
                (300L, 225L, 62.5m)
            );
            additionalRoot.DownloadTaskType.ShouldBe(DownloadTaskType.PhotoFolder);
            additionalRoot.Children.Select(x => x.ParentId).ShouldBe([rootId, rootId]);
            additionalRoot.Children.ShouldAllBe(x =>
                x.DownloadTaskType == DownloadTaskType.PhotoImage && x.IsDownloadable
            );
        }
        allTasks.ShouldAllBe(x => serverIds.Contains(x.PlexServerId));
        var requestedServerId = filterServer ? allTasks[0].PlexServerId : 0;
        var expected = allTasks
            .Where(x => !filterServer || x.PlexServerId == requestedServerId)
            .Flatten(x => x.Children)
            .OrderBy(x => x.Id)
            .ToArray();
        if (!filterServer)
            expected.Select(x => x.DownloadTaskType).Distinct().Count().ShouldBe(14);
        expected.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.FullTitle));
        var expectedKeys = expected.Select(x => x.ToKey()).ToArray();
        (await dbContext.GetDownloadTaskKeysByStatusAsync(expectedKeys, [DownloadStatus.Queued], CancellationToken))
            .OrderBy(x => x.Id)
            .ShouldBe(
                expected.Where(x => x.DownloadStatus == DownloadStatus.Queued).Select(x => x.ToKey()).OrderBy(x => x.Id)
            );
        (await dbContext.GetDownloadTaskKeysByStatusAsync(expectedKeys, [DownloadStatus.Completed], CancellationToken))
            .OrderBy(x => x.Id)
            .ShouldBe(
                expected
                    .Where(x => x.DownloadStatus == DownloadStatus.Completed)
                    .Select(x => x.ToKey())
                    .OrderBy(x => x.Id)
            );

        // Act
        var progressTasks = await dbContext.GetDownloadProgressTasksByServerAsync(requestedServerId, CancellationToken);

        // Assert
        progressTasks
            .Select(x => x.Id)
            .Order()
            .ShouldBe(
                allTasks.Where(x => !filterServer || x.PlexServerId == requestedServerId).Select(x => x.Id).Order()
            );
        var actual = progressTasks.Flatten(x => x.Children).OrderBy(x => x.Id).ToArray();
        actual
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.RatingKey,
                x.Title,
                x.FullTitle,
                x.MediaType,
                x.DownloadTaskType,
                x.PlexServerId,
                x.PlexLibraryId,
                x.IsDownloadable,
                x.DownloadStatus,
                x.DataTotal,
                x.DataReceived,
                x.Percentage,
                x.DownloadSpeed,
                x.TimeRemaining,
                x.FileTransferSpeed,
                x.FileDataTransferred,
            })
            .ShouldBe(
                expected.Select(x => new
                {
                    x.Id,
                    x.ParentId,
                    x.RatingKey,
                    x.Title,
                    x.FullTitle,
                    x.MediaType,
                    x.DownloadTaskType,
                    x.PlexServerId,
                    x.PlexLibraryId,
                    x.IsDownloadable,
                    x.DownloadStatus,
                    x.DataTotal,
                    x.DataReceived,
                    x.Percentage,
                    x.DownloadSpeed,
                    x.TimeRemaining,
                    x.FileTransferSpeed,
                    x.FileDataTransferred,
                })
            );
        if (filterServer)
        {
            var otherServerId = serverIds.Single(x => x != requestedServerId);
            var otherProgress = await dbContext.GetDownloadProgressTasksByServerAsync(otherServerId, CancellationToken);
            otherProgress
                .Select(x => x.Id)
                .Order()
                .ShouldBe(allTasks.Where(x => x.PlexServerId == otherServerId).Select(x => x.Id).Order());
            otherProgress.Select(x => x.Id).Intersect(progressTasks.Select(x => x.Id)).ShouldBeEmpty();
        }
    }
}
