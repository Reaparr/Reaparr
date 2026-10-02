namespace Reaparr.Application.UnitTests;

public class SetLibraryEnabledEndpointUnitTests
    : BaseEndpointUnitTest<SetLibraryEnabledEndpoint, SetLibraryEnabledRequest, ResultDTO<PlexLibraryDTO>>
{
    [Test]
    public async Task ShouldPurgeOnlySelectedPhotoLibrary_AndResetAllFamilyMetrics()
    {
        // Arrange
        await SetupDatabase(
            4601,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );
        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var targetLibrary = FakeData.GetPlexLibrary(new Seed(4601), PlexMediaType.Photos).Generate();
        targetLibrary.PlexServerId = serverId;
        var controlLibrary = FakeData.GetPlexLibrary(new Seed(4602), PlexMediaType.Photos).Generate();
        controlLibrary.PlexServerId = serverId;
        dbContext.PlexLibraries.AddRange(targetLibrary, controlLibrary);
        await dbContext.SaveChangesAsync(CancellationToken);

        dbContext.PlexPhotoAlbums.AddRange(
            CreatePhotoAlbum(targetLibrary.Id, serverId, 101, "Target album"),
            CreatePhotoAlbum(controlLibrary.Id, serverId, 102, "Control album")
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = serverId,
                PlexLibraryId = targetLibrary.Id,
                Priority = 3,
                Status = LibrarySyncJobStatus.Cancelled,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                CompletedAt = DateTime.UtcNow.AddMinutes(-1),
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexLibraries.Where(x => x.Id == targetLibrary.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.MediaSize, 999L)
                        .SetProperty(y => y.SyncedContentChangedAt, 123L)
                        .SetProperty(y => y.ArtistCount, 1)
                        .SetProperty(y => y.AlbumCount, 2)
                        .SetProperty(y => y.TrackCount, 3)
                        .SetProperty(y => y.TrackMediaVersionCount, 4)
                        .SetProperty(y => y.TrackFilePartCount, 5)
                        .SetProperty(y => y.PhotoAlbumCount, 6)
                        .SetProperty(y => y.PhotoCount, 7)
                        .SetProperty(y => y.PhotoClipCount, 8)
                        .SetProperty(y => y.PhotoMediaVersionCount, 9)
                        .SetProperty(y => y.PhotoFilePartCount, 10)
                        .SetProperty(y => y.OtherVideoCount, 11)
                        .SetProperty(y => y.OtherVideoMediaVersionCount, 12)
                        .SetProperty(y => y.OtherVideoFilePartCount, 13),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CancelLibrarySyncJobCommand>(command => command.PlexLibraryId == targetLibrary.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetLibraryEnabledRequest { PlexLibraryId = targetLibrary.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();
        endpointResult.Response.Errors.Count.ShouldBe(0);
        endpointResult.Response.Value.ShouldNotBeNull().IsEnabled.ShouldBeFalse();
        var remainingAlbums = await dbContext
            .PlexPhotoAlbums.AsNoTracking()
            .Select(x => new { x.PlexLibraryId, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        remainingAlbums.ShouldBe([new { PlexLibraryId = controlLibrary.Id, PlexApiRatingKey = 102 }]);
        var updatedLibrary = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .AsNoTracking()
            .SingleAsync(x => x.Id == targetLibrary.Id, CancellationToken);
        updatedLibrary.IsEnabled.ShouldBeFalse();
        updatedLibrary.SyncedAt.ShouldBeNull();
        updatedLibrary.SyncedContentChangedAt.ShouldBeNull();
        updatedLibrary.Outdated.ShouldBeFalse();
        new[]
        {
            updatedLibrary.ArtistCount,
            updatedLibrary.AlbumCount,
            updatedLibrary.TrackCount,
            updatedLibrary.TrackMediaVersionCount,
            updatedLibrary.TrackFilePartCount,
            updatedLibrary.PhotoAlbumCount,
            updatedLibrary.PhotoCount,
            updatedLibrary.PhotoClipCount,
            updatedLibrary.PhotoMediaVersionCount,
            updatedLibrary.PhotoFilePartCount,
            updatedLibrary.OtherVideoCount,
            updatedLibrary.OtherVideoMediaVersionCount,
            updatedLibrary.OtherVideoFilePartCount,
        }.ShouldAllBe(x => x == 0);
        updatedLibrary.MediaSize.ShouldBe(0);
        var retainedQueueHistory = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .Where(x => x.PlexLibraryId == targetLibrary.Id)
            .Select(x => new { x.PlexLibraryId, x.Status, x.CompletedAt })
            .SingleAsync(CancellationToken);
        retainedQueueHistory.PlexLibraryId.ShouldBe(targetLibrary.Id);
        retainedQueueHistory.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        retainedQueueHistory.CompletedAt.ShouldNotBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldPurgeMusicAndOtherVideoRoots_WithoutPurgingOtherLibraries()
    {
        // Arrange
        await SetupDatabase(4604, config => config.PlexServerCount = 1);
        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var targetMusic = FakeData.GetPlexLibrary(new Seed(4604), PlexMediaType.Music).Generate();
        var controlMusic = FakeData.GetPlexLibrary(new Seed(4605), PlexMediaType.Music).Generate();
        var targetOtherVideos = FakeData.GetPlexLibrary(new Seed(4606), PlexMediaType.OtherVideos).Generate();
        var controlOtherVideos = FakeData.GetPlexLibrary(new Seed(4607), PlexMediaType.OtherVideos).Generate();
        var libraries = new[] { targetMusic, controlMusic, targetOtherVideos, controlOtherVideos };
        foreach (var library in libraries)
            library.PlexServerId = serverId;

        dbContext.PlexLibraries.AddRange(libraries);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexArtists.AddRange(
            CreateArtist(targetMusic.Id, serverId, 201, "Target artist"),
            CreateArtist(controlMusic.Id, serverId, 202, "Control artist")
        );
        dbContext.PlexOtherVideos.AddRange(
            CreateOtherVideo(targetOtherVideos.Id, serverId, 301, "Target video"),
            CreateOtherVideo(controlOtherVideos.Id, serverId, 302, "Control video")
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var targetLibraryIds = new[] { targetMusic.Id, targetOtherVideos.Id };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CancelLibrarySyncJobCommand>(command => targetLibraryIds.Contains(command.PlexLibraryId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Act
        foreach (var libraryId in targetLibraryIds)
        {
            var endpointResult = await TestEndpointHandleAsync(
                new SetLibraryEnabledRequest { PlexLibraryId = libraryId, IsEnabled = false }
            );
            endpointResult.IsValid.ShouldBeTrue();
            endpointResult.Response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
            endpointResult.Response.Errors.Count.ShouldBe(0);
        }

        // Assert
        var remainingArtists = await dbContext
            .PlexArtists.AsNoTracking()
            .Select(x => new { x.PlexLibraryId, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        remainingArtists.ShouldBe([new { PlexLibraryId = controlMusic.Id, PlexApiRatingKey = 202 }]);
        var remainingVideos = await dbContext
            .PlexOtherVideos.AsNoTracking()
            .Select(x => new { x.PlexLibraryId, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        remainingVideos.ShouldBe([new { PlexLibraryId = controlOtherVideos.Id, PlexApiRatingKey = 302 }]);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldQueueFreshSyncWithoutOverviewRebuild_WhenReEnablingMusicLibrary()
    {
        // Arrange
        await SetupDatabase(
            4603,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );
        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var library = FakeData.GetPlexLibrary(new Seed(4603), PlexMediaType.Music).Generate();
        library.PlexServerId = serverId;
        library.IsEnabled = false;
        dbContext.PlexLibraries.Add(library);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueLibrarySyncJobCommand>(command =>
                        command.PlexLibraryIds.SequenceEqual(new[] { library.Id })
                        && !command.ForceLibrarySync
                        && !command.ForceMediaRefresh
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetLibraryEnabledRequest { PlexLibraryId = library.Id, IsEnabled = true }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();
        endpointResult.Response.Errors.Count.ShouldBe(0);
        endpointResult.Response.Value.ShouldNotBeNull().IsEnabled.ShouldBeTrue();
        var isEnabled = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.Id == library.Id)
            .Select(x => x.IsEnabled)
            .SingleAsync(CancellationToken);
        isEnabled.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>().Verify();
    }

    private static PlexMusicArtist CreateArtist(int libraryId, int serverId, int ratingKey, string title) =>
        new()
        {
            PlexApiRatingKey = ratingKey,
            Title = title,
            Year = 2026,
            SortIndex = ratingKey,
            SearchTitle = title.ToSearchTitle(),
            Duration = 1,
            MediaSize = 1,
            PlexApiMetaDataKey = ratingKey,
            Studio = string.Empty,
            Summary = string.Empty,
            ContentRating = string.Empty,
            Rating = 0,
            ChildCount = 0,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OriginallyAvailableAt = null,
            HasThumb = false,
            HasArt = false,
            HasTheme = false,
            FullTitle = title,
            Guid = string.Empty,
            Guid_IMDB = null,
            Guid_TMDB = null,
            Guid_TVDB = null,
            PlexLibraryId = libraryId,
            PlexServerId = serverId,
        };

    private static PlexOtherVideo CreateOtherVideo(int libraryId, int serverId, int ratingKey, string title) =>
        new()
        {
            PlexApiRatingKey = ratingKey,
            Title = title,
            Year = 2026,
            SortIndex = ratingKey,
            SearchTitle = title.ToSearchTitle(),
            Duration = 1,
            MediaSize = 1,
            PlexApiMetaDataKey = ratingKey,
            Studio = string.Empty,
            Summary = string.Empty,
            ContentRating = string.Empty,
            Rating = 0,
            ChildCount = 0,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OriginallyAvailableAt = null,
            HasThumb = false,
            HasArt = false,
            HasTheme = false,
            FullTitle = title,
            Guid = string.Empty,
            Guid_IMDB = null,
            Guid_TMDB = null,
            Guid_TVDB = null,
            PlexLibraryId = libraryId,
            PlexServerId = serverId,
        };

    private static PlexPhotoAlbum CreatePhotoAlbum(int libraryId, int serverId, int ratingKey, string title) =>
        new()
        {
            PlexApiRatingKey = ratingKey,
            Title = title,
            SearchTitle = title.ToSearchTitle(),
            SortIndex = ratingKey,
            MediaSize = 1,
            AddedAt = DateTime.UtcNow,
            PlexLibraryId = libraryId,
            PlexServerId = serverId,
        };
}
