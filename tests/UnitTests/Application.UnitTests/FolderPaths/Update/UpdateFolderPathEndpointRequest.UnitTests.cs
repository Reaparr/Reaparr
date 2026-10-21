namespace Reaparr.Application.UnitTests;

public class UpdateFolderPathEndpointRequestUnitTests
    : BaseEndpointUnitTest<UpdateFolderPathEndpoint, UpdateFolderPathEndpointRequest, ResultDTO<FolderPathDTO>>
{
    [Test]
    public async Task ShouldRejectNullFolderDtoWithoutMutation_WhenUpdatingFolderPath()
    {
        // Arrange
        await SetupDatabase(45810);
        var db = IDbContext;
        var before = await db.FolderPaths.SingleAsync(x => x.Id == 2, CancellationToken);
        var request = new UpdateFolderPathEndpointRequest { FolderPathDTO = null! };

        // Act
        var outcome = await TestEndpointHandleAsync(request);
        var after = await db.FolderPaths.SingleAsync(x => x.Id == 2, CancellationToken);

        // Assert
        outcome.IsValid.ShouldBeFalse();
        outcome.Response.ShouldBeNull();
        outcome.ValidationResult.ShouldNotBeNull().Errors.Select(x => x.PropertyName)
            .ShouldBe([nameof(UpdateFolderPathEndpointRequest.FolderPathDTO)]);
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(
            (before.Id, before.DisplayName, before.DirectoryPath, before.FolderType, before.MediaType));
    }

    [Test]
    [Arguments(2, FolderType.MovieFolder, PlexMediaType.Movie)]
    [Arguments(1, FolderType.DownloadFolder, PlexMediaType.None)]
    [Arguments(10, FolderType.MovieFolder, PlexMediaType.Movie)]
    public async Task ShouldPersistAllowedFolderEdits_WhenRequestIsValid(
        int folderId, FolderType folderType, PlexMediaType mediaType)
    {
        // Arrange
        await SetupDatabase(45811);
        var db = IDbContext;
        var before = await db.FolderPaths.SingleAsync(x => x.Id == folderId, CancellationToken);
        var control = await db.FolderPaths.SingleAsync(x => x.Id == 7, CancellationToken);
        var request = new UpdateFolderPathEndpointRequest
        {
            FolderPathDTO = new FolderPathDTO
            {
                Id = before.Id, DisplayName = "Allowed edit", Directory = "/tmp/allowed-folder-edit",
                FolderType = folderType, MediaType = mediaType, IsDefault = false, IsValid = true,
            },
        };

        // Act
        var outcome = await TestEndpointHandleAsync(request);
        var after = await db.FolderPaths.SingleAsync(x => x.Id == folderId, CancellationToken);
        var afterControl = await db.FolderPaths.SingleAsync(x => x.Id == 7, CancellationToken);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var result = outcome.Response.ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        var value = result.Value.ShouldNotBeNull();
        (value.Id, value.DisplayName, value.Directory, value.FolderType, value.MediaType).ShouldBe(
            (folderId, "Allowed edit", "/tmp/allowed-folder-edit", folderType, mediaType));
        value.IsDefault.ShouldBeTrue();
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(
            (folderId, "Allowed edit", "/tmp/allowed-folder-edit", folderType, mediaType));
        (afterControl.Id, afterControl.DisplayName, afterControl.DirectoryPath, afterControl.FolderType, afterControl.MediaType).ShouldBe(
            (control.Id, control.DisplayName, control.DirectoryPath, control.FolderType, control.MediaType));
    }

    [Test]
    [Arguments(FolderType.MovieFolder, PlexMediaType.None, "FolderPathDTO.MediaType")]
    [Arguments(FolderType.MovieFolder, PlexMediaType.Unknown, "FolderPathDTO.MediaType")]
    [Arguments(FolderType.None, PlexMediaType.Movie, "FolderPathDTO.FolderType")]
    [Arguments(FolderType.Unknown, PlexMediaType.Movie, "FolderPathDTO.FolderType")]
    public async Task ShouldRejectInvalidRoleWithoutMutation_WhenUpdatingFolderPath(
        FolderType folderType, PlexMediaType mediaType, string errorProperty)
    {
        // Arrange
        await SetupDatabase(45812);
        var db = IDbContext;
        var before = await db.FolderPaths.SingleAsync(x => x.Id == 2, CancellationToken);
        var request = new UpdateFolderPathEndpointRequest
        {
            FolderPathDTO = new FolderPathDTO
            {
                Id = before.Id, DisplayName = "Rejected edit", Directory = "/tmp/rejected-folder-edit",
                FolderType = folderType, MediaType = mediaType, IsDefault = false, IsValid = true,
            },
        };

        // Act
        var outcome = await TestEndpointHandleAsync(request);
        var after = await db.FolderPaths.SingleAsync(x => x.Id == 2, CancellationToken);

        // Assert
        outcome.IsValid.ShouldBeFalse();
        outcome.Response.ShouldBeNull();
        outcome.ValidationResult.ShouldNotBeNull().Errors.Select(x => x.PropertyName).ShouldBe([errorProperty]);
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(
            (before.Id, before.DisplayName, before.DirectoryPath, before.FolderType, before.MediaType));
    }
}
