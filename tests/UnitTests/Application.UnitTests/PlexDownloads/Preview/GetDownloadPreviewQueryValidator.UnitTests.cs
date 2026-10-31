namespace Reaparr.Application.UnitTests;

public class GetDownloadPreviewQueryValidatorUnitTests : BaseUnitTest<GetDownloadPreviewQueryValidator>
{
    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldAcceptPhotoSelection_WhenQualitiesAreEmpty(PlexMediaType type)
    {
        // Arrange
        var query = CreateQuery([CreateSelection(type)]);

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldRejectPhotoSelection_WhenQualitySelectorIsPresent(PlexMediaType type)
    {
        // Arrange
        var query = CreateQuery([CreateSelection(type, qualities: [CreateQuality(PlexMediaType.PhotoImage)])]);

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ErrorMessage.ShouldBe("Photo selections must not include quality or part selectors.");
    }

    [Test]
    public async Task ShouldStillAcceptMovieQualitySelector_WhenPhotoRuleDoesNotApply()
    {
        // Arrange
        var query = CreateQuery([
            CreateSelection(PlexMediaType.Movie, qualities: [CreateQuality(PlexMediaType.Movie)]),
        ]);

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist, false)]
    [Arguments(PlexMediaType.MusicArtist, true)]
    [Arguments(PlexMediaType.MusicAlbum, false)]
    [Arguments(PlexMediaType.MusicAlbum, true)]
    [Arguments(PlexMediaType.MusicTrack, false)]
    [Arguments(PlexMediaType.MusicTrack, true)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldAcceptMusicOrOtherVideoSelection_WhenAloneOrMixedWithMovie(
        PlexMediaType type,
        bool mixedWithMovie
    )
    {
        // Arrange
        var selections = new List<DownloadMediaDTO> { CreateSelection(type, ids: (1, 11, 101)) };
        if (mixedWithMovie)
            selections.Insert(0, CreateSelection(PlexMediaType.Movie, ids: (1, 11, 102)));

        // Act
        var result = await Sut.ValidateAsync(CreateQuery(selections), CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldAcceptOrdinaryMixedRequest_WhenEverySupportedSelectionLevelIsPresent()
    {
        // Arrange
        var supportedTypes = new[]
        {
            PlexMediaType.Movie,
            PlexMediaType.TvShow,
            PlexMediaType.Season,
            PlexMediaType.Episode,
            PlexMediaType.MusicArtist,
            PlexMediaType.MusicAlbum,
            PlexMediaType.MusicTrack,
            PlexMediaType.PhotoAlbum,
            PlexMediaType.PhotoImage,
            PlexMediaType.OtherVideos,
        };
        var query = CreateQuery(
            supportedTypes.Select((type, index) => CreateSelection(type, ids: (index + 1, index + 11, index + 101)))
        );

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(PlexMediaType.None)]
    [Arguments(PlexMediaType.Games)]
    [Arguments(PlexMediaType.Unknown)]
    [Arguments((PlexMediaType)int.MaxValue)]
    public async Task ShouldRejectUnsupportedMediaType_WhenSelectionIsOtherwiseValid(PlexMediaType type)
    {
        // Arrange
        var query = CreateQuery([CreateSelection(type, ids: (1, 11, 101))]);
        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().PropertyName.ShouldBe("Request.DownloadMedias[0].Type");
    }

    [Test]
    public async Task ShouldAcceptFolderSelection_WhenSelectionKindIsFolder()
    {
        // Arrange
        var query = CreateQuery([CreateSelectionWithKind(PlexMediaType.PhotoImage, PhotoItemKind.Folder)]);

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldRejectPhotoSelectionWithoutExplicitKind()
    {
        // Arrange
        var query = CreateQuery([CreateSelectionWithoutKind(PlexMediaType.PhotoImage)]);

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.ErrorMessage.Contains("Photo album selections"));
    }

    [Test]
    [Arguments(PlexMediaType.PhotoImage, "Asset", PhotoItemKind.Asset)]
    [Arguments(PlexMediaType.PhotoImage, "Folder", PhotoItemKind.Folder)]
    [Arguments(PlexMediaType.PhotoAlbum, "Album", PhotoItemKind.Album)]
    public async Task ShouldDeserializePhotoSelectionKind_WhenApiUsesStringEnum(
        PlexMediaType type,
        string selectionKind,
        PhotoItemKind expectedSelectionKind
    )
    {
        // Arrange
        var json = $$"""
            {
              "downloadMedias": [
                {
                  "plexServerId": 14,
                  "plexLibraryId": 46,
                  "mediaIds": [112],
                  "type": "{{type}}",
                  "selectionKind": "{{selectionKind}}",
                  "qualities": [],
                  "keepCompletedInDownloadFolder": false
                }
              ],
              "destinationFolderPathId": 5,
              "customDestinationFolderPath": ""
            }
            """;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Act
        var request = JsonSerializer.Deserialize<DownloadPreviewRequest>(json, options);

        // Assert
        request.ShouldNotBeNull();
        request.DownloadMedias.Count.ShouldBe(1);
        var selection = request.DownloadMedias.Single();
        selection.Type.ShouldBe(type);
        selection.SelectionKind.ShouldBe(expectedSelectionKind);
        selection.PlexServerId.ShouldBe(14);
        selection.PlexLibraryId.ShouldBe(46);
        selection.MediaIds.ShouldBe([112]);
        selection.Qualities.ShouldBeEmpty();
        selection.KeepCompletedInDownloadFolder.ShouldBeFalse();
        request.DestinationFolderPathId.ShouldBe(5);
        request.CustomDestinationFolderPath.ShouldBeEmpty();

        var validationResult = await Sut.ValidateAsync(new GetDownloadPreviewQuery(request), CancellationToken);
        validationResult.IsValid.ShouldBeTrue();
        validationResult.Errors.Count.ShouldBe(0);
    }

    private static GetDownloadPreviewQuery CreateQuery(IEnumerable<DownloadMediaDTO> selections) =>
        new(new DownloadPreviewRequest { DownloadMedias = selections.ToList() });

    private static DownloadMediaDTO CreateSelection(
        PlexMediaType type,
        List<PlexMediaQualityDTO>? qualities = null,
        (int ServerId, int LibraryId, int MediaId)? ids = null
    ) => CreateSelectionWithKind(type, GetDefaultSelectionKind(type), qualities, ids);

    private static DownloadMediaDTO CreateSelectionWithKind(
        PlexMediaType type,
        PhotoItemKind? selectionKind,
        List<PlexMediaQualityDTO>? qualities = null,
        (int ServerId, int LibraryId, int MediaId)? ids = null
    )
    {
        var (serverId, libraryId, mediaId) = ids ?? (1, 2, 3);
        return new DownloadMediaDTO
        {
            Type = type,
            SelectionKind = selectionKind,
            PlexServerId = serverId,
            PlexLibraryId = libraryId,
            MediaIds = [mediaId],
            Qualities = qualities ?? [],
        };
    }

    private static DownloadMediaDTO CreateSelectionWithoutKind(PlexMediaType type) =>
        CreateSelectionWithKind(type, selectionKind: null);

    private static PhotoItemKind? GetDefaultSelectionKind(PlexMediaType type) =>
        type switch
        {
            PlexMediaType.PhotoAlbum => PhotoItemKind.Album,
            PlexMediaType.PhotoImage => PhotoItemKind.Asset,
            _ => null,
        };

    private static PlexMediaQualityDTO CreateQuality(PlexMediaType type) =>
        new()
        {
            MediaId = 3,
            DataId = 4,
            MediaDataType = type,
            VideoQuality = VideoQuality.Unknown,
        };
}
