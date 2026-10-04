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
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldMapSortedDescendantsAndOriginals_WithoutRestartingRootRetrieval(PlexMediaType mediaType)
    {
        // Arrange
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625625)).Generate();
        var library = CreateDescendantLibrary(mediaType, template);
        var parentKey = mediaType == PlexMediaType.MusicTrack ? "101" : "100";
        var source = template with
        {
            RatingKey = 103,
            Type = mediaType,
            ParentRatingKey = parentKey,
            SortTitle = "Item 10",
            Duration = 17,
            Media =
            [
                template.Media.Single() with
                {
                    Id = 901,
                    Duration = 17000,
                    Parts =
                    [
                        template.Media.Single().Parts.First() with
                        {
                            Id = 1001,
                            Duration = 17000,
                            Size = 4200,
                            File = "/source/item.bin",
                            Key = "/library/parts/1001/file.bin",
                        },
                    ],
                },
            ],
        };
        var earlier = source with
        {
            RatingKey = 104,
            SortTitle = "Item 2",
            Type = mediaType == PlexMediaType.PhotoImage ? PlexMediaType.OtherVideos : mediaType,
        };
        Mock.SetupCommand(() =>
                It.Is<GetAllMediaByTypeFromPlexApiCommand>(x => x.PlexLibrary == library && x.MediaType == mediaType)
            )
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { source, earlier }))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new GetLibraryMediaFromPlexApiCommand(library, mediaType),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Library.ShouldBeSameAs(library);
        result.Errors.Count.ShouldBe(0);
        var items = mediaType switch
        {
            PlexMediaType.MusicAlbum => library.Albums.Select(x => (x.PlexApiRatingKey, x.SortIndex)),
            PlexMediaType.MusicTrack => library.Tracks.Select(x => (x.PlexApiRatingKey, x.SortIndex)),
            _ => library.PhotoImages.Select(x => (x.PlexApiRatingKey, x.SortIndex)),
        };
        items.ShouldBe([(104, 1), (103, 2)]);
        if (mediaType == PlexMediaType.MusicAlbum)
        {
            library.Albums.ShouldAllBe(x => x.PlexArtist == null);
            library.Albums.ShouldAllBe(x => x.ParentKey == library.Music.Single().PlexApiRatingKey);
        }
        else if (mediaType == PlexMediaType.MusicTrack)
        {
            var track = library.Tracks.Single(x => x.PlexApiRatingKey == 103);
            track.PlexAlbum.ShouldBeNull();
            track.ParentKey.ShouldBe(library.Albums.Single().PlexApiRatingKey);
            (track.Duration, track.MediaSize).ShouldBe((17, 4200L));
            track
                .MediaDataList.Select(x => (x.PlexApiMediaId, x.PlexApiPartId, x.Duration, x.Size, x.OriginalFilename))
                .ShouldBe([(901, 1001, 17000, 4200L, "item.bin")]);
            track.MediaDataList.Single().PlexTrack.ShouldBeSameAs(track);
        }
        else
        {
            var photo = library.PhotoImages.Single(x => x.PlexApiRatingKey == 103);
            photo.PlexPhotoAlbum.ShouldBeNull();
            photo.ParentKey.ShouldBe(library.PhotoAlbums.Single().PlexApiRatingKey);
            (photo.Duration, photo.MediaSize).ShouldBe((17, 4200L));
            photo
                .MediaDataList.Select(x => (x.PlexApiMediaId, x.PlexApiPartId, x.Duration, x.Size, x.OriginalFilename))
                .ShouldBe([(901, 1001, 17000, 4200L, "item.bin")]);
            photo.MediaDataList.Single().PlexPhoto.ShouldBeSameAs(photo);
            result.Value.PhotoClipCount.ShouldBe(1);
        }
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetLibrarySectionsCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.StartAsync(It.IsAny<int>(), It.IsAny<PlexMediaType>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldMapDescendantsWithoutResolvingParents_WhenRetrievedParentIsMissing(PlexMediaType mediaType)
    {
        // Arrange
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625627)).Generate();
        var library = CreateDescendantLibrary(mediaType, template);
        var valid = template with
        {
            RatingKey = 103,
            Type = mediaType,
            ParentRatingKey = mediaType == PlexMediaType.MusicTrack ? "101" : "100",
            SortTitle = "Item 1",
        };
        var orphan = valid with { RatingKey = 104, ParentRatingKey = "999", SortTitle = "Item 2" };
        var albums = library.Albums.ToArray();
        var tracks = library.Tracks.ToArray();
        var photos = library.PhotoImages.ToArray();
        Mock.SetupCommand(() =>
                It.Is<GetAllMediaByTypeFromPlexApiCommand>(x => x.PlexLibrary == library && x.MediaType == mediaType)
            )
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { valid, orphan }))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new GetLibraryMediaFromPlexApiCommand(library, mediaType),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var mapped = mediaType switch
        {
            PlexMediaType.MusicAlbum => library.Albums.Select(x => (x.PlexApiRatingKey, x.ParentKey)),
            PlexMediaType.MusicTrack => library.Tracks.Select(x => (x.PlexApiRatingKey, x.ParentKey)),
            _ => library.PhotoImages.Select(x => (x.PlexApiRatingKey, x.ParentKey)),
        };
        mapped.ShouldBe([(103, int.Parse(valid.ParentRatingKey)), (104, 999)]);
        if (mediaType != PlexMediaType.MusicAlbum)
            library.Albums.ShouldBe(albums);
        if (mediaType != PlexMediaType.MusicTrack)
            library.Tracks.ShouldBe(tracks);
        if (mediaType != PlexMediaType.PhotoImage)
            library.PhotoImages.ShouldBe(photos);
        Mock.Mock<ICommandExecutor>().Verify();
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

    [Test]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldClearOnlyRequestedDescendants_WhenRetrievalConfirmsEmpty(PlexMediaType mediaType)
    {
        // Arrange
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625629)).Generate();
        var library = CreateDescendantLibrary(mediaType, template);
        var artists = library.Music.ToArray();
        var photoAlbums = library.PhotoAlbums.ToArray();
        var albums = library.Albums.ToArray();
        var tracks = library.Tracks.ToArray();
        Mock.SetupCommand(() =>
                It.Is<GetAllMediaByTypeFromPlexApiCommand>(x => x.PlexLibrary == library && x.MediaType == mediaType)
            )
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO>()))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new GetLibraryMediaFromPlexApiCommand(library, mediaType),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        library.Music.ShouldBe(artists);
        library.PhotoAlbums.ShouldBe(photoAlbums);
        library.Albums.ShouldBe(mediaType == PlexMediaType.MusicAlbum ? [] : albums);
        library.Tracks.ShouldBe(mediaType == PlexMediaType.MusicTrack ? [] : tracks);
        library.PhotoImages.ShouldBeEmpty();
        result.Value.PhotoClipCount.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
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
