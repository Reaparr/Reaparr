using Reaparr.Application.Contracts;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class GetLibraryMediaFromPlexApiCommandHandlerUnitTests : BaseUnitTest<GetLibraryMediaFromPlexApiCommandHandler>
{
    [Test]
    public async Task ShouldPublishOriginalErrorWithoutFetchingMedia_WhenSectionRetrievalFails()
    {
        // Arrange
        var library = CreateLibrary(PlexMediaType.Movie);
        var error = new Error("Section request failed");
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == library.PlexServerId))
            .ReturnsAsync(Result.Fail<List<PlexLibrary>>(error)).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, library.Type, CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1 && r.Errors[0] == error), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldBe([error]);
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.IsAny<GetAllMediaByTypeFromPlexApiCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldReportNotFoundWithoutFetchingMedia_WhenSectionDisappears()
    {
        // Arrange
        var library = CreateLibrary(PlexMediaType.Movie);
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == library.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary>())).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, library.Type, CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.Has404NotFoundError() && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.Has404NotFoundError().ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.IsAny<GetAllMediaByTypeFromPlexApiCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldPublishFailureWithoutReturningPartialMetadata_WhenMediaRetrievalFails()
    {
        // Arrange
        var library = CreateLibrary(PlexMediaType.Movie);
        var error = new Error("Media request failed");
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == library.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { library })).Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<GetAllMediaByTypeFromPlexApiCommand>(
                x => x.PlexLibrary.Id == library.Id && x.MediaType == PlexMediaType.Movie))
            .ReturnsAsync(Result.Fail<List<LibraryMediaItemDTO>>(error)).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, library.Type, CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1 && r.Errors[0] == error), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldBe([error]);
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.IsAny<GetDetailMetadataByRatingKeysCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Movie, PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow, PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.Music, PlexMediaType.Artist)]
    [Arguments(PlexMediaType.Photos, PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos, PlexMediaType.OtherVideos)]
    public async Task ShouldReturnNaturallySortedRootsWithoutFetchingDescendants_WhenLibraryIsSupported(
        PlexMediaType type,
        PlexMediaType rootType
    )
    {
        // Arrange
        var original = CreateLibrary(type);
        var section = CreateLibrary(type);
        section.Id = 0;
        section.PlexServerId = 0;
        var sources = FakeData.GetLibraryMediaItemDTO(new Seed(9101), mediaType: rootType)
            .RuleFor(x => x.Title, "Root")
            .RuleFor(x => x.Guid, string.Empty)
            .Generate(3);
        sources[0] = sources[0] with { Title = "Item 10", SortTitle = "Item 10", RatingKey = 110 };
        sources[1] = sources[1] with { Title = "Item 2", SortTitle = "Item 2", RatingKey = 102 };
        sources[2] = sources[2] with { Title = "Item 1", SortTitle = "Item 1", RatingKey = 101 };
        var countries = sources.SelectMany(x => x.Country).ToArray();
        var genres = sources.SelectMany(x => x.Genre).ToArray();
        var actors = sources.SelectMany(x => x.Role).ToArray();
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == original.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { section })).Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<GetAllMediaByTypeFromPlexApiCommand>(
                x => x.PlexLibrary.Id == original.Id && x.PlexLibrary.PlexServerId == original.PlexServerId
                    && x.MediaType == rootType))
            .ReturnsAsync(Result.Ok(sources)).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(original.Id, type, CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(original), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Library.Id.ShouldBe(original.Id);
        result.Value.Library.PlexServerId.ShouldBe(original.PlexServerId);
        result.Value.Countries.ShouldBe(countries, ignoreOrder: true);
        result.Value.Genres.ShouldBe(genres, ignoreOrder: true);
        result.Value.Actors.ShouldBe(actors, ignoreOrder: true);
        var library = result.Value.Library;
        var media = type switch
        {
            PlexMediaType.Movie => library.Movies.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            PlexMediaType.TvShow => library.TvShows.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            PlexMediaType.Music => library.Artists.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            PlexMediaType.Photos => library.PhotoAlbums.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            _ => library.OtherVideos.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
        };
        media.ShouldBe([(101, "Item 1", 1), (102, "Item 2", 2), (110, "Item 10", 3)]);
        library.Albums.ShouldBeEmpty();
        library.Tracks.ShouldBeEmpty();
        library.Photos.ShouldBeEmpty();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.Is<GetAllMediaByTypeFromPlexApiCommand>(c => c.MediaType != rootType),
            It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.IsAny<GetDetailMetadataByRatingKeysCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateErrorAsync(
            It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Music)]
    [Arguments(PlexMediaType.Photos)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldReturnEmptyRootsWithoutFetchingDescendants_WhenRootCollectionIsEmpty(PlexMediaType type)
    {
        // Arrange
        var library = CreateLibrary(type);
        var rootType = type switch
        {
            PlexMediaType.Music => PlexMediaType.Artist,
            PlexMediaType.Photos => PlexMediaType.PhotoAlbum,
            _ => PlexMediaType.OtherVideos,
        };
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == library.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { library })).Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<GetAllMediaByTypeFromPlexApiCommand>(
                x => x.PlexLibrary.Id == library.Id && x.MediaType == rootType))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO>())).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, type, CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Library.Type.ShouldBe(type);
        result.Value.Library.Artists.ShouldBeEmpty();
        result.Value.Library.Albums.ShouldBeEmpty();
        result.Value.Library.Tracks.ShouldBeEmpty();
        result.Value.Library.PhotoAlbums.ShouldBeEmpty();
        result.Value.Library.Photos.ShouldBeEmpty();
        result.Value.Library.OtherVideos.ShouldBeEmpty();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.IsAny<GetDetailMetadataByRatingKeysCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateErrorAsync(
            It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private static PlexLibrary CreateLibrary(PlexMediaType type) => new()
    {
        Id = 4,
        PlexServerId = 9,
        Key = "17",
        Type = type,
        Title = "Source library",
        Uuid = "source-library",
        Language = "en",
        CreatedAt = null,
        UpdatedAt = null,
        ScannedAt = null,
        ContentChangedAt = 1,
    };
}
