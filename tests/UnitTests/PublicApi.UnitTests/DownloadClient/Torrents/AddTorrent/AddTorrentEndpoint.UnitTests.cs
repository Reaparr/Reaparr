using BencodeNET.Objects;
using BencodeNET.Torrents;
using Reaparr.Application.Contracts;

// ReSharper disable RedundantAssignment

namespace Reaparr.PublicAPI.UnitTests;

public class AddTorrentEndpointUnitTests : BaseEndpointUnitTest<AddTorrentEndpoint, AddTorrentEndpointRequest>
{
    [Test]
    public async Task ShouldReturnOk_WhenValidTorrentFileIsUploaded()
    {
        // Arrange
        await SetupDatabase(
            1234,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        var validMetadata = CreateValidTorrentMetadata() with { PlexApiPartId = movieFile.PlexApiPartId };
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(validMetadata, "test.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);

        // Verify command was called with correct data
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<CreateDownloadTasksCommand>(cmd =>
                            cmd.Request.DownloadMedias.Count == 1
                            && cmd.Request.DownloadMedias[0].Type == validMetadata.Type
                            && cmd.Request.DownloadMedias[0].PlexServerId == validMetadata.ServerId
                            && cmd.Request.DownloadMedias[0].PlexLibraryId == validMetadata.LibraryId
                            && cmd.Request.DownloadMedias[0].MediaIds.Contains(validMetadata.MediaId)
                            && cmd.Request.DownloadMedias[0].Qualities.Count == 1
                            && cmd.Request.DownloadMedias[0].Qualities[0].MediaDataType == validMetadata.Type
                            && cmd.Request.DownloadMedias[0].Qualities[0].Quality == validMetadata.Quality
                            && cmd.Request.DownloadMedias[0].Qualities[0].MediaId == validMetadata.MediaId
                            && cmd.Request.DownloadMedias[0].Qualities[0].DataId == validMetadata.DataId
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);
    }

    [Test]
    public async Task ShouldReturnValidationErrors_WhenTorrentMetadataIsInvalid()
    {
        // Arrange
        var invalidMetadata = new TorrentMetadataDTO
        {
            Type = PlexMediaType.Movie,
            MediaId = 0, // Invalid - should be > 0
            DataId = -1, // Invalid - should be > 0
            PartId = 0, // Invalid - should be > 0
            PlexApiPartId = 0, // Invalid - should be > 0
            Quality = VideoQuality.HD,
            LibraryId = 0, // Invalid - should be > 0
            ServerId = 0, // Invalid - should be > 0
        };

        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(invalidMetadata, "invalid.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.ShouldNotBeEmpty();
        endpointResult.ValidationErrors.Count.ShouldBeGreaterThan(0);

        // Verify command was not called
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);
    }

    [Test]
    public async Task ShouldReturnFail_WhenCreateDownloadTasksCommandFails()
    {
        // Arrange
        await SetupDatabase(1235, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var validMetadata = CreateValidTorrentMetadata();
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(validMetadata, "test.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        var failureResult = Result.Fail("Command execution failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);

        // Verify command was called once
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);
    }

    [Test]
    public void TorrentMetadataDTOValidator_ShouldValidateAllRequiredFields()
    {
        // Arrange
        var validator = new TorrentMetadataDTOValidator();
        var validMetadata = CreateValidTorrentMetadata();

        // Act
        var result = validator.Validate(validMetadata);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void TorrentMetadataDTOValidator_ShouldFailValidation_WhenFieldsAreInvalid()
    {
        // Arrange
        var validator = new TorrentMetadataDTOValidator();
        var invalidMetadata = new TorrentMetadataDTO
        {
            Type = (PlexMediaType)999, // Invalid enum value
            MediaId = 0,
            DataId = 0,
            PartId = 0,
            PlexApiPartId = 0,
            Quality = (VideoQuality)999, // Invalid enum value
            LibraryId = 0,
            ServerId = 0,
        };

        // Act
        var result = validator.Validate(invalidMetadata);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThan(0);

        // Check specific validation errors
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.LibraryId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.ServerId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.MediaId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.DataId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.PartId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.PlexApiPartId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.Type));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.Quality));
    }

    [Test]
    public void AddTorrentEndpointRequestValidator_ShouldRequireTorrentFile()
    {
        // Arrange
        var validator = new AddTorrentEndpointRequestValidator();
        var request = new AddTorrentEndpointRequest { TorrentFile = null };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(AddTorrentEndpointRequest.TorrentFile));
    }

    [Test]
    public void TorrentMetadataDTOValidator_ShouldRejectUnsupportedMediaTypes()
    {
        // Arrange
        var validator = new TorrentMetadataDTOValidator();
        var request = CreateValidTorrentMetadata(PlexMediaType.Season);

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.Type));
    }

    [Test]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.MusicArtist)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.MusicArtist)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.MusicAlbum)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.MusicAlbum)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.MusicTrack)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.MusicTrack)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.PhotoAlbum)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.PhotoAlbum)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.PhotoImage)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.PhotoImage)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.OtherVideos)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.OtherVideos)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.Movie)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.Episode)]
    public async Task ShouldRejectUnsupportedOrOppositeFamilyWithoutCreatingTasks_WhenIntegrationUploadsTorrent(
        IntegrationType integrationType,
        PlexMediaType mediaType
    )
    {
        // Arrange
        await SetupDatabase(88390, config =>
        {
            config.MovieDownloadTasksCount = 1;
            config.SonarrIntegrationCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integrationId = integrationType == IntegrationType.Sonarr
            ? await dbContext.SonarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken)
            : await dbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var before = await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ParentId, x.HashId, x.DownloadStatus })
            .ToArrayAsync(CancellationToken);
        before.Length.ShouldBe(1);
        var metadata = CreateValidTorrentMetadata(mediaType);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(metadata, "unsupported.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        // Act
        var response = await TestEndpointHandleAsync(
            request,
            integrationIdentity: new IntegrationIdentity(integrationType, integrationId)
        );

        // Assert
        if (mediaType is PlexMediaType.Movie or PlexMediaType.Episode)
            response.StatusCode.ShouldBe(403);
        else
        {
            response.IsValid.ShouldBeFalse();
            response.ValidationErrors.Count.ShouldBe(1);
            response.ValidationErrors.Single().PropertyName.ShouldBe("GeneralErrors");
            response.ValidationErrors.Single().ErrorMessage.ShouldBe("Type");
        }
        (await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ParentId, x.HashId, x.DownloadStatus })
            .ToArrayAsync(CancellationToken)).ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Once());
    }

    [Test]
    public async Task ShouldSetHashIdOnMovieDownloadTask_WhenTorrentIsMovieType()
    {
        // Arrange
        var movieMetadata = CreateValidTorrentMetadata();
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(movieMetadata, "movie.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        // Setup database with movie download tasks that match the metadata
        await SetupDatabase(
            1234,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        // Get the actual server and library IDs from the database
        var server = await IDbContext.PlexServers.FirstAsync(CancellationToken);
        var library = await IDbContext.PlexLibraries.FirstAsync(CancellationToken);
        using var movieContext = IDbContext;
        var movieFile = await movieContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        var movie = await movieContext.DownloadTaskMovie.AsTracking().FirstAsync(CancellationToken);
        movie.RadarrIntegrationId = integration.Id;
        movieFile.RadarrIntegrationId = integration.Id;
        await movieContext.SaveChangesAsync(CancellationToken);

        // Create metadata with the actual database IDs
        var actualMetadata = new TorrentMetadataDTO
        {
            Type = PlexMediaType.Movie,
            MediaId = 1,
            DataId = 1,
            PartId = 1,
            PlexApiPartId = movieFile.PlexApiPartId,
            Quality = VideoQuality.HD,
            LibraryId = library.Id,
            ServerId = server.Id,
        };

        // Update the torrent file with the correct metadata
        var (actualTorrentFile, actualTorrentFileMock) = CreateMockTorrentFile(actualMetadata, "movie.torrent");
        request = new AddTorrentEndpointRequest { TorrentFile = actualTorrentFile };

        // Update the mock references for verification
        torrentFileMock = actualTorrentFileMock;
        movieMetadata = actualMetadata;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);

        // Verify command was called once
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);

        // Verify that the HashId was set on the movie file
        var updatedMovieFile = await IDbContext.DownloadTaskMovieFile.FirstOrDefaultAsync(
            x =>
                x.PlexLibraryId == movieMetadata.LibraryId
                && x.PlexServerId == movieMetadata.ServerId
                && x.PlexApiPartId == movieMetadata.PlexApiPartId,
            CancellationToken
        );

        updatedMovieFile.ShouldNotBeNull();
        updatedMovieFile.HashId.ShouldNotBeNull();
        updatedMovieFile.HashId.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldSetHashIdOnEpisodeDownloadTask_WhenTorrentIsEpisodeType()
    {
        // Arrange
        var episodeMetadata = CreateValidTorrentMetadata(PlexMediaType.Episode);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(episodeMetadata, "episode.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        // Setup database with TV show download tasks that match the metadata
        await SetupDatabase(
            5678,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.SonarrIntegrationCount = 1;
            }
        );
        var integration = await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        // Get the actual server and library IDs from the database
        var server = await IDbContext.PlexServers.FirstAsync(CancellationToken);
        var library = await IDbContext.PlexLibraries.FirstAsync(CancellationToken);
        using var episodeContext = IDbContext;
        var episodeFile = await episodeContext.DownloadTaskTvShowEpisodeFile.AsTracking().FirstAsync(CancellationToken);
        var episode = await episodeContext.DownloadTaskTvShowEpisode.AsTracking().FirstAsync(CancellationToken);
        episode.SonarrIntegrationId = integration.Id;
        episodeFile.SonarrIntegrationId = integration.Id;
        await episodeContext.SaveChangesAsync(CancellationToken);

        // Create metadata with the actual database IDs
        var actualMetadata = new TorrentMetadataDTO
        {
            Type = PlexMediaType.Episode,
            MediaId = 1,
            DataId = 1,
            PartId = 1,
            PlexApiPartId = episodeFile.PlexApiPartId,
            Quality = VideoQuality.HD,
            LibraryId = library.Id,
            ServerId = server.Id,
        };

        // Update the torrent file with the correct metadata
        var (actualTorrentFile, actualTorrentFileMock) = CreateMockTorrentFile(actualMetadata, "episode.torrent");
        request = new AddTorrentEndpointRequest { TorrentFile = actualTorrentFile };

        // Update the mock references for verification
        torrentFileMock = actualTorrentFileMock;
        episodeMetadata = actualMetadata;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToSonarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);

        // Verify command was called once
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);

        // Verify that the HashId was set on the episode file
        var updatedEpisodeFile = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstOrDefaultAsync(
            x =>
                x.PlexLibraryId == episodeMetadata.LibraryId
                && x.PlexServerId == episodeMetadata.ServerId
                && x.PlexApiPartId == episodeMetadata.PlexApiPartId,
            CancellationToken
        );

        updatedEpisodeFile.ShouldNotBeNull();
        updatedEpisodeFile.HashId.ShouldNotBeNull();
        updatedEpisodeFile.HashId.ShouldNotBeEmpty();
    }

    private static TorrentMetadataDTO CreateValidTorrentMetadata(PlexMediaType type = PlexMediaType.Movie) =>
        new()
        {
            Type = type,
            MediaId = 1,
            DataId = 1,
            PartId = 1,
            PlexApiPartId = 12345,
            Quality = VideoQuality.HD,
            LibraryId = 1,
            ServerId = 1,
        };

    private static (IFormFile file, Mock<IFormFile> mock) CreateMockTorrentFile(
        TorrentMetadataDTO metadata,
        string fileName
    )
    {
        // Create a proper torrent using BencodeNET.Torrents.Torrent class like in DownloadTorrentEndpoint
        var extraFields = new BDictionary
        {
            ["created by"] = new BString("Reaparr"),
            ["comment"] = new BString("Test torrent for unit tests"),

            // Add metadata as extra fields
            [nameof(TorrentMetadataDTO.Type)] = new BString(metadata.Type.ToString()),
            [nameof(TorrentMetadataDTO.MediaId)] = new BString(metadata.MediaId.ToString()),
            [nameof(TorrentMetadataDTO.DataId)] = new BString(metadata.DataId.ToString()),
            [nameof(TorrentMetadataDTO.PartId)] = new BString(metadata.PartId.ToString()),
            [nameof(TorrentMetadataDTO.PlexApiPartId)] = new BString(metadata.PlexApiPartId.ToString()),
            [nameof(TorrentMetadataDTO.Quality)] = new BString(metadata.Quality.ToString()),
            [nameof(TorrentMetadataDTO.LibraryId)] = new BString(metadata.LibraryId.ToString()),
            [nameof(TorrentMetadataDTO.ServerId)] = new BString(metadata.ServerId.ToString()),
        };

        var torrent = new Torrent
        {
            CreationDate = DateTime.UtcNow,
            Pieces = new byte[20], // Single piece hash (20 bytes SHA1)
            PieceSize = 32768,
            Trackers =
            [
                ["http://tracker.example.com/announce"],
            ],
            File = new SingleFileInfo { FileName = "test.mkv", FileSize = 1000000 },
            ExtraFields = extraFields,
        };

        var torrentBytes = torrent.EncodeAsBytes();

        var formFileMock = new Mock<IFormFile>();
        formFileMock.Setup(f => f.FileName).Returns(fileName);
        formFileMock.Setup(f => f.Length).Returns(torrentBytes.Length);
        formFileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(torrentBytes));
        formFileMock.Setup(f => f.ContentType).Returns("application/x-bittorrent");

        return (formFileMock.Object, formFileMock);
    }
}
