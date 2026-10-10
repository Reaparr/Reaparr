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
            .ReturnsAsync(Result.Fail<List<PlexLibrary>>(error))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, library.Type, CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateErrorAsync(
                    library.Id,
                    It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1 && r.Errors[0] == error),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldBe([error]);
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetAllMediaByTypeFromPlexApiCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReportNotFoundWithoutFetchingMedia_WhenSectionDisappears()
    {
        // Arrange
        var library = CreateLibrary(PlexMediaType.Movie);
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == library.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary>()))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, library.Type, CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateErrorAsync(
                    library.Id,
                    It.Is<Result>(r => r.Has404NotFoundError() && r.Errors.Count == 1),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.Has404NotFoundError().ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetAllMediaByTypeFromPlexApiCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPublishFailureWithoutReturningPartialMetadata_WhenMediaRetrievalFails()
    {
        // Arrange
        var library = CreateLibrary(PlexMediaType.Movie);
        var error = new Error("Media request failed");
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == library.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { library }))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() =>
                It.Is<GetAllMediaByTypeFromPlexApiCommand>(x =>
                    x.PlexLibrary.Id == library.Id && x.MediaType == PlexMediaType.Movie
                )
            )
            .ReturnsAsync(Result.Fail<List<LibraryMediaItemDTO>>(error))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, library.Type, CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateErrorAsync(
                    library.Id,
                    It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1 && r.Errors[0] == error),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldBe([error]);
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetDetailMetadataByRatingKeysCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.Movie, PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow, PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.MusicArtist, PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum, PlexMediaType.PhotoAlbum)]
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
        var sources = FakeData
            .GetLibraryMediaItemDTO(new Seed(9101), mediaType: rootType)
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
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { section }))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() =>
                It.Is<GetAllMediaByTypeFromPlexApiCommand>(x =>
                    x.PlexLibrary.Id == original.Id
                    && x.PlexLibrary.PlexServerId == original.PlexServerId
                    && x.MediaType == rootType
                )
            )
            .ReturnsAsync(Result.Ok(sources))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(original.Id, type, CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(original), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Library.Id.ShouldBe(original.Id);
        result.Value.Library.PlexServerId.ShouldBe(original.PlexServerId);
        result
            .Value.Countries.Select(x => (x.Key, x.Name))
            .ShouldBe(countries.Select(x => (x.Key, x.Name)), ignoreOrder: true);
        result
            .Value.Genres.Select(x => (x.Key, x.Name))
            .ShouldBe(genres.Select(x => (x.Key, x.Name)), ignoreOrder: true);
        result
            .Value.Actors.Select(x => (x.Key, x.Name))
            .ShouldBe(actors.Select(x => (x.Key, x.Name)), ignoreOrder: true);
        var library = result.Value.Library;
        var media = type switch
        {
            PlexMediaType.Movie => library.Movies.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            PlexMediaType.TvShow => library.TvShows.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            PlexMediaType.MusicArtist => library.Music.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            PlexMediaType.PhotoAlbum => library.PhotoAlbums.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
            _ => library.OtherVideos.Select(x => (x.PlexApiRatingKey, x.Title, x.SortIndex)),
        };
        media.ShouldBe([(101, "Item 1", 1), (102, "Item 2", 2), (110, "Item 10", 3)]);
        if (type == PlexMediaType.PhotoAlbum)
        {
            var source = sources.Single(x => x.RatingKey == 101);
            var album = library.PhotoAlbums.Single(x => x.PlexApiRatingKey == 101);
            (
                album.Year,
                album.Studio,
                album.Summary,
                album.ContentRating,
                album.Rating,
                album.ChildCount,
                album.OriginallyAvailableAt,
                album.HasThumb,
                album.HasArt,
                album.HasTheme,
                album.Guid
            ).ShouldBe(
                (
                    source.Year,
                    source.Studio,
                    source.Summary,
                    source.ContentRating,
                    source.Rating,
                    source.ChildCount,
                    source.OriginallyAvailableAt.ToDateTime(),
                    !string.IsNullOrEmpty(source.Thumb),
                    !string.IsNullOrEmpty(source.Art),
                    !string.IsNullOrEmpty(source.Theme),
                    source.Guid
                )
            );
        }
        library.Albums.ShouldBeEmpty();
        library.Tracks.ShouldBeEmpty();
        library.PhotoImages.ShouldBeEmpty();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<GetAllMediaByTypeFromPlexApiCommand>(c => c.MediaType != rootType),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetDetailMetadataByRatingKeysCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldReturnEmptyRootsWithoutFetchingDescendants_WhenRootCollectionIsEmpty(PlexMediaType type)
    {
        // Arrange
        var library = CreateLibrary(type);
        var rootType = type switch
        {
            PlexMediaType.MusicArtist => PlexMediaType.MusicArtist,
            PlexMediaType.PhotoAlbum => PlexMediaType.PhotoAlbum,
            _ => PlexMediaType.OtherVideos,
        };
        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == library.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { library }))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() =>
                It.Is<GetAllMediaByTypeFromPlexApiCommand>(x =>
                    x.PlexLibrary.Id == library.Id && x.MediaType == rootType
                )
            )
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO>()))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(library.Id, type, CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(library), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Library.Type.ShouldBe(type);
        result.Value.Library.Music.ShouldBeEmpty();
        result.Value.Library.Albums.ShouldBeEmpty();
        result.Value.Library.Tracks.ShouldBeEmpty();
        result.Value.Library.PhotoAlbums.ShouldBeEmpty();
        result.Value.Library.PhotoImages.ShouldBeEmpty();
        result.Value.Library.OtherVideos.ShouldBeEmpty();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetDetailMetadataByRatingKeysCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }



    [Test]
    [Arguments(PlexMediaType.MusicAlbum, false)]
    [Arguments(PlexMediaType.MusicAlbum, true)]
    [Arguments(PlexMediaType.MusicTrack, false)]
    [Arguments(PlexMediaType.MusicTrack, true)]
    [Arguments(PlexMediaType.PhotoImage, false)]
    [Arguments(PlexMediaType.PhotoImage, true)]
    public async Task ShouldPreserveDescendantsAndCancellation_WhenRetrievalFails(
        PlexMediaType mediaType,
        bool cancelled
    )
    {
        // Arrange
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625628)).Generate();
        var library = CreateDescendantLibrary(mediaType, template);
        var albums = library.Albums.ToArray();
        var tracks = library.Tracks.ToArray();
        var photos = library.PhotoImages.ToArray();
        var failure = cancelled ? ResultExtensions.TaskIsCancelled("descendants") : Result.Fail("incomplete page");
        Mock.SetupCommand(() =>
                It.Is<GetAllMediaByTypeFromPlexApiCommand>(x => x.PlexLibrary == library && x.MediaType == mediaType)
            )
            .ReturnsAsync(failure)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new GetLibraryMediaFromPlexApiCommand(library, mediaType),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.ShouldBe(failure.Errors);
        library.Albums.ShouldBe(albums);
        library.Tracks.ShouldBe(tracks);
        library.PhotoImages.ShouldBe(photos);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    private static PlexLibrary CreateDescendantLibrary(PlexMediaType mediaType, LibraryMediaItemDTO template)
    {
        var library = CreateLibrary(
            mediaType == PlexMediaType.PhotoImage ? PlexMediaType.PhotoAlbum : PlexMediaType.MusicArtist
        );
        if (mediaType == PlexMediaType.PhotoImage)
        {
            var album = (template with { RatingKey = 100, Type = PlexMediaType.PhotoAlbum }).ToPlexPhotoAlbum();
            library.PhotoAlbums.Add(album);
            var photo = (template with { RatingKey = 201, Type = PlexMediaType.PhotoImage }).ToPlexPhoto();
            photo.ParentKey = album.PlexApiRatingKey;
            photo.PlexPhotoAlbum = album;
            library.PhotoImages.Add(photo);
        }
        else
        {
            var artist = (template with { RatingKey = 100, Type = PlexMediaType.MusicArtist }).ToPlexMusicArtist();
            library.Music.Add(artist);
            var album = (template with { RatingKey = 101, Type = PlexMediaType.MusicAlbum }).ToPlexMusicAlbum();
            album.ParentKey = artist.PlexApiRatingKey;
            album.PlexArtist = artist;
            library.Albums.Add(album);
            var track = (template with { RatingKey = 201, Type = PlexMediaType.MusicTrack }).ToPlexMusicTrack();
            track.ParentKey = album.PlexApiRatingKey;
            track.PlexAlbum = album;
            library.Tracks.Add(track);
        }
        return library;
    }

    private static PlexLibrary CreateLibrary(PlexMediaType type) =>
        new()
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
