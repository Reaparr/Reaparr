using Reaparr.Domain.Validators;

namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskTvShowEpisodesCommandHandlerUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskTvShowEpisodesCommand>
{
    private readonly DownloadTaskTvShowValidator _validator = new();

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldQueueEverySelectedPartWithDistinctPaths_WhenEpisodeHasMultipleParts(bool generatedNames)
    {
        // Arrange
        var seed = await SetupDatabase(
            91335,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );
        var dbContext = IDbContext;
        var episodes = await dbContext
            .PlexTvShowEpisodes.AsTracking()
            .Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var episode = episodes[0];
        var singlePartEpisode = episodes[1];
        var firstPart = episode.MediaDataList.Single();
        dbContext
            .Entry(firstPart)
            .CurrentValues.SetValues(
                new
                {
                    PlexApiMediaId = 81001,
                    PlexApiPartId = 82001,
                    Size = 1000L,
                    OriginalFilename = "episode.cd1.mkv",
                    GeneratedFilename = generatedNames ? "Episode.1080p.mkv" : null,
                    Key = "/library/parts/82001/file.mkv",
                }
            );
        var secondPart = FakeData.GetPlexTvShowEpisodeMediaData(seed).Generate();
        dbContext
            .Entry(secondPart)
            .CurrentValues.SetValues(
                new
                {
                    PlexTvShowEpisodeId = episode.Id,
                    episode.PlexServerId,
                    episode.PlexLibraryId,
                    episode.PlexApiRatingKey,
                    PlexApiMediaId = 81001,
                    PlexApiPartId = 82002,
                    Size = 2000L,
                    OriginalFilename = "episode.cd2.mkv",
                    GeneratedFilename = generatedNames ? "Episode.1080p.mkv" : null,
                    Key = "/library/parts/82002/file.mkv",
                }
            );
        var otherQuality = FakeData.GetPlexTvShowEpisodeMediaData(seed).Generate();
        dbContext
            .Entry(otherQuality)
            .CurrentValues.SetValues(
                new
                {
                    PlexTvShowEpisodeId = episode.Id,
                    episode.PlexServerId,
                    episode.PlexLibraryId,
                    episode.PlexApiRatingKey,
                    PlexApiMediaId = 81002,
                    PlexApiPartId = 82003,
                }
            );
        dbContext.PlexTvShowEpisodeData.AddRange(secondPart, otherQuality);
        await dbContext.SaveChangesAsync(CancellationToken);
        var singlePart = singlePartEpisode.MediaDataList.Single();
        var request = new DownloadMediaDTO
        {
            Type = PlexMediaType.Episode,
            SelectionKind = null,
            MediaIds = [episode.Id, singlePartEpisode.Id],
            PlexServerId = episode.PlexServerId,
            PlexLibraryId = episode.PlexLibraryId,
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaDataType = PlexMediaType.Episode,
                    MediaId = episode.Id,
                    DataId = firstPart.Id,
                    VideoQuality = firstPart.VideoQuality,
                },
            ],
        };
        (await dbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken)).ShouldBeEmpty();
        episode.MediaDataList.Select(x => x.PlexApiPartId).Order().ShouldBe([82001, 82002, 82003]);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskTvShowEpisodesCommand([request])
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Episodes.ShouldBe(2);
        var tasks = await dbContext.DownloadTaskTvShowEpisode.Include(x => x.Children).ToListAsync(CancellationToken);
        var target = tasks.Single(x => x.PlexApiRatingKey == episode.PlexApiRatingKey);
        var files = target.Children.OrderBy(x => x.PlexApiPartId).ToArray();
        files
            .Select(x => (x.PlexApiMediaId, x.PlexApiPartId, x.FileLocationUrl, x.DataTotal))
            .ShouldBe([
                (81001, 82001, "/library/parts/82001/file.mkv", 1000L),
                (81001, 82002, "/library/parts/82002/file.mkv", 2000L),
            ]);
        files
            .Select(x => x.FileName)
            .ShouldBe(
                generatedNames
                    ? ["Episode.1080p.part82001.mkv", "Episode.1080p.part82002.mkv"]
                    : ["episode.cd1.mkv", "episode.cd2.mkv"]
            );
        files.Select(x => x.DownloadFilePath).Distinct().Count().ShouldBe(2);
        files.Select(x => x.DestinationFilePath).Distinct().Count().ShouldBe(2);
        files.Select(x => x.ParentId).ShouldBe([target.Id, target.Id]);
        target.Calculate().DataTotal.ShouldBe(3000L);
        files[0].DownloadStatus = DownloadStatus.Completed;
        target.Calculate();
        target.DownloadStatus.ShouldNotBe(DownloadStatus.Completed);
        files[1].DownloadStatus = DownloadStatus.Completed;
        target.Calculate();
        target.DownloadStatus.ShouldBe(DownloadStatus.Completed);
        var control = tasks.Single(x => x.PlexApiRatingKey == singlePartEpisode.PlexApiRatingKey).Children.Single();
        (control.PlexApiPartId, control.FileName, control.DataTotal).ShouldBe(
            (singlePart.PlexApiPartId, singlePart.GetFileName, singlePart.Size)
        );
        (await dbContext.DownloadTaskTvShowEpisodeFile.CountAsync(CancellationToken)).ShouldBe(3);
        var logs = await dbContext
            .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskTvShowEpisodeId == target.Id)
            .OrderBy(x => x.DownloadTaskFileId)
            .Select(x => x.DownloadTaskFileId)
            .ToListAsync(CancellationToken);
        logs.ShouldBe(files.Select(x => x.Id).Order().ToList());

        var originalFiles = await dbContext
            .DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.FileName,
                x.DownloadStatus,
                x.PlexApiMediaId,
                x.PlexApiPartId,
            })
            .ToListAsync(CancellationToken);
        var originalLogIds = await dbContext
            .DownloadTaskTvShowEpisodeFileLogs.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        // A second completed request must not create fresh file rows, paths, or queue logs.
        var repeated = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskTvShowEpisodesCommand([request])
        );
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Errors.Count.ShouldBe(0);
        repeated.Value.Episodes.ShouldBe(0);
        (
            await dbContext
                .DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ParentId,
                    x.FileName,
                    x.DownloadStatus,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(originalFiles);
        (
            await dbContext
                .DownloadTaskTvShowEpisodeFileLogs.OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync(CancellationToken)
        ).ShouldBe(originalLogIds);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldAddOnlyMissingOwnedParts_WhenRequestsOverlapAndAnotherQualityIsSelected(bool differentOwner)
    {
        // Arrange
        var seed = await SetupDatabase(
            91336,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
                config.SonarrIntegrationCount = 2;
            }
        );
        var dbContext = IDbContext;
        var owners = await dbContext.SonarrIntegrations.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        owners.Count.ShouldBe(2);
        var firstOwner = new IntegrationIdentity(IntegrationType.Sonarr, owners[0].Id);
        var targetOwner = differentOwner ? new IntegrationIdentity(IntegrationType.Sonarr, owners[1].Id) : firstOwner;
        var episode = await dbContext
            .PlexTvShowEpisodes.AsTracking()
            .Include(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var firstPart = episode.MediaDataList.Single();
        dbContext
            .Entry(firstPart)
            .CurrentValues.SetValues(
                new
                {
                    PlexApiMediaId = 91001,
                    PlexApiPartId = 92001,
                    OriginalFilename = "episode.first.mkv",
                }
            );
        await dbContext.SaveChangesAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.Episode,
            SelectionKind = null,
            MediaIds = [episode.Id],
            PlexServerId = episode.PlexServerId,
            PlexLibraryId = episode.PlexLibraryId,
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaDataType = PlexMediaType.Episode,
                    MediaId = episode.Id,
                    DataId = firstPart.Id,
                    VideoQuality = firstPart.VideoQuality,
                },
            ],
        };
        var firstResult = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskTvShowEpisodesCommand(
                new CreateDownloadTasksRequest([selection], integration: firstOwner)
            )
        );
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Errors.Count.ShouldBe(0);
        firstResult.Value.Episodes.ShouldBe(1);
        var original = await dbContext.DownloadTaskTvShowEpisodeFile.SingleAsync(CancellationToken);
        original.SonarrIntegrationId.ShouldBe(firstOwner.Id);
        (original.PlexApiMediaId, original.PlexApiPartId).ShouldBe((91001, 92001));
        await dbContext.SetDownloadStatus(original.ToKey(), DownloadStatus.Paused);
        var originalRootId = await dbContext.DownloadTaskTvShow.Select(x => x.Id).SingleAsync(CancellationToken);
        var secondPart = FakeData.GetPlexTvShowEpisodeMediaData(seed).Generate();
        dbContext
            .Entry(secondPart)
            .CurrentValues.SetValues(
                new
                {
                    PlexTvShowEpisodeId = episode.Id,
                    episode.PlexServerId,
                    episode.PlexLibraryId,
                    episode.PlexApiRatingKey,
                    PlexApiMediaId = 91001,
                    PlexApiPartId = 92002,
                    OriginalFilename = "episode.second.mkv",
                }
            );
        var otherQuality = FakeData.GetPlexTvShowEpisodeMediaData(seed).Generate();
        dbContext
            .Entry(otherQuality)
            .CurrentValues.SetValues(
                new
                {
                    PlexTvShowEpisodeId = episode.Id,
                    episode.PlexServerId,
                    episode.PlexLibraryId,
                    episode.PlexApiRatingKey,
                    PlexApiMediaId = 91002,
                    PlexApiPartId = 92001,
                    OriginalFilename = "episode.other-quality.mkv",
                }
            );
        dbContext.PlexTvShowEpisodeData.AddRange(secondPart, otherQuality);
        await dbContext.SaveChangesAsync(CancellationToken);
        var overlappingRequest = new GenerateDownloadTaskTvShowEpisodesCommand(
            new CreateDownloadTasksRequest([selection], integration: targetOwner)
        );

        // Act
        var overlap = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(overlappingRequest);
        var repeated = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(overlappingRequest);

        // Assert — the overlap adds the missing part, not a duplicate of the retained first file.
        overlap.IsSuccess.ShouldBeTrue();
        overlap.Errors.Count.ShouldBe(0);
        overlap.Value.Episodes.ShouldBe(1);
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Errors.Count.ShouldBe(0);
        repeated.Value.Episodes.ShouldBe(0);
        var targetFiles = await dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.SonarrIntegrationId == targetOwner.Id)
            .OrderBy(x => x.PlexApiPartId)
            .ToListAsync(CancellationToken);
        targetFiles.Select(x => (x.PlexApiMediaId, x.PlexApiPartId)).ShouldBe([(91001, 92001), (91001, 92002)]);
        var retained = await dbContext.DownloadTaskTvShowEpisodeFile.SingleAsync(
            x => x.Id == original.Id,
            CancellationToken
        );
        (
            retained.ParentId,
            retained.FileName,
            retained.DownloadFilePath,
            retained.DestinationFilePath,
            retained.DownloadStatus
        ).ShouldBe(
            (
                original.ParentId,
                original.FileName,
                original.DownloadFilePath,
                original.DestinationFilePath,
                DownloadStatus.Paused
            )
        );
        if (differentOwner)
            targetFiles.Select(x => x.ParentId).ShouldNotContain(original.ParentId);

        // Select the other quality with the same PartId: media identity must also be compared.
        var otherSelection = selection with
        {
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaDataType = PlexMediaType.Episode,
                    MediaId = episode.Id,
                    DataId = otherQuality.Id,
                    VideoQuality = otherQuality.VideoQuality,
                },
            ],
        };
        var otherResult = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskTvShowEpisodesCommand(
                new CreateDownloadTasksRequest([otherSelection], integration: targetOwner)
            )
        );
        otherResult.IsSuccess.ShouldBeTrue();
        otherResult.Errors.Count.ShouldBe(0);
        otherResult.Value.Episodes.ShouldBe(1);
        var allFiles = await dbContext.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        allFiles
            .Where(x => x.SonarrIntegrationId == targetOwner.Id)
            .OrderBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .Select(x => (x.PlexApiMediaId, x.PlexApiPartId))
            .ShouldBe([(91001, 92001), (91001, 92002), (91002, 92001)]);
        allFiles
            .Where(x => x.SonarrIntegrationId != targetOwner.Id)
            .Select(x => x.Id)
            .ShouldBe(differentOwner ? [original.Id] : []);
        var roots = await dbContext
            .DownloadTaskTvShow.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.SonarrIntegrationId })
            .ToListAsync(CancellationToken);
        roots.Single(x => x.Id == originalRootId).SonarrIntegrationId.ShouldBe(firstOwner.Id);
        roots
            .Select(x => x.SonarrIntegrationId)
            .Order()
            .ShouldBe(differentOwner ? new Guid?[] { firstOwner.Id, targetOwner.Id }.Order() : [firstOwner.Id]);
        (
            await dbContext
                .DownloadTaskTvShowEpisodeFileLogs.OrderBy(x => x.DownloadTaskFileId)
                .Select(x => x.DownloadTaskFileId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(allFiles.Select(x => x.Id).ToList());
    }

    [Test]
    public void GenerateDownloadTaskTvShowEpisodesCommandValidator_ShouldRejectNullRequest()
    {
        // Arrange
        var command = new GenerateDownloadTaskTvShowEpisodesCommand((CreateDownloadTasksRequest)null!);
        var validator = new GenerateDownloadTaskTvShowEpisodesCommandValidator();

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GenerateDownloadTaskTvShowEpisodesCommand.Request));
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenPlexTvShowsAreEmpty()
    {
        // Arrange
        await SetupDatabase(84901);
        var downloadMediaDtos = new List<DownloadMediaDTO>();

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldGenerateValidTvShowDownloadTaskWithEpisodeDownloadTask_WhenNoDownloadTasksExist()
    {
        // Arrange
        await SetupDatabase(
            47893,
            config =>
            {
                config.TvShowCount = 5;
                config.TvShowSeasonCount = 5;
                config.TvShowEpisodeCount = 5;
            }
        );

        var plexTvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);

        var plexSeasons = plexTvShows.SelectMany(x => x.Seasons).ToList();
        var plexEpisodes = plexSeasons.SelectMany(x => x.Episodes).ToList();
        var expectedSeasonCount = plexSeasons.Count;
        var expectedEpisodeCount = plexEpisodes.Count;
        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var request = new CreateDownloadTasksRequest(downloadMediaDtos, 99);
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(request);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskTvShows = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        downloadTaskTvShows.Count.ShouldBe(5);

        var downloadTaskSeasons = downloadTaskTvShows.SelectMany(x => x.Children).ToList();
        downloadTaskSeasons.Count.ShouldBe(expectedSeasonCount);

        var downloadTaskEpisodes = downloadTaskSeasons.SelectMany(x => x.Children).ToList();
        downloadTaskEpisodes.Count.ShouldBe(expectedEpisodeCount);

        var downloadTaskEpisodeFiles = downloadTaskEpisodes.SelectMany(x => x.Children).ToList();
        downloadTaskEpisodeFiles.Count.ShouldBe(expectedEpisodeCount);

        foreach (var downloadTaskTvShow in downloadTaskTvShows)
        {
            downloadTaskTvShow.Calculate();
            var validationResult = await _validator.ValidateAsync(downloadTaskTvShow, CancellationToken);

            // Ignore DownloadDirectory and DestinationDirectory errors as these are set in the DownloadJob
            var validErrors = validationResult.Errors.FindAll(x =>
                !x.PropertyName.Contains(nameof(DownloadTaskFileBase.DownloadDirectory))
                && !x.PropertyName.Contains(nameof(DownloadTaskFileBase.DestinationDirectory))
            );
            validErrors.ShouldBeEmpty();
        }

        foreach (var episodeFile in downloadTaskEpisodeFiles)
        {
            episodeFile.DestinationFolderPathId.ShouldBe(99);
        }
    }

    [Test]
    public async Task ShouldGenerateValidEpisodeDownloadTask_WhenTvShowParentDownloadTaskAlreadyExist()
    {
        // Arrange
        await SetupDatabase(
            52051,
            config =>
            {
                config.TvShowCount = 5;
                config.TvShowSeasonCount = 5;
                config.TvShowEpisodeCount = 5;
            }
        );
        var dbContext = IDbContext;
        var plexTvShows = await dbContext.PlexTvShows.IncludeAll().ToListAsync(CancellationToken);
        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        var expectedTvShowCount = plexTvShows.Count;
        var expectedSeasonCount = plexTvShows.SelectMany(x => x.Seasons).Count();
        var expectedEpisodeCount = plexEpisodes.Count;

        // Create a download task for the TV-show
        var createdTvShowDownloadTask = plexTvShows.First().MapToDownloadTask(null);
        dbContext.DownloadTaskTvShow.Add(createdTvShowDownloadTask);
        await dbContext.SaveChangesAsync(CancellationToken);

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskTvShows = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        downloadTaskTvShows.Count.ShouldBe(expectedTvShowCount);
        var downloadTaskSeasons = downloadTaskTvShows.SelectMany(x => x.Children).ToList();
        downloadTaskSeasons.Count.ShouldBe(expectedSeasonCount);
        var downloadTaskEpisodes = downloadTaskSeasons.SelectMany(x => x.Children).ToList();
        downloadTaskEpisodes.Count.ShouldBe(expectedEpisodeCount);
        var downloadTaskEpisodeFiles = downloadTaskEpisodes.SelectMany(x => x.Children).ToList();
        downloadTaskEpisodeFiles.Count.ShouldBe(expectedEpisodeCount);

        downloadTaskTvShows.FirstOrDefault(x => x.Id == createdTvShowDownloadTask.Id).ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldIgnoreNonExistentEpisodeIds_WhenProcessingValidEpisodes()
    {
        // Arrange
        await SetupDatabase(
            34567,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var plexTvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);

        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        var validEpisodeIds = plexEpisodes.Select(x => x.Id).ToList();
        var invalidEpisodeIds = new List<int> { 99999, 88888, 77777 }; // Non-existent IDs

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = validEpisodeIds.Concat(invalidEpisodeIds).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskTvShows = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        downloadTaskTvShows.Count.ShouldBe(1);

        var downloadTaskEpisodes = downloadTaskTvShows.SelectMany(x => x.Children).SelectMany(x => x.Children).ToList();
        downloadTaskEpisodes.Count.ShouldBe(3); // Only valid episodes should be processed
    }

    [Test]
    public async Task ShouldHandleDuplicateEpisodeIds_WithoutCreatingDuplicateDownloadTasks()
    {
        // Arrange
        await SetupDatabase(
            45678,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var plexTvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);

        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        var episodeIds = plexEpisodes.Select(x => x.Id).ToList();
        var duplicatedEpisodeIds = episodeIds.Concat(episodeIds).ToList(); // Duplicate all IDs

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = duplicatedEpisodeIds,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskTvShows = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        downloadTaskTvShows.Count.ShouldBe(1);

        var downloadTaskEpisodes = downloadTaskTvShows.SelectMany(x => x.Children).SelectMany(x => x.Children).ToList();
        downloadTaskEpisodes.Count.ShouldBe(2); // Should not create duplicates
    }

    [Test]
    public async Task ShouldCreateNewDownloadTasks_WhenSomeEpisodesAlreadyHaveDownloadTasks()
    {
        // Arrange
        await SetupDatabase(
            56789,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 3;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexTvShow = await dbContext.PlexTvShows.IncludeAll().FirstOrDefaultAsync(CancellationToken);
        plexTvShow.ShouldNotBeNull();
        var plexEpisodes = plexTvShow.Seasons.SelectMany(x => x.Episodes).ToList();
        plexEpisodes.Count.ShouldBe(6);

        // Create a complete download task hierarchy for the first episode only
        var tvShowDownloadTask = await dbContext
            .DownloadTaskTvShow.AsTracking()
            .IncludeAll()
            .FirstOrDefaultAsync(CancellationToken);
        tvShowDownloadTask.ShouldNotBeNull();

        // Copy tv-show key over to the download task
        tvShowDownloadTask.UpdateInitProperty(nameof(tvShowDownloadTask.PlexApiRatingKey), plexTvShow.PlexApiRatingKey);

        // Copy season key over to the download task
        var season = plexTvShow.Seasons.FirstOrDefault();
        var seasonDownloadTask = tvShowDownloadTask.Children.FirstOrDefault();
        season.ShouldNotBeNull();
        seasonDownloadTask.ShouldNotBeNull();

        seasonDownloadTask.UpdateInitProperty(nameof(seasonDownloadTask.PlexApiRatingKey), season.PlexApiRatingKey);

        // Copy episode key over to the download task
        var episode = season.Episodes.FirstOrDefault();
        var episodeDownloadTask = seasonDownloadTask.Children.FirstOrDefault();
        episode.ShouldNotBeNull();
        episodeDownloadTask.ShouldNotBeNull();
        episodeDownloadTask.UpdateInitProperty(nameof(episodeDownloadTask.PlexApiRatingKey), episode.PlexApiRatingKey);

        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());

        dbContext = IDbContext;
        dbContext.DownloadTaskTvShow.Count().ShouldBe(1);
        dbContext.DownloadTaskTvShowSeason.Count().ShouldBe(2);
        dbContext.DownloadTaskTvShowEpisode.Count().ShouldBe(6);
        dbContext.DownloadTaskTvShowEpisodeFile.Count().ShouldBe(7);

        var downloadTaskEpisodes = await dbContext.DownloadTaskTvShowEpisode.ToListAsync(CancellationToken);

        // Verify the existing episode download task still exists
        downloadTaskEpisodes.ShouldContain(x => x.PlexApiRatingKey == episodeDownloadTask.PlexApiRatingKey);
    }

    [Test]
    public async Task ShouldGenerateDownloadTasksForMultipleTvShows_WhenEpisodesFromDifferentShowsRequested()
    {
        // Arrange
        await SetupDatabase(
            67890,
            config =>
            {
                config.TvShowCount = 3;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );

        var plexTvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);

        plexTvShows.Count.ShouldBe(3);

        // Select episodes from all 3 TV shows
        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        plexEpisodes.Count.ShouldBe(12); // 3 shows × 2 seasons × 2 episodes each

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskTvShows = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        downloadTaskTvShows.Count.ShouldBe(3); // One for each TV show

        var downloadTaskSeasons = downloadTaskTvShows.SelectMany(x => x.Children).ToList();
        downloadTaskSeasons.Count.ShouldBe(6); // 3 shows × 2 seasons each

        var downloadTaskEpisodes = downloadTaskSeasons.SelectMany(x => x.Children).ToList();
        downloadTaskEpisodes.Count.ShouldBe(12); // 3 shows × 2 seasons × 2 episodes each

        var downloadTaskEpisodeFiles = downloadTaskEpisodes.SelectMany(x => x.Children).ToList();
        downloadTaskEpisodeFiles.Count.ShouldBe(12); // One file per episode

        // Verify each TV show has correct structure
        foreach (var tvShow in downloadTaskTvShows)
        {
            tvShow.Children.Count.ShouldBe(2); // 2 seasons per show
            foreach (var season in tvShow.Children)
            {
                season.Children.Count.ShouldBe(2); // 2 episodes per season
            }
        }
    }

    [Test]
    public async Task ShouldIgnoreNonEpisodeMediaTypes_WhenMixedMediaTypesProvided()
    {
        // Arrange
        await SetupDatabase(
            78901,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
                config.MovieCount = 2;
            }
        );

        var plexTvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);
        var plexMovies = await IDbContext.PlexMovies.ToListAsync(CancellationToken);

        plexTvShows.Count.ShouldBe(1);
        plexMovies.Count.ShouldBe(2);

        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        plexEpisodes.Count.ShouldBe(2);

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
            new()
            {
                Type = PlexMediaType.Movie, // This should be ignored by the episode handler
                SelectionKind = null,
                MediaIds = plexMovies.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskTvShows = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);
        downloadTaskTvShows.Count.ShouldBe(1); // Only TV show download tasks should be created

        var downloadTaskEpisodes = downloadTaskTvShows.SelectMany(x => x.Children).SelectMany(x => x.Children).ToList();
        downloadTaskEpisodes.Count.ShouldBe(2); // Only episode download tasks should be created

        // Verify no movie download tasks were created
        var movieDownloadTasks = await IDbContext.DownloadTaskMovie.ToListAsync(CancellationToken);
        movieDownloadTasks.Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldSetCorrectDestinationFolderPathId_WhenCustomDestinationFolderProvided()
    {
        // Arrange
        await SetupDatabase(
            89012,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var plexTvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);

        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        plexEpisodes.Count.ShouldBe(2);

        var customDestinationFolderId = 12345;

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var request = new CreateDownloadTasksRequest(downloadMediaDtos, customDestinationFolderId);
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(request);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskEpisodeFiles = await IDbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken);
        downloadTaskEpisodeFiles.Count.ShouldBe(2);

        foreach (var episodeFile in downloadTaskEpisodeFiles)
        {
            episodeFile.DestinationFolderPathId.ShouldBe(customDestinationFolderId);
        }
    }

    [Test]
    public async Task ShouldSetCustomDestinationFolderPath_WhenRequestContainsCustomDestinationFolderPath()
    {
        // Arrange
        await SetupDatabase(
            89013,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;

        var plexTvShows = await dbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);

        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        plexEpisodes.Count.ShouldBe(2);

        const string customPath = "/custom/destination/path";

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var request = new CreateDownloadTasksRequest(downloadMediaDtos, null, customPath);
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(request);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskEpisodeFiles = await dbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken);
        downloadTaskEpisodeFiles.Count.ShouldBe(2);

        foreach (var episodeFile in downloadTaskEpisodeFiles)
        {
            episodeFile.DirectoryMeta.DestinationRootPath.ShouldBe(customPath);
        }
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenEmptyMediaIdsList()
    {
        // Arrange
        await SetupDatabase(90123);

        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = new List<int>(), // Empty list
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldCalculateDownloadTaskPropertiesCorrectly_WhenDownloadTasksGenerated()
    {
        // Arrange
        await SetupDatabase(
            91234,
            config =>
            {
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var plexTvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .ToListAsync(CancellationToken);

        var plexEpisodes = plexTvShows.SelectMany(x => x.Seasons).SelectMany(x => x.Episodes).ToList();
        var downloadMediaDtos = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                MediaIds = plexEpisodes.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        // Act
        var command = new GenerateDownloadTaskTvShowEpisodesCommand(downloadMediaDtos);
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        var downloadTaskTvShows = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);

        foreach (var tvShow in downloadTaskTvShows)
        {
            tvShow.Calculate();

            // Verify calculated properties
            tvShow.DataReceived.ShouldBeGreaterThanOrEqualTo(0);
            tvShow.DataTotal.ShouldBeGreaterThan(0);
            tvShow.FileTransferSpeed.ShouldBeGreaterThanOrEqualTo(0);
            tvShow.Percentage.ShouldBeGreaterThanOrEqualTo(0);
            tvShow.Percentage.ShouldBeLessThanOrEqualTo(100);

            // Verify parent-child relationships
            foreach (var season in tvShow.Children)
            {
                season.ParentId.ShouldBe(tvShow.Id);
                foreach (var episode in season.Children)
                {
                    episode.ParentId.ShouldBe(season.Id);
                    foreach (var episodeFile in episode.Children)
                    {
                        episodeFile.ParentId.ShouldBe(episode.Id);
                    }
                }
            }
        }
    }
}
