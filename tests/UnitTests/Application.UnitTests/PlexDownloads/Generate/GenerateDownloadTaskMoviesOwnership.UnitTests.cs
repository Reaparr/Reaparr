namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskMoviesOwnershipUnitTests : BaseCommandUnitTest<GenerateDownloadTaskMoviesCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSuppressOnlyExactOwnerServerRatingKeyDuplicate_WhenCallerRequestsUnownedOrRadarrMovies(
        bool radarrCaller
    )
    {
        // Arrange
        var scenario = await SeedScenarioAsync(radarrCaller);
        scenario.SelectedRatingKeys.Length.ShouldBe(5);
        scenario.SelectedRatingKeys.Distinct().Count().ShouldBe(5);
        scenario.SelectedMovieIds.Length.ShouldBe(5);
        scenario.SelectedMovieIds.Distinct().Count().ShouldBe(5);
        scenario.ExistingTaskIds.Length.ShouldBe(4);
        scenario.ExistingTaskIds.ShouldAllBe(x => x != Guid.Empty);
        scenario.ExpectedCreatedParents.Length.ShouldBe(radarrCaller ? 4 : 3);
        using var dbContext = IDbContext;
        var beforeParents = await ReadParentRows(dbContext, scenario.ExistingTaskIds);
        var beforeFiles = await ReadFileRows(dbContext, beforeParents.Select(x => x.Id).ToArray());

        beforeParents.Length.ShouldBe(4);
        beforeFiles.Length.ShouldBe(4);
        beforeParents.Select(x => x.Id).ShouldBe(scenario.ExistingTaskIds.OrderBy(x => x));
        var crossLibraryDuplicate = beforeParents.Single(x => x.PlexApiRatingKey == scenario.SelectedRatingKeys[0]);
        crossLibraryDuplicate.PlexServerId.ShouldBe(scenario.SourceServerId);
        crossLibraryDuplicate.RadarrIntegrationId.ShouldBe(scenario.RequestIntegration?.Id);
        crossLibraryDuplicate.PlexLibraryId.ShouldNotBe(scenario.SourceLibraryId);
        beforeParents
            .Single(x => x.PlexApiRatingKey == scenario.SelectedRatingKeys[1])
            .RadarrIntegrationId.ShouldNotBe(scenario.RequestIntegration?.Id);
        beforeParents
            .Single(x => x.PlexApiRatingKey == scenario.SelectedRatingKeys[1])
            .RadarrIntegrationId.ShouldNotBeNull();
        beforeParents
            .Single(x => x.PlexApiRatingKey == scenario.SelectedRatingKeys[2])
            .RadarrIntegrationId.ShouldBeNull();
        beforeParents
            .Single(x => x.PlexApiRatingKey == scenario.SelectedRatingKeys[3])
            .PlexServerId.ShouldNotBe(scenario.SourceServerId);

        var command = CreateCommand(scenario);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        var expectedCreatedCount = scenario.ExpectedCreatedParents.Length;
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { Movies = expectedCreatedCount });
        result.Value.Movies.ShouldBe(expectedCreatedCount);
        result.Value.Total.ShouldBe(expectedCreatedCount);

        using var afterDbContext = IDbContext;
        var afterParents = await ReadParentRows(afterDbContext);
        var afterFiles = await ReadFileRows(afterDbContext);
        afterParents.Length.ShouldBe(beforeParents.Length + expectedCreatedCount);
        afterFiles.Length.ShouldBe(beforeFiles.Length + expectedCreatedCount);

        afterParents.Where(x => scenario.ExistingTaskIds.Contains(x.Id)).ToArray().ShouldBe(beforeParents);
        afterFiles.Where(x => beforeFiles.Select(y => y.Id).Contains(x.Id)).ToArray().ShouldBe(beforeFiles);

        var createdParents = afterParents
            .Where(x => !scenario.ExistingTaskIds.Contains(x.Id))
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArray();
        createdParents.Select(ToParentIdentity).ShouldBe(scenario.ExpectedCreatedParents);
        createdParents.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Queued);

        var existingFileIds = beforeFiles.Select(x => x.Id).ToArray();
        var createdFiles = afterFiles
            .Where(x => !existingFileIds.Contains(x.Id))
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArray();
        createdFiles.Length.ShouldBe(expectedCreatedCount);
        createdFiles.Select(ToFileIdentity).ShouldBe(createdParents.Select(ToParentFileIdentity));
        createdFiles.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Queued);
    }

    [Test]
    public async Task ShouldKeepDistinctSelectedMoviesWithTheSameKey_WhenNeitherHasAPersistedDownload()
    {
        // Arrange
        await SetupDatabase(
            56170,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        using var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.IncludeAll().OrderBy(x => x.Id).ToArrayAsync(CancellationToken);
        var sharedKey = movies[0].PlexApiRatingKey;
        await dbContext.PlexMovies.ExecuteUpdateAsync(
            update => update.SetProperty(x => x.PlexApiRatingKey, sharedKey),
            CancellationToken
        );
        await dbContext.PlexMovieData.ExecuteUpdateAsync(
            update => update.SetProperty(x => x.PlexApiRatingKey, sharedKey),
            CancellationToken
        );
        var selected = await dbContext.PlexMovies.OrderBy(x => x.Id).ToArrayAsync(CancellationToken);
        selected.Select(x => x.Id).Distinct().Count().ShouldBe(2);
        selected.ShouldAllBe(x => x.PlexApiRatingKey == sharedKey);
        (await ReadParentRows(dbContext)).ShouldBeEmpty();
        (await ReadFileRows(dbContext)).ShouldBeEmpty();
        var command = new GenerateDownloadTaskMoviesCommand(
            new CreateDownloadTasksRequest([
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.Movie,
                    SelectionKind = null,
                    PlexServerId = movies[0].PlexServerId,
                    PlexLibraryId = movies[0].PlexLibraryId,
                    MediaIds = movies.Select(x => x.Id).ToList(),
                    Qualities = [],
                },
            ])
        );

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { Movies = 2 });
        result.Value.Total.ShouldBe(2);
        var parents = await ReadParentRows(dbContext);
        var files = await ReadFileRows(dbContext);
        parents.Select(x => x.Id).Distinct().Count().ShouldBe(2);
        parents.Select(x => x.Title).Order().ShouldBe(movies.Select(x => x.Title).Order());
        parents
            .Select(ToParentIdentity)
            .ShouldBe(
                Enumerable.Repeat(
                    new ParentIdentity(sharedKey, movies[0].PlexServerId, movies[0].PlexLibraryId, null, null),
                    2
                )
            );
        files
            .Select(ToFileIdentity)
            .OrderBy(x => x.ParentId)
            .ShouldBe(parents.Select(ToParentFileIdentity).OrderBy(x => x.ParentId));
        files
            .Select(x => (x.PlexApiMediaId, x.PlexApiPartId))
            .OrderBy(x => x.PlexApiPartId)
            .ShouldBe(
                movies
                    .SelectMany(x => x.MediaDataList)
                    .Select(x => (x.PlexApiMediaId, x.PlexApiPartId))
                    .OrderBy(x => x.PlexApiPartId)
            );
        parents.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Queued);
        files.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Queued);
    }

    private async Task<OwnershipScenario> SeedScenarioAsync(bool radarrCaller)
    {
        await SetupDatabase(
            radarrCaller ? 56157 : 56156,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
                config.MovieCount = 5;
                config.RadarrIntegrationCount = 2;
            }
        );

        using var dbContext = IDbContext;
        var movieLibraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.PlexServerId)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var sourceServerId = movieLibraries[0].PlexServerId;
        var sourceLibraryId = movieLibraries[0].Id;

        var sourceMovies = await dbContext
            .PlexMovies.Where(x => x.PlexServerId == sourceServerId && x.PlexLibraryId == sourceLibraryId)
            .IncludeAll()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var selectedRatingKeys = sourceMovies.Select(x => x.PlexApiRatingKey).ToArray();

        var radarrIntegrations = await dbContext
            .RadarrIntegrations.OrderBy(x => x.DisplayName)
            .ToListAsync(CancellationToken);
        var requestIntegration = radarrCaller
            ? new IntegrationIdentity(IntegrationType.Radarr, radarrIntegrations[0].Id)
            : null;
        var matchingRadarrId = requestIntegration?.Id;

        var existingTasks = CreateExistingTasks(
            sourceMovies,
            movieLibraries,
            requestIntegration,
            radarrIntegrations[1].Id
        );
        dbContext.DownloadTaskMovie.AddRange(existingTasks);
        await dbContext.SaveChangesAsync(CancellationToken);

        var existingTaskIds = existingTasks.Select(x => x.Id).ToArray();

        var createdRatingKeys = radarrCaller
            ? selectedRatingKeys.Skip(1).ToArray()
            : [selectedRatingKeys[1], selectedRatingKeys[3], selectedRatingKeys[4]];
        var expectedCreatedParents = createdRatingKeys
            .Select(x => new ParentIdentity(x, sourceServerId, sourceLibraryId, matchingRadarrId, null))
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArray();

        return new OwnershipScenario(
            requestIntegration,
            sourceServerId,
            sourceLibraryId,
            sourceMovies.Select(x => x.Id).ToArray(),
            selectedRatingKeys,
            expectedCreatedParents,
            existingTaskIds
        );
    }

    private static GenerateDownloadTaskMoviesCommand CreateCommand(OwnershipScenario scenario) =>
        new(
            new CreateDownloadTasksRequest(
                [
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.Movie,
                        SelectionKind = null,
                        PlexServerId = scenario.SourceServerId,
                        PlexLibraryId = scenario.SourceLibraryId,
                        MediaIds = scenario.SelectedMovieIds.ToList(),
                        Qualities = [],
                    },
                ],
                integration: scenario.RequestIntegration
            )
        );

    private async Task<ParentSnapshot[]> ReadParentRows(IReaparrDbContext dbContext, Guid[]? ids = null)
    {
        var query = dbContext.DownloadTaskMovie.AsQueryable();
        if (ids is not null)
            query = query.Where(x => ids.Contains(x.Id));

        var rows = await query.ToListAsync(CancellationToken);
        return rows.OrderBy(x => x.Id)
            .Select(x => new ParentSnapshot(
                x.Id,
                x.PlexApiRatingKey,
                x.PlexServerId,
                x.PlexLibraryId,
                x.RadarrIntegrationId,
                x.SonarrIntegrationId,
                x.DownloadStatus,
                x.Title,
                x.FullTitle,
                x.Year,
                x.CreatedAt
            ))
            .ToArray();
    }

    private async Task<FileSnapshot[]> ReadFileRows(IReaparrDbContext dbContext, Guid[]? parentIds = null)
    {
        var query = dbContext.DownloadTaskMovieFile.AsQueryable();
        if (parentIds is not null)
            query = query.Where(x => parentIds.Contains(x.ParentId));

        var rows = await query.ToListAsync(CancellationToken);
        return rows.OrderBy(x => x.Id)
            .Select(x => new FileSnapshot(
                x.Id,
                x.ParentId,
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexServerId,
                x.PlexLibraryId,
                x.RadarrIntegrationId,
                x.SonarrIntegrationId,
                x.DownloadStatus,
                x.HashId,
                x.Title,
                x.FullTitle,
                x.FileName,
                x.FileLocationUrl,
                x.VideoQuality,
                x.DataTotal,
                x.DestinationFolderPathId,
                x.DownloadClientType
            ))
            .ToArray();
    }

    private static ParentIdentity ToParentIdentity(ParentSnapshot x) =>
        new(x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId, x.RadarrIntegrationId, x.SonarrIntegrationId);

    private static FileIdentity ToFileIdentity(FileSnapshot x) =>
        new(
            x.ParentId,
            x.PlexApiRatingKey,
            x.PlexServerId,
            x.PlexLibraryId,
            x.RadarrIntegrationId,
            x.SonarrIntegrationId
        );

    private static FileIdentity ToParentFileIdentity(ParentSnapshot x) =>
        new(x.Id, x.PlexApiRatingKey, x.PlexServerId, x.PlexLibraryId, x.RadarrIntegrationId, x.SonarrIntegrationId);

    private static DownloadTaskMovie[] CreateExistingTasks(
        IReadOnlyList<PlexMovie> sourceMovies,
        IReadOnlyList<PlexLibrary> libraries,
        IntegrationIdentity? integration,
        Guid otherRadarrId
    )
    {
        var sourceLibrary = libraries.Single(x => x.Id == sourceMovies[0].PlexLibraryId);
        var duplicateLibrary = libraries.First(x =>
            x.PlexServerId == sourceLibrary.PlexServerId && x.Id != sourceLibrary.Id
        );
        var otherServerLibrary = libraries.First(x => x.PlexServerId != sourceLibrary.PlexServerId);
        DownloadTaskMovie[] tasks =
        [
            // A different persisted library does not change the original duplicate identity.
            CreateExistingTask(sourceMovies[0], duplicateLibrary, integration?.Id, 56160),
            CreateExistingTask(sourceMovies[1], sourceLibrary, otherRadarrId, 56161),
            CreateExistingTask(sourceMovies[2], sourceLibrary, null, 56162),
            CreateExistingTask(sourceMovies[3], otherServerLibrary, integration?.Id, 56163),
        ];
        tasks[0].SetDownloadStatus(DownloadStatus.Downloading);
        tasks[2].SetDownloadStatus(DownloadStatus.Error);
        tasks[3].SetDownloadStatus(DownloadStatus.Queued);
        return tasks;
    }

    private static DownloadTaskMovie CreateExistingTask(
        PlexMovie sourceMovie,
        PlexLibrary library,
        Guid? radarrIntegrationId,
        int seed
    )
    {
        var task = FakeData.GetMovieDownloadTask(new Seed(seed)).Generate();
        task.UpdateInitProperty(nameof(DownloadTaskMovie.PlexApiRatingKey), sourceMovie.PlexApiRatingKey);
        task.PlexServerId = library.PlexServerId;
        task.PlexLibraryId = library.Id;
        task.SonarrIntegrationId = null;
        task.RadarrIntegrationId = radarrIntegrationId;
        task.DownloadStatus = DownloadStatus.Paused;

        var sourceMediaData = sourceMovie.MediaDataList.Single();
        foreach (var file in task.Children)
        {
            file.PlexServerId = library.PlexServerId;
            file.PlexLibraryId = library.Id;
            file.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiRatingKey), sourceMovie.PlexApiRatingKey);
            file.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiMediaId), sourceMediaData.PlexApiMediaId);
            file.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiPartId), sourceMediaData.PlexApiPartId);
            file.UpdateInitProperty(nameof(DownloadTaskMovieFile.HashId), $"existing-control-{seed}");
            file.SonarrIntegrationId = null;
            file.RadarrIntegrationId = radarrIntegrationId;
            file.DownloadStatus = DownloadStatus.Paused;
        }

        return task;
    }

    private sealed record OwnershipScenario(
        IntegrationIdentity? RequestIntegration,
        int SourceServerId,
        int SourceLibraryId,
        int[] SelectedMovieIds,
        int[] SelectedRatingKeys,
        ParentIdentity[] ExpectedCreatedParents,
        Guid[] ExistingTaskIds
    );

    private sealed record ParentIdentity(
        int PlexApiRatingKey,
        int PlexServerId,
        int PlexLibraryId,
        Guid? RadarrIntegrationId,
        Guid? SonarrIntegrationId
    );

    private sealed record FileIdentity(
        Guid ParentId,
        int PlexApiRatingKey,
        int PlexServerId,
        int PlexLibraryId,
        Guid? RadarrIntegrationId,
        Guid? SonarrIntegrationId
    );

    private sealed record ParentSnapshot(
        Guid Id,
        int PlexApiRatingKey,
        int PlexServerId,
        int PlexLibraryId,
        Guid? RadarrIntegrationId,
        Guid? SonarrIntegrationId,
        DownloadStatus DownloadStatus,
        string Title,
        string FullTitle,
        int Year,
        DateTime CreatedAt
    );

    private sealed record FileSnapshot(
        Guid Id,
        Guid ParentId,
        int PlexApiRatingKey,
        int PlexApiMediaId,
        int PlexApiPartId,
        int PlexServerId,
        int PlexLibraryId,
        Guid? RadarrIntegrationId,
        Guid? SonarrIntegrationId,
        DownloadStatus DownloadStatus,
        string? HashId,
        string Title,
        string FullTitle,
        string FileName,
        string FileLocationUrl,
        VideoQuality VideoQuality,
        long DataTotal,
        int? DestinationFolderPathId,
        PlexDownloadClientType DownloadClientType
    );
}
