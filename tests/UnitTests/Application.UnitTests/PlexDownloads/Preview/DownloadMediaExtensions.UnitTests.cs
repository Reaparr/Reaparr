namespace Reaparr.Application.UnitTests;

public class DownloadMediaExtensionsUnitTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldKeepFolderAndAssetIdsSeparate_WhenMergingPhotoSelections(bool groupByLibrary)
    {
        // Arrange
        List<DownloadMediaDTO> selections =
        [
            new()
            {
                PlexServerId = 1,
                PlexLibraryId = 2,
                Type = PlexMediaType.PhotoImage,
                SelectionKind = PhotoItemKind.Folder,
                MediaIds = [11, 12],
                Qualities = [],
                KeepCompletedInDownloadFolder = true,
            },
            new()
            {
                PlexServerId = 1,
                PlexLibraryId = 2,
                Type = PlexMediaType.PhotoImage,
                SelectionKind = PhotoItemKind.Asset,
                MediaIds = [11],
                Qualities = [],
                KeepCompletedInDownloadFolder = false,
            },
        ];

        // Act
        var merged = groupByLibrary ? selections.MergeAndGroupList() : selections.Merge(PlexMediaType.PhotoImage);

        // Assert
        merged.Count.ShouldBe(2);
        var folder = merged.Single(x => x.SelectionKind == PhotoItemKind.Folder);
        var asset = merged.Single(x => x.SelectionKind == PhotoItemKind.Asset);
        folder.MediaIds.ShouldBe([11, 12]);
        asset.MediaIds.ShouldBe([11]);
        folder.KeepCompletedInDownloadFolder.ShouldBeTrue();
        asset.KeepCompletedInDownloadFolder.ShouldBeFalse();
        merged
            .Select(x => (x.PlexServerId, x.PlexLibraryId, x.Type))
            .ShouldBe([(1, 2, PlexMediaType.PhotoImage), (1, 2, PlexMediaType.PhotoImage)]);
    }
}
