using Reaparr.Data;

namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskPhotoImagesFolderSelectionUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskPhotoImagesCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldResolveOverlappingFolderOptions_AccordingToDirectSelectionPrecedence(
        bool includeDirectSelection
    )
    {
        // Arrange
        var selections = await CreateOverlappingFolderSelectionsAsync(includeDirectSelection);
        var dbContext = IDbContext;

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(new CreateDownloadTasksRequest(selections))
        );

        // Assert
        if (includeDirectSelection)
        {
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 2, PhotoImages = 1 });
            var queued = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
            queued.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBeFalse();
        }
        else
        {
            result.IsFailed.ShouldBeTrue();
            result.Errors.Count.ShouldBe(1);
            (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
            (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        }
    }

    [Test]
    public async Task ShouldExpandEveryDescendant_WhenPhysicalFolderIsSelected()
    {
        // Arrange
        var selection = await CreateRecursiveFolderSelectionAsync();
        var dbContext = IDbContext;

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/folder-target")
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 3, PhotoImages = 2 });
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(2);
        (
            await dbContext.DownloadTaskPhotoFolders.Select(x => x.RelativePath).ToListAsync(CancellationToken)
        ).ShouldContain("Trips/Day 1");
    }

    private async Task<List<DownloadMediaDTO>> CreateOverlappingFolderSelectionsAsync(bool includeDirectSelection)
    {
        await SetupDatabase(
            62318,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var folder = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "Trips",
            Title = "Trips",
        };
        dbContext.PlexPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        var albums = await dbContext.PlexPhotoAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var file = await dbContext.PlexPhotoMediaData.AsTracking().SingleAsync(CancellationToken);
        file.PlexPhotoFolderId = folder.Id;
        await dbContext.SaveChangesAsync(CancellationToken);
        var selections = albums
            .Select(album => new DownloadMediaDTO
            {
                Type = PlexMediaType.PhotoAlbum,
                SelectionKind = PhotoItemKind.Album,
                PlexServerId = album.PlexServerId,
                PlexLibraryId = album.PlexLibraryId,
                MediaIds = [album.Id],
                Qualities = [],
                KeepCompletedInDownloadFolder = false,
            })
            .ToList();
        selections.Add(
            new DownloadMediaDTO
            {
                Type = PlexMediaType.PhotoImage,
                SelectionKind = PhotoItemKind.Folder,
                PlexServerId = file.PlexServerId,
                PlexLibraryId = file.PlexLibraryId,
                MediaIds = [folder.Id],
                Qualities = [],
                KeepCompletedInDownloadFolder = true,
            }
        );
        if (includeDirectSelection)
            selections.Add(
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoImage,
                    SelectionKind = PhotoItemKind.Asset,
                    PlexServerId = file.PlexServerId,
                    PlexLibraryId = file.PlexLibraryId,
                    MediaIds = [file.Id],
                    Qualities = [],
                    KeepCompletedInDownloadFolder = false,
                }
            );
        return selections;
    }

    private async Task<DownloadMediaDTO> CreateRecursiveFolderSelectionAsync()
    {
        await SetupDatabase(
            62317,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var child = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "Trips",
            Title = "Trips",
        };
        dbContext.PlexPhotoFolders.Add(child);
        await dbContext.SaveChangesAsync(CancellationToken);
        var grandchild = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = child.Id,
            RelativePath = "Trips/Day 1",
            Title = "Day 1",
        };
        dbContext.PlexPhotoFolders.Add(grandchild);
        await dbContext.SaveChangesAsync(CancellationToken);
        media[0].PlexPhotoFolderId = child.Id;
        media[1].PlexPhotoFolderId = grandchild.Id;
        await dbContext.SaveChangesAsync(CancellationToken);
        return new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Folder,
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            MediaIds = [child.Id],
            Qualities = [],
        };
    }
}
