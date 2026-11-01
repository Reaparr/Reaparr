namespace Reaparr.Application.UnitTests;

public class UpdateFolderPathEndpointUnitTests
    : BaseEndpointUnitTest<UpdateFolderPathEndpoint, UpdateFolderPathEndpointRequest, ResultDTO<FolderPathDTO>>
{
    [Test]
    [Arguments(2)]
    [Arguments(11)]
    public async Task ShouldUpdateNameAndPath_WhenFolderRoleIsUnchanged(int folderId)
    {
        // Arrange
        await SetupDatabase(44201);
        var db = IDbContext;
        if (folderId == 11)
        {
            db.FolderPaths.Add(NewFolder(11, FolderType.MovieFolder, PlexMediaType.Movie));
            await db.SaveChangesAsync(CancellationToken);
        }
        var before = await db.FolderPaths.SingleAsync(x => x.Id == folderId, CancellationToken);
        var request = Request(folderId, before.FolderType, before.MediaType);

        // Act
        var outcome = await TestEndpointHandleAsync(request);
        var after = await db.FolderPaths.SingleAsync(x => x.Id == folderId, CancellationToken);
        var resolvedDefault = await db.GetDefaultDestinationFolderPath(PlexMediaType.Movie);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var result = outcome.Response.ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        var value = result.Value.ShouldNotBeNull();
        (value.Id, value.DisplayName, value.Directory, value.FolderType, value.MediaType).ShouldBe(
            (folderId, "Changed name", "/tmp/folder-role-new", before.FolderType, before.MediaType));
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(
            (folderId, "Changed name", "/tmp/folder-role-new", before.FolderType, before.MediaType));
        resolvedDefault.Id.ShouldBe(2);
        if (folderId == 2)
            resolvedDefault.DirectoryPath.ShouldBe("/tmp/folder-role-new");
    }

    [Test]
    public async Task ShouldReturnNotFoundWithoutMutation_WhenFolderDoesNotExist()
    {
        // Arrange
        await SetupDatabase(44202);
        var db = IDbContext;
        var before = await db.FolderPaths.SingleAsync(x => x.Id == 2, CancellationToken);

        // Act
        var outcome = await TestEndpointHandleAsync(Request(999, FolderType.MovieFolder, PlexMediaType.Movie));
        var after = await db.FolderPaths.SingleAsync(x => x.Id == 2, CancellationToken);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var result = outcome.Response.ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((false, 1));
        result.Value.ShouldBeNull();
        (await db.FolderPaths.CountAsync(x => x.Id == 999, CancellationToken)).ShouldBe(0);
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(
            (before.Id, before.DisplayName, before.DirectoryPath, before.FolderType, before.MediaType));
    }

    [Test]
    [Arguments(1, FolderType.MovieFolder, PlexMediaType.Movie)]
    [Arguments(2, FolderType.TvShowFolder, PlexMediaType.TvShow)]
    [Arguments(3, FolderType.MovieFolder, PlexMediaType.Movie)]
    [Arguments(4, FolderType.MovieFolder, PlexMediaType.Movie)]
    [Arguments(5, FolderType.MovieFolder, PlexMediaType.Movie)]
    [Arguments(6, FolderType.MovieFolder, PlexMediaType.Movie)]
    [Arguments(7, FolderType.MovieFolder, PlexMediaType.Movie)]
    [Arguments(1, FolderType.DownloadFolder, PlexMediaType.Movie)]
    [Arguments(2, FolderType.MovieFolder, PlexMediaType.TvShow)]
    [Arguments(3, FolderType.TvShowFolder, PlexMediaType.Movie)]
    [Arguments(4, FolderType.MusicFolder, PlexMediaType.MusicAlbum)]
    [Arguments(5, FolderType.PhotosFolder, PlexMediaType.PhotoImage)]
    [Arguments(6, FolderType.OtherVideosFolder, PlexMediaType.Movie)]
    [Arguments(7, FolderType.GamesVideosFolder, PlexMediaType.Movie)]
    public async Task ShouldRejectRoleChangeWithoutPartialWrites_WhenFolderIsCanonicalDefault(
        int folderId, FolderType requestedFolderType, PlexMediaType requestedMediaType)
    {
        // Arrange
        await SetupDatabase(44203);
        var db = IDbContext;
        var before = await db.FolderPaths.SingleAsync(x => x.Id == folderId, CancellationToken);
        var control = await db.FolderPaths.SingleAsync(x => x.Id == 10, CancellationToken);
        var request = Request(folderId, requestedFolderType, requestedMediaType);

        // Act
        var outcome = await TestEndpointHandleAsync(request);
        var after = await db.FolderPaths.SingleAsync(x => x.Id == folderId, CancellationToken);
        var afterControl = await db.FolderPaths.SingleAsync(x => x.Id == 10, CancellationToken);
        var resolved = await db.GetDefaultDestinationFolderPath(before.MediaType);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var result = outcome.Response.ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((false, 1));
        result.Value.ShouldBeNull();
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(
            (before.Id, before.DisplayName, before.DirectoryPath, before.FolderType, before.MediaType));
        (resolved.Id, resolved.DirectoryPath, resolved.FolderType, resolved.MediaType).ShouldBe(
            (before.Id, before.DirectoryPath, before.FolderType, before.MediaType));
        (afterControl.Id, afterControl.DisplayName, afterControl.DirectoryPath, afterControl.FolderType, afterControl.MediaType).ShouldBe(
            (control.Id, control.DisplayName, control.DirectoryPath, control.FolderType, control.MediaType));
    }

    [Test]
    [Arguments(IntegrationType.Sonarr, FolderType.MovieFolder, PlexMediaType.Movie, false)]
    [Arguments(IntegrationType.Radarr, FolderType.MovieFolder, PlexMediaType.Movie, false)]
    [Arguments(IntegrationType.Sonarr, FolderType.DownloadFolder, PlexMediaType.None, true)]
    [Arguments(IntegrationType.Radarr, FolderType.DownloadFolder, PlexMediaType.None, true)]
    [Arguments(IntegrationType.Sonarr, FolderType.DownloadFolder, PlexMediaType.Movie, true)]
    [Arguments(IntegrationType.Radarr, FolderType.DownloadFolder, PlexMediaType.Movie, true)]
    public async Task ShouldPreserveIntegrationDownloadRole_WhenAssignedFolderIsEdited(
        IntegrationType integrationType, FolderType requestedFolderType, PlexMediaType requestedMediaType, bool accepted)
    {
        // Arrange
        var (db, identity) = await SetupAssignedDownloadFolderAsync(integrationType);
        var before = await db.FolderPaths.SingleAsync(x => x.Id == 11, CancellationToken);
        var beforeResolved = await db.GetDownloadFolder(identity);
        beforeResolved.Id.ShouldBe(11);
        var request = Request(11, requestedFolderType, requestedMediaType);

        // Act
        var outcome = await TestEndpointHandleAsync(request);
        var after = await db.FolderPaths.SingleAsync(x => x.Id == 11, CancellationToken);
        var resolved = await db.GetDownloadFolder(identity);
        var assignedId = integrationType == IntegrationType.Sonarr
            ? await db.SonarrIntegrations.Select(x => x.DownloadFolderId).SingleAsync(CancellationToken)
            : await db.RadarrIntegrations.Select(x => x.DownloadFolderId).SingleAsync(CancellationToken);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(accepted ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        var result = outcome.Response.ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((accepted, accepted ? 0 : 1));
        var expected = accepted
            ? (11, "Changed name", "/tmp/folder-role-new", requestedFolderType, requestedMediaType)
            : (before.Id, before.DisplayName, before.DirectoryPath, before.FolderType, before.MediaType);
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(expected);
        (resolved.Id, resolved.DisplayName, resolved.DirectoryPath, resolved.FolderType, resolved.MediaType).ShouldBe(expected);
        assignedId.ShouldBe(11);
        if (accepted)
        {
            var value = result.Value.ShouldNotBeNull();
            (value.Id, value.DisplayName, value.Directory, value.FolderType, value.MediaType).ShouldBe(expected);
        }
        else
            result.Value.ShouldBeNull();
    }

    [Test]
    [Arguments(FolderType.TvShowFolder, PlexMediaType.TvShow, false, true)]
    [Arguments(FolderType.TvShowFolder, PlexMediaType.Movie, false, true)]
    [Arguments(FolderType.MovieFolder, PlexMediaType.TvShow, false, true)]
    [Arguments(FolderType.MovieFolder, PlexMediaType.Movie, true, true)]
    [Arguments(FolderType.TvShowFolder, PlexMediaType.TvShow, false, false)]
    [Arguments(FolderType.TvShowFolder, PlexMediaType.Movie, false, false)]
    [Arguments(FolderType.MovieFolder, PlexMediaType.TvShow, false, false)]
    [Arguments(FolderType.MovieFolder, PlexMediaType.Movie, true, false)]
    public async Task ShouldPreserveLibraryDestinationRole_WhenAssignedFolderIsEdited(
        FolderType requestedFolderType, PlexMediaType requestedMediaType, bool accepted, bool libraryEnabled)
    {
        // Arrange
        await SetupDatabase(44205, options => options.PlexMovieLibraryCount = 1);
        var db = IDbContext;
        db.FolderPaths.Add(NewFolder(11, FolderType.MovieFolder, PlexMediaType.Movie));
        await db.SaveChangesAsync(CancellationToken);
        var libraryId = await db.PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => x.Id).SingleAsync(CancellationToken);
        await db.PlexLibraries.Where(x => x.Id == libraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.DefaultDestinationId, 11), CancellationToken);
        var before = await db.FolderPaths.SingleAsync(x => x.Id == 11, CancellationToken);
        (await db.GetDestinationFolder(libraryId)).ShouldNotBeNull().Id.ShouldBe(11);
        await db.PlexLibraries.Where(x => x.Id == libraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, libraryEnabled), CancellationToken);

        // Act
        var outcome = await TestEndpointHandleAsync(Request(11, requestedFolderType, requestedMediaType));
        var after = await db.FolderPaths.SingleAsync(x => x.Id == 11, CancellationToken);
        var assignment = await db.PlexLibraries.IgnoreIsEnabledFilter().Where(x => x.Id == libraryId)
            .Select(x => new { x.DefaultDestinationId, x.IsEnabled }).SingleAsync(CancellationToken);
        await db.PlexLibraries.IgnoreIsEnabledFilter().Where(x => x.Id == libraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);
        var resolved = (await db.GetDestinationFolder(libraryId)).ShouldNotBeNull();

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(accepted ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        var result = outcome.Response.ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((accepted, accepted ? 0 : 1));
        var expected = accepted
            ? (11, "Changed name", "/tmp/folder-role-new", FolderType.MovieFolder, PlexMediaType.Movie)
            : (before.Id, before.DisplayName, before.DirectoryPath, before.FolderType, before.MediaType);
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(expected);
        (resolved.Id, resolved.DisplayName, resolved.DirectoryPath, resolved.FolderType, resolved.MediaType).ShouldBe(expected);
        (assignment.DefaultDestinationId, assignment.IsEnabled).ShouldBe((11, libraryEnabled));
        if (accepted)
        {
            var value = result.Value.ShouldNotBeNull();
            (value.Id, value.DisplayName, value.Directory, value.FolderType, value.MediaType).ShouldBe(expected);
        }
        else
            result.Value.ShouldBeNull();
    }

    [Test]
    public async Task ShouldAllowRoleChangeWithoutChangingOtherFolders_WhenCustomFolderIsUnassigned()
    {
        // Arrange
        await SetupDatabase(44206);
        var db = IDbContext;
        db.FolderPaths.AddRange(
            NewFolder(11, FolderType.DownloadFolder, PlexMediaType.None),
            NewFolder(12, FolderType.DownloadFolder, PlexMediaType.None));
        await db.SaveChangesAsync(CancellationToken);
        var control = await db.FolderPaths.SingleAsync(x => x.Id == 12, CancellationToken);

        // Act
        var outcome = await TestEndpointHandleAsync(Request(11, FolderType.MovieFolder, PlexMediaType.Movie));
        var after = await db.FolderPaths.SingleAsync(x => x.Id == 11, CancellationToken);
        var afterControl = await db.FolderPaths.SingleAsync(x => x.Id == 12, CancellationToken);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var result = outcome.Response.ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        var value = result.Value.ShouldNotBeNull();
        (value.Id, value.DisplayName, value.Directory, value.FolderType, value.MediaType).ShouldBe(
            (11, "Changed name", "/tmp/folder-role-new", FolderType.MovieFolder, PlexMediaType.Movie));
        (after.Id, after.DisplayName, after.DirectoryPath, after.FolderType, after.MediaType).ShouldBe(
            (11, "Changed name", "/tmp/folder-role-new", FolderType.MovieFolder, PlexMediaType.Movie));
        (afterControl.Id, afterControl.DisplayName, afterControl.DirectoryPath, afterControl.FolderType, afterControl.MediaType).ShouldBe(
            (control.Id, control.DisplayName, control.DirectoryPath, control.FolderType, control.MediaType));
    }

    private static FolderPath NewFolder(int id, FolderType folderType, PlexMediaType mediaType) => new()
    {
        Id = id,
        DisplayName = "Original name",
        DirectoryPath = "/tmp/folder-role-old",
        FolderType = folderType,
        MediaType = mediaType,
    };

    private static UpdateFolderPathEndpointRequest Request(int id, FolderType folderType, PlexMediaType mediaType) => new()
    {
        FolderPathDTO = new FolderPathDTO
        {
            Id = id,
            DisplayName = "Changed name",
            Directory = "/tmp/folder-role-new",
            FolderType = folderType,
            MediaType = mediaType,
            IsDefault = false,
            IsValid = true,
        },
    };

    private async Task<(IReaparrDbContext Db, IntegrationIdentity Identity)> SetupAssignedDownloadFolderAsync(
        IntegrationType integrationType)
    {
        await SetupDatabase(44204, options =>
        {
            options.SonarrIntegrationCount = integrationType == IntegrationType.Sonarr ? 1 : 0;
            options.RadarrIntegrationCount = integrationType == IntegrationType.Radarr ? 1 : 0;
        });
        var db = IDbContext;
        db.FolderPaths.Add(NewFolder(11, FolderType.DownloadFolder, PlexMediaType.None));
        await db.SaveChangesAsync(CancellationToken);
        Guid integrationId;
        if (integrationType == IntegrationType.Sonarr)
        {
            integrationId = await db.SonarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
            await db.SonarrIntegrations.ExecuteUpdateAsync(x => x.SetProperty(y => y.DownloadFolderId, 11), CancellationToken);
        }
        else
        {
            integrationId = await db.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
            await db.RadarrIntegrations.ExecuteUpdateAsync(x => x.SetProperty(y => y.DownloadFolderId, 11), CancellationToken);
        }
        return (db, new IntegrationIdentity(integrationType, integrationId));
    }
}
