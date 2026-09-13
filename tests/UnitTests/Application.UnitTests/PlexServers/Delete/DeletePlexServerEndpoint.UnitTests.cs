using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application.UnitTests;

public class DeletePlexServerEndpointUnitTests
    : BaseEndpointUnitTest<DeletePlexServerEndpoint, DeletePlexServerEndpointRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldDeleteServerAndDirectRelatedData_WhenServerExists()
    {
        // Arrange
        await SetupDatabase(
            91301,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var servers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetServerId = servers[0].Id;
        var otherServerId = servers[1].Id;

        var targetConnectionIds = await dbContext
            .PlexServerConnections.Where(x => x.PlexServerId == targetServerId)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetLibraryIds = await dbContext
            .PlexLibraries.Where(x => x.PlexServerId == targetServerId)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var otherLibraryIds = await dbContext
            .PlexLibraries.Where(x => x.PlexServerId == otherServerId)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        Mock.Mock<IMediaOverviewReadStore>()
            .Setup(x => x.RebuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        var targetMovieDownloadId = Guid.Parse("91301000-0000-0000-0000-000000000001");
        var targetMovieFileDownloadId = Guid.Parse("91301000-0000-0000-0000-000000000002");
        var otherMovieDownloadId = Guid.Parse("91301000-0000-0000-0000-000000000003");
        var otherMovieFileDownloadId = Guid.Parse("91301000-0000-0000-0000-000000000004");
        var targetLibraryId = targetLibraryIds[0];
        var otherLibraryId = otherLibraryIds[0];

        dbContext.DownloadTaskMovie.Add(
            new DownloadTaskMovie
            {
                Id = targetMovieDownloadId,
                PlexApiRatingKey = 913011,
                Title = "Target server movie",
                FullTitle = "Target server movie",
                DownloadStatus = DownloadStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                PlexServerId = targetServerId,
                PlexLibraryId = targetLibraryId,
                Year = 2026,
                DataReceived = 0,
                FileDataTransferred = 0,
                DataTotal = 0,
                DownloadSpeed = 0,
                FileTransferSpeed = 0,
                Children =
                [
                    new DownloadTaskMovieFile
                    {
                        Id = targetMovieFileDownloadId,
                        PlexApiRatingKey = 913011,
                        Title = "Target server movie file",
                        FullTitle = "Target server movie file",
                        DownloadStatus = DownloadStatus.Queued,
                        CreatedAt = DateTime.UtcNow,
                        PlexServerId = targetServerId,
                        PlexLibraryId = targetLibraryId,
                        PlexApiMediaId = 913012,
                        PlexApiPartId = 913013,
                        FileName = "target-server-movie.mkv",
                        FileLocationUrl = "/library/parts/913013/file.mkv",
                        HashId = "target-server-movie-hash",
                        Quality = VideoQuality.FullHD,
                        DirectoryMeta = CreateDownloadTaskDirectory(),
                        DataReceived = 0,
                        DataTotal = 10,
                        DownloadSpeed = 0,
                        DirectDownloadSnapshot = null,
                        DownloadClientType = PlexDownloadClientType.Direct,
                        FileTransferSpeed = 0,
                        FileDataTransferred = 0,
                        TimeRemaining = 0,
                        DestinationFolderPathId = null,
                        Parent = null,
                        ParentId = targetMovieDownloadId,
                        Logs =
                        [
                            new DownloadTaskMovieFileLog
                            {
                                Status = DownloadStatus.Queued,
                                LogLevel = NotificationLevel.Information,
                                Message = "Target server download queued",
                                CreatedAt = DateTime.UtcNow,
                                DownloadTaskFileId = targetMovieFileDownloadId,
                                DownloadTaskMovieId = targetMovieDownloadId,
                            },
                        ],
                    },
                ],
            }
        );
        dbContext.DownloadTaskMovie.Add(
            new DownloadTaskMovie
            {
                Id = otherMovieDownloadId,
                PlexApiRatingKey = 913021,
                Title = "Other server movie",
                FullTitle = "Other server movie",
                DownloadStatus = DownloadStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                PlexServerId = otherServerId,
                PlexLibraryId = otherLibraryId,
                Year = 2026,
                DataReceived = 0,
                FileDataTransferred = 0,
                DataTotal = 0,
                DownloadSpeed = 0,
                FileTransferSpeed = 0,
                Children =
                [
                    new DownloadTaskMovieFile
                    {
                        Id = otherMovieFileDownloadId,
                        PlexApiRatingKey = 913021,
                        Title = "Other server movie file",
                        FullTitle = "Other server movie file",
                        DownloadStatus = DownloadStatus.Queued,
                        CreatedAt = DateTime.UtcNow,
                        PlexServerId = otherServerId,
                        PlexLibraryId = otherLibraryId,
                        PlexApiMediaId = 913022,
                        PlexApiPartId = 913023,
                        FileName = "other-server-movie.mkv",
                        FileLocationUrl = "/library/parts/913023/file.mkv",
                        HashId = "other-server-movie-hash",
                        Quality = VideoQuality.FullHD,
                        DirectoryMeta = CreateDownloadTaskDirectory(),
                        DataReceived = 0,
                        DataTotal = 10,
                        DownloadSpeed = 0,
                        DirectDownloadSnapshot = null,
                        DownloadClientType = PlexDownloadClientType.Direct,
                        FileTransferSpeed = 0,
                        FileDataTransferred = 0,
                        TimeRemaining = 0,
                        DestinationFolderPathId = null,
                        Parent = null,
                        ParentId = otherMovieDownloadId,
                    },
                ],
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        targetConnectionIds.ShouldNotBeEmpty();
        targetLibraryIds.ShouldNotBeEmpty();
        (
            await dbContext.PlexAccountServers.AnyAsync(x => x.PlexServerId == targetServerId, CancellationToken)
        ).ShouldBeTrue();
        (
            await dbContext.DownloadTaskMovie.AnyAsync(x => x.Id == targetMovieDownloadId, CancellationToken)
        ).ShouldBeTrue();
        (
            await dbContext.DownloadTaskMovieFile.AnyAsync(x => x.Id == targetMovieFileDownloadId, CancellationToken)
        ).ShouldBeTrue();
        (
            await dbContext.DownloadTaskMovieFileLogs.AnyAsync(
                x => x.DownloadTaskMovieId == targetMovieDownloadId,
                CancellationToken
            )
        ).ShouldBeTrue();

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeletePlexServerEndpointRequest { PlexServerId = targetServerId }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        (
            await dbContext.PlexServers.IgnoreIsEnabledFilter().AnyAsync(x => x.Id == targetServerId, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexServerConnections.AnyAsync(x => x.PlexServerId == targetServerId, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexServerStatuses.AnyAsync(x => x.PlexServerId == targetServerId, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexAccountServers.AnyAsync(x => x.PlexServerId == targetServerId, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.PlexLibraries.AnyAsync(x => x.PlexServerId == targetServerId, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.DownloadTaskMovie.AnyAsync(x => x.Id == targetMovieDownloadId, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.DownloadTaskMovieFile.AnyAsync(x => x.Id == targetMovieFileDownloadId, CancellationToken)
        ).ShouldBeFalse();
        (
            await dbContext.DownloadTaskMovieFileLogs.AnyAsync(
                x => x.DownloadTaskMovieId == targetMovieDownloadId,
                CancellationToken
            )
        ).ShouldBeFalse();
        (
            await dbContext.PlexServers.IgnoreIsEnabledFilter().AnyAsync(x => x.Id == otherServerId, CancellationToken)
        ).ShouldBeTrue();
        (
            await dbContext.DownloadTaskMovie.AnyAsync(x => x.Id == otherMovieDownloadId, CancellationToken)
        ).ShouldBeTrue();
        (
            await dbContext.DownloadTaskMovieFile.AnyAsync(x => x.Id == otherMovieFileDownloadId, CancellationToken)
        ).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldInvalidateServerLibraries_WhenServerIsDeleted()
    {
        // Arrange
        await SetupDatabase(
            91304,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
                config.PlexTvShowLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        Mock.Mock<IMediaOverviewReadStore>()
            .Setup(x => x.RebuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeletePlexServerEndpointRequest { PlexServerId = server.Id }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        (
            await dbContext.PlexServers.IgnoreIsEnabledFilter().AnyAsync(x => x.Id == server.Id, CancellationToken)
        ).ShouldBeFalse();
        Mock.Mock<IMediaOverviewReadStore>().Verify(x => x.RebuildAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    private static DownloadTaskDirectory CreateDownloadTaskDirectory() =>
        new()
        {
            DownloadRootPath = "/downloads",
            DestinationRootPath = "/media/movies",
            MovieFolder = "Movie",
            TvShowFolder = string.Empty,
            SeasonFolder = string.Empty,
            KeepCompletedInDownloadFolder = false,
        };

    [Test]
    public async Task ShouldDeleteServer_WhenServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(91302, config => config.PlexServerCount = 1);

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);
        Mock.Mock<IMediaOverviewReadStore>()
            .Setup(x => x.RebuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeletePlexServerEndpointRequest { PlexServerId = server.Id }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        (
            await dbContext.PlexServers.IgnoreIsEnabledFilter().AnyAsync(x => x.Id == server.Id, CancellationToken)
        ).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenServerDoesNotExist()
    {
        // Arrange
        await SetupDatabase(91303, config => config.PlexServerCount = 0);

        // Act
        var endpointResult = await TestEndpointHandleAsync(new DeletePlexServerEndpointRequest { PlexServerId = 9999 });
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public void ShouldRejectInvalidPlexServerId_WhenValidatingRequest()
    {
        // Arrange
        var validator = new DeletePlexServerEndpointRequestValidator();

        // Act
        var result = validator.Validate(new DeletePlexServerEndpointRequest { PlexServerId = 0 });

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
