namespace Reaparr.Application.UnitTests;

public class SetPlexLibraryDefaultDestinationByIdEndpointUnitTests
    : BaseEndpointUnitTest<
        SetPlexLibraryDefaultDestinationByIdEndpoint,
        SetPlexLibraryDefaultDestinationByIdEndpointRequest,
        BaseResultDTO
    >
{
    [Test]
    [Arguments(PlexMediaType.Movie, FolderType.MovieFolder)]
    [Arguments(PlexMediaType.TvShow, FolderType.TvShowFolder)]
    [Arguments(PlexMediaType.MusicArtist, FolderType.MusicFolder)]
    [Arguments(PlexMediaType.PhotoAlbum, FolderType.PhotosFolder)]
    [Arguments(PlexMediaType.OtherVideos, FolderType.OtherVideosFolder)]
    [Arguments(PlexMediaType.Games, FolderType.GamesVideosFolder)]
    public async Task ShouldUpdateOnlySelectedLibraryAndResolveItsFolder_WhenDestinationIsCompatible(
        PlexMediaType mediaType,
        FolderType folderType
    )
    {
        // Arrange
        var (dbContext, target, control) = await CreateLibraries(mediaType);
        var selected = new FolderPath(folderType, "Selected destination", "/media/selected") { MediaType = mediaType };
        dbContext.FolderPaths.Add(selected);
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = await ReadDestinations(dbContext);
        before.Single(x => x.Id == target.Id).DefaultDestinationId.ShouldBe(mediaType.ToDefaultDestinationFolderId());
        before.Single(x => x.Id == control.Id).DefaultDestinationId.ShouldBe(mediaType.ToDefaultDestinationFolderId());

        // Act
        var response = await TestEndpointHandleAsync(
            new SetPlexLibraryDefaultDestinationByIdEndpointRequest(target.Id, selected.Id)
        );

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(200);
        response.Response.ShouldNotBeNull();
        response.Response.IsSuccess.ShouldBeTrue();
        response.Response.Errors.Count.ShouldBe(0);
        (await ReadDestinations(dbContext)).ShouldBe(
            before.Select(x => x.Id == target.Id ? x with { DefaultDestinationId = selected.Id } : x).ToArray()
        );
        var destination = await dbContext.GetDestinationFolder(target.Id);
        destination.ShouldNotBeNull();
        (destination.Id, destination.DirectoryPath, destination.FolderType, destination.MediaType).ShouldBe(
            (selected.Id, selected.DirectoryPath, folderType, mediaType)
        );
        var controlDestination = await dbContext.GetDestinationFolder(control.Id);
        controlDestination.ShouldNotBeNull();
        controlDestination.Id.ShouldBe(mediaType.ToDefaultDestinationFolderId());
    }

    [Test]
    [Arguments(FolderType.TvShowFolder, PlexMediaType.TvShow)]
    [Arguments(FolderType.DownloadFolder, PlexMediaType.None)]
    [Arguments(FolderType.TvShowFolder, PlexMediaType.Movie)]
    [Arguments(FolderType.MovieFolder, PlexMediaType.TvShow)]
    public async Task ShouldPreservePreviousDestinationAndResolver_WhenFolderIsIncompatible(
        FolderType folderType,
        PlexMediaType folderMediaType
    )
    {
        // Arrange
        var (dbContext, target, control) = await CreateLibraries(PlexMediaType.Movie);
        var incompatible = new FolderPath(folderType, "Incompatible destination", "/media/wrong")
        {
            MediaType = folderMediaType,
        };
        dbContext.FolderPaths.Add(incompatible);
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = await ReadDestinations(dbContext);
        var oldDestination = await dbContext.GetDestinationFolder(target.Id);
        oldDestination.ShouldNotBeNull();
        oldDestination.Id.ShouldBe(PlexMediaType.Movie.ToDefaultDestinationFolderId());

        // Act
        var response = await TestEndpointHandleAsync(
            new SetPlexLibraryDefaultDestinationByIdEndpointRequest(target.Id, incompatible.Id)
        );

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(400);
        response.Response.ShouldNotBeNull();
        response.Response.IsSuccess.ShouldBeFalse();
        response.Response.Errors.Count.ShouldBe(1);
        (await ReadDestinations(dbContext)).ShouldBe(before);
        var destination = await dbContext.GetDestinationFolder(target.Id);
        destination.ShouldNotBeNull();
        (destination.Id, destination.DirectoryPath, destination.FolderType, destination.MediaType).ShouldBe(
            (oldDestination.Id, oldDestination.DirectoryPath, oldDestination.FolderType, oldDestination.MediaType)
        );
        (await dbContext.GetDestinationFolder(control.Id))
            .ShouldNotBeNull()
            .Id.ShouldBe(PlexMediaType.Movie.ToDefaultDestinationFolderId());
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldReturnNotFoundWithoutChangingDestinations_WhenLibraryOrFolderDoesNotExist(
        bool missingLibrary
    )
    {
        // Arrange
        var (dbContext, target, control) = await CreateLibraries(PlexMediaType.Movie);
        var before = await ReadDestinations(dbContext);
        var request = new SetPlexLibraryDefaultDestinationByIdEndpointRequest(
            missingLibrary ? int.MaxValue : target.Id,
            missingLibrary ? PlexMediaType.Movie.ToDefaultDestinationFolderId() : int.MaxValue
        );

        // Act
        var response = await TestEndpointHandleAsync(request);

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(404);
        response.Response.ShouldNotBeNull();
        response.Response.IsSuccess.ShouldBeFalse();
        response.Response.Errors.Count.ShouldBe(1);
        (await ReadDestinations(dbContext)).ShouldBe(before);
        (await dbContext.GetDestinationFolder(target.Id))
            .ShouldNotBeNull()
            .Id.ShouldBe(PlexMediaType.Movie.ToDefaultDestinationFolderId());
        (await dbContext.GetDestinationFolder(control.Id))
            .ShouldNotBeNull()
            .Id.ShouldBe(PlexMediaType.Movie.ToDefaultDestinationFolderId());
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRejectNonpositiveIdsWithoutChangingDestinations_WhenRequestIsInvalid(bool invalidLibrary)
    {
        // Arrange
        var (dbContext, target, _) = await CreateLibraries(PlexMediaType.Movie);
        var before = await ReadDestinations(dbContext);
        var request = new SetPlexLibraryDefaultDestinationByIdEndpointRequest(
            invalidLibrary ? 0 : target.Id,
            invalidLibrary ? PlexMediaType.Movie.ToDefaultDestinationFolderId() : 0
        );

        // Act
        var response = await TestEndpointHandleAsync(request);

        // Assert
        response.IsValid.ShouldBeFalse();
        response.ValidationErrors.Count.ShouldBe(1);
        response
            .ValidationErrors.Single()
            .PropertyName.ShouldBe(
                invalidLibrary
                    ? nameof(SetPlexLibraryDefaultDestinationByIdEndpointRequest.PlexLibraryId)
                    : nameof(SetPlexLibraryDefaultDestinationByIdEndpointRequest.FolderPathId)
            );
        (await ReadDestinations(dbContext)).ShouldBe(before);
    }

    private async Task<(IReaparrDbContext Context, PlexLibrary Target, PlexLibrary Control)> CreateLibraries(
        PlexMediaType mediaType
    )
    {
        await SetupDatabase(54001, config => config.PlexServerCount = 1);
        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var target = FakeData.GetPlexLibrary(new Seed(54002), mediaType).Generate();
        var control = FakeData.GetPlexLibrary(new Seed(54003), mediaType).Generate();
        target.PlexServerId = serverId;
        control.PlexServerId = serverId;
        target.DefaultDestinationId = mediaType.ToDefaultDestinationFolderId();
        control.DefaultDestinationId = mediaType.ToDefaultDestinationFolderId();
        dbContext.PlexLibraries.AddRange(target, control);
        await dbContext.SaveChangesAsync(CancellationToken);
        return (dbContext, target, control);
    }

    private Task<DestinationState[]> ReadDestinations(IReaparrDbContext dbContext) =>
        dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .Select(x => new DestinationState(x.Id, x.DefaultDestinationId))
            .ToArrayAsync(CancellationToken);

    private sealed record DestinationState(int Id, int? DefaultDestinationId);
}
