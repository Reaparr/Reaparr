namespace Reaparr.Application.UnitTests;

public class ClearCompletedDownloadTasksByServerIdCommandUnitTests
    : BaseCommandUnitTest<ClearCompletedDownloadTasksByServerIdCommand>
{
    [Test]
    public async Task ShouldClearOnlyCompletedPhotosOnRequestedServer_WhenClearingServer()
    {
        // Arrange
        await SetupDatabase(
            88100,
            c =>
            {
                c.PlexServerCount = 2;
                c.PlexPhotoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        var albums = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88100))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88101)).Generate(1))
            .Generate(3);
        for (var i = 0; i < albums.Count; i++)
        {
            var album = albums[i];
            var library = libraries[i == 2 ? 1 : 0];
            album.DestinationRootPath = $"/Photos/context-{i}";
            foreach (var file in album.Files)
                file.DirectoryMeta.DestinationRootPath = album.DestinationRootPath;
            foreach (var node in new DownloadTaskBase[] { album }.Concat(album.Files))
            {
                node.PlexServerId = library.PlexServerId;
                node.PlexLibraryId = library.Id;
                node.DownloadStatus = i == 1 ? DownloadStatus.Queued : DownloadStatus.Completed;
            }
        }
        dbContext.DownloadTaskPhotoFolders.AddRange(albums);
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(3);

        // Act
        var result = await TestHandlerExecuteAsync<int>(
            new ClearCompletedDownloadTasksByServerIdCommand(libraries[0].PlexServerId)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoFolders.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(albums.Skip(1).Select(x => x.Id).Order());
        (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(albums.Skip(1).SelectMany(x => x.Files).Select(x => x.Id).Order());
    }

    [Test]
    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public async Task ShouldClearOnlyCompletedMediaOnSelectedServerAndCountOrphans_WhenClearing(
        bool isMusic,
        bool completeRoot
    )
    {
        // Arrange
        await SetupDatabase(
            88260,
            c =>
            {
                c.PlexMusicLibraryCount = 1;
                c.MusicArtistDownloadTasksCount = 3;
                c.MusicAlbumDownloadTasksCount = 1;
                c.MusicTrackDownloadTasksCount = 1;
                c.MusicTrackFileDownloadTasksCount = 1;
                c.PlexOtherVideoLibraryCount = 1;
                c.OtherVideoDownloadTasksCount = 3;
                c.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var musicFiles = await dbContext
            .DownloadTaskMusicTrackFiles.Include(x => x.Parent)
                .ThenInclude(x => x!.Parent!)
                    .ThenInclude(x => x!.Parent!)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        var otherVideoFiles = await dbContext
            .DownloadTaskOtherVideoFiles.Include(x => x.Parent)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        DownloadTaskFileBase target = isMusic ? musicFiles[0] : otherVideoFiles[0];
        DownloadTaskFileBase idle = isMusic ? musicFiles[1] : otherVideoFiles[1];
        DownloadTaskFileBase otherServer = isMusic ? musicFiles[2] : otherVideoFiles[2];
        var retainedServer = FakeData.GetPlexServer(new Seed(88261)).Generate();
        retainedServer.MachineIdentifier = "clear-completed-retained-server";
        dbContext.PlexServers.Add(retainedServer);
        await dbContext.SaveChangesAsync(CancellationToken);
        var retainedLibrary = FakeData
            .GetPlexLibrary(new Seed(88262), isMusic ? PlexMediaType.MusicArtist : PlexMediaType.OtherVideos)
            .RuleFor(x => x.PlexServerId, _ => retainedServer.Id)
            .Generate();
        dbContext.PlexLibraries.Add(retainedLibrary);
        await dbContext.SaveChangesAsync(CancellationToken);
        DownloadTaskBase[] targetParents = isMusic
            ?
            [
                ((DownloadTaskMusicTrackFile)target).Parent!,
                ((DownloadTaskMusicTrackFile)target).Parent!.Parent!,
                ((DownloadTaskMusicTrackFile)target).Parent!.Parent!.Parent!,
            ]
            : [((DownloadTaskOtherVideoFile)target).Parent!];
        DownloadTaskBase[] retainedParents = isMusic
            ?
            [
                ((DownloadTaskMusicTrackFile)otherServer).Parent!,
                ((DownloadTaskMusicTrackFile)otherServer).Parent!.Parent!,
                ((DownloadTaskMusicTrackFile)otherServer).Parent!.Parent!.Parent!,
            ]
            : [((DownloadTaskOtherVideoFile)otherServer).Parent!];
        target.DownloadStatus = DownloadStatus.Completed;
        dbContext.Entry(target).State = EntityState.Modified;
        foreach (var parent in targetParents)
        {
            parent.DownloadStatus = completeRoot ? DownloadStatus.Completed : DownloadStatus.Queued;
            dbContext.Entry(parent).State = EntityState.Modified;
        }
        foreach (var node in retainedParents.Append(otherServer))
        {
            node.PlexServerId = retainedServer.Id;
            node.PlexLibraryId = retainedLibrary.Id;
            node.DownloadStatus = DownloadStatus.Completed;
            dbContext.Entry(node).State = EntityState.Modified;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var beforeIds = (
            isMusic
                ? await dbContext.DownloadTaskMusicTrackFiles.Select(x => x.Id).ToListAsync(CancellationToken)
                : await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).ToListAsync(CancellationToken)
        );
        beforeIds.Order().ShouldBe(new[] { target.Id, idle.Id, otherServer.Id }.Order());

        // Act
        var result = await TestHandlerExecuteAsync<int>(
            new ClearCompletedDownloadTasksByServerIdCommand(target.PlexServerId)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(
            completeRoot ? 1
            : isMusic ? 4
            : 2
        );
        var expectedFiles = new[] { idle.Id, otherServer.Id }.Order();
        if (isMusic)
        {
            (await dbContext.DownloadTaskMusicTrackFiles.Select(x => x.Id).ToListAsync(CancellationToken))
                .Order()
                .ShouldBe(expectedFiles);
            (await dbContext.DownloadTaskMusicTracks.Select(x => x.Id).ToListAsync(CancellationToken))
                .Order()
                .ShouldBe(
                    new[]
                    {
                        ((DownloadTaskMusicTrackFile)idle).ParentId,
                        ((DownloadTaskMusicTrackFile)otherServer).ParentId,
                    }.Order()
                );
            (await dbContext.DownloadTaskMusicAlbums.Select(x => x.Id).ToListAsync(CancellationToken))
                .Order()
                .ShouldBe(
                    new[]
                    {
                        ((DownloadTaskMusicTrackFile)idle).Parent!.ParentId,
                        ((DownloadTaskMusicTrackFile)otherServer).Parent!.ParentId,
                    }.Order()
                );
            (await dbContext.DownloadTaskMusicArtists.Select(x => x.Id).ToListAsync(CancellationToken))
                .Order()
                .ShouldBe(
                    new[]
                    {
                        ((DownloadTaskMusicTrackFile)idle).Parent!.Parent!.ParentId,
                        ((DownloadTaskMusicTrackFile)otherServer).Parent!.Parent!.ParentId,
                    }.Order()
                );
        }
        else
        {
            (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).ToListAsync(CancellationToken))
                .Order()
                .ShouldBe(expectedFiles);
            (await dbContext.DownloadTaskOtherVideos.Select(x => x.Id).ToListAsync(CancellationToken))
                .Order()
                .ShouldBe(
                    new[]
                    {
                        ((DownloadTaskOtherVideoFile)idle).ParentId,
                        ((DownloadTaskOtherVideoFile)otherServer).ParentId,
                    }.Order()
                );
        }
        (await dbContext.GetDownloadTaskFileAsync(idle.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        (await dbContext.GetDownloadTaskFileAsync(otherServer.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Completed
        );
    }

    [Test]
    public async Task ShouldClearCompletedStandalonePhotoRootWithoutTouchingActiveOrOtherServer()
    {
        await SetupDatabase(
            88116,
            c =>
            {
                c.PlexServerCount = 2;
                c.PlexPhotoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        var roots = FakeData.GetDownloadTaskPhotoFolder(new Seed(88116)).Generate(3);
        for (var i = 0; i < roots.Count; i++)
        {
            var library = libraries[i == 2 ? 1 : 0];
            roots[i].DestinationRootPath = $"/Photos/context-{i}";
            foreach (var file in roots[i].Files)
                file.DirectoryMeta.DestinationRootPath = roots[i].DestinationRootPath;
            foreach (var node in new DownloadTaskBase[] { roots[i] }.Concat(roots[i].Files))
            {
                node.PlexServerId = library.PlexServerId;
                node.PlexLibraryId = library.Id;
                node.DownloadStatus = i == 1 ? DownloadStatus.Queued : DownloadStatus.Completed;
            }
        }
        dbContext.DownloadTaskPhotoFolders.AddRange(roots);
        await dbContext.SaveChangesAsync(CancellationToken);

        var result = await TestHandlerExecuteAsync<int>(
            new ClearCompletedDownloadTasksByServerIdCommand(libraries[0].PlexServerId)
        );

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoFolders.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(new[] { roots[1].Id, roots[2].Id }.Order());
        (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(new[] { roots[1].Files.Single().Id, roots[2].Files.Single().Id }.Order());
        (
            await dbContext.GetAllDownloadTasksByServerAsync(
                libraries[0].PlexServerId,
                cancellationToken: CancellationToken
            )
        )
            .Select(x => x.Id)
            .ShouldBe([roots[1].Id]);
    }
}
