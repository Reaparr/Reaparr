namespace Reaparr.Application.UnitTests;

public class GetPhotoDownloadPreviewCommandFolderSelectionUnitTests
    : BaseCommandUnitTest<GetPhotoDownloadPreviewCommand>
{
    [Test]
    public async Task ShouldPreviewEveryDescendant_WhenPhysicalFolderIsSelected()
    {
        // Arrange
        await SetupDatabase(
            62409,
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
        var command = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.PhotoImage,
                        SelectionKind = PhotoItemKind.Folder,
                        PlexServerId = root.PlexServerId,
                        PlexLibraryId = root.PlexLibraryId,
                        MediaIds = [child.Id],
                        Qualities = [],
                    },
                ],
                CustomDestinationFolderPath = "/folder-preview",
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var preview = result.Value.Single();
        preview.ChildCount.ShouldBe(2);
        preview.Size.ShouldBe(media.Sum(x => x.Size));
        preview
            .Children.Single(x => x.Title == "Trips")
            .Children.Single(x => x.Title == "Day 1")
            .Children.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldReturnSuccessfulEmptyPreview_WhenSelectedFolderHasNoAssets()
    {
        // Arrange
        await SetupDatabase(
            62410,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var empty = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "Empty",
            Title = "Empty",
        };
        dbContext.PlexPhotoFolders.Add(empty);
        await dbContext.SaveChangesAsync(CancellationToken);
        var command = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.PhotoImage,
                        SelectionKind = PhotoItemKind.Folder,
                        PlexServerId = root.PlexServerId,
                        PlexLibraryId = root.PlexLibraryId,
                        MediaIds = [empty.Id],
                        Qualities = [],
                    },
                ],
                CustomDestinationFolderPath = "/empty-preview",
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }
}
