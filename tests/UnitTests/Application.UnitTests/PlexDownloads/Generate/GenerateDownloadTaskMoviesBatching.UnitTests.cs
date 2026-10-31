using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Reaparr.Data;

namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskMoviesBatchingUnitTests : BaseCommandUnitTest<GenerateDownloadTaskMoviesCommand>
{
    private const int DestinationFolderPathId = 99;
    private const string CustomDestinationFolderPath = "/custom-movie-destination";

    [Test]
    [Arguments(1, 1)]
    [Arguments(1, 20)]
    [Arguments(3, 20)]
    public async Task ShouldPreloadDuplicateMovieKeysOncePerGroupAndPersistOnlyNewMovies(
        int groupCount,
        int moviesPerGroup
    )
    {
        // Arrange
        var scenario = await CreateScenario(groupCount, moviesPerGroup);
        using var dbContext = IDbContext;
        var before = await ReadState(dbContext);
        before.Parents.Length.ShouldBe(groupCount);
        before.Files.Length.ShouldBe(groupCount * 2);
        before.Logs.ShouldBeEmpty();
        scenario.Groups.SelectMany(x => x.Movies).Select(x => x.MediaDataList.Count).ShouldAllBe(x => x == 2);
        scenario
            .Groups.SelectMany(x => x.Movies)
            .SelectMany(x => x.MediaDataList)
            .GroupBy(x => x.PlexApiMediaId)
            .ShouldAllBe(x => x.Count() == 2);
        before
            .Parents.Select(x =>
                (x.PlexServerId, x.PlexLibraryId, x.PlexApiRatingKey, x.RadarrIntegrationId, x.SonarrIntegrationId)
            )
            .OrderBy(x => x.PlexLibraryId)
            .ShouldBe(
                scenario
                    .Groups.Select(x =>
                        (
                            x.PlexServerId,
                            x.LibraryId,
                            x.Movies[0].PlexApiRatingKey,
                            (Guid?)scenario.Owner.Id,
                            (Guid?)null
                        )
                    )
                    .OrderBy(x => x.Item2)
            );
        var reads = new MovieDuplicateReadCounter();
        await using var measuredContext = CreateMeasuredContext(dbContext.DatabaseName, reads);
        SetupDependencies(builder =>
            builder.RegisterInstance(measuredContext).As<IReaparrDbContext>().ExternallyOwned()
        );

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(scenario.Command);

        // Assert
        reads.DuplicateSelects.ShouldBe(groupCount);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { Movies = scenario.ExpectedNewMovies.Length });
        result.Value.Total.ShouldBe(scenario.ExpectedNewMovies.Length);
        var after = await ReadState(dbContext);
        var parentIds = before.Parents.Select(x => x.Id).ToHashSet();
        var fileIds = before.Files.Select(x => x.Id).ToHashSet();
        var newParents = after
            .Parents.Where(x => !parentIds.Contains(x.Id))
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.PlexApiRatingKey)
            .ToArray();
        var newFiles = after
            .Files.Where(x => !fileIds.Contains(x.Id))
            .OrderBy(x => x.ParentId)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .ToArray();
        after
            .Parents.Where(x => parentIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ShouldBe(before.Parents.OrderBy(x => x.Id));
        after.Files.Where(x => fileIds.Contains(x.Id)).OrderBy(x => x.Id).ShouldBe(before.Files.OrderBy(x => x.Id));
        newParents.Select(x => x with { Id = Guid.Empty, CreatedAt = default }).ShouldBe(scenario.ExpectedNewParents);
        newFiles.Select(x => x with { Id = Guid.Empty }).ShouldBe(ExpectedFiles(scenario, newParents));
        after.Logs.ShouldBe(ExpectedLogs(newFiles));
    }

    private async Task<Scenario> CreateScenario(int groupCount, int moviesPerGroup)
    {
        await SetupDatabase(
            56000 + groupCount * 100 + moviesPerGroup,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = groupCount;
                config.MovieCount = moviesPerGroup;
                config.IncludeMultiPartMovies = true;
                config.RadarrIntegrationCount = 1;
            }
        );
        using var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .ToArrayAsync(CancellationToken);
        var movies = await dbContext
            .PlexMovies.IncludeAll()
            .Where(x => libraries.Select(y => y.Id).Contains(x.PlexLibraryId))
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownerId = await dbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var owner = new IntegrationIdentity(IntegrationType.Radarr, ownerId);
        var groups = libraries
            .Select(library => new MovieGroup(
                library.Id,
                library.PlexServerId,
                movies.Where(x => x.PlexLibraryId == library.Id).ToArray()
            ))
            .ToArray();
        dbContext.DownloadTaskMovie.AddRange(
            groups.Select((group, index) => CreateExistingTask(group.Movies[0], new Seed(57000 + index * 10), owner))
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedNewMovies = groups.SelectMany(x => x.Movies.Skip(1)).ToArray();
        return new Scenario(
            groups,
            owner,
            expectedNewMovies,
            ExpectedParents(expectedNewMovies, owner),
            CreateCommand(groups, owner)
        );
    }

    private static GenerateDownloadTaskMoviesCommand CreateCommand(MovieGroup[] groups, IntegrationIdentity owner) =>
        new(
            new CreateDownloadTasksRequest(
                groups
                    .Select(group => new DownloadMediaDTO
                    {
                        Type = PlexMediaType.Movie,
                        SelectionKind = null,
                        PlexServerId = group.PlexServerId,
                        PlexLibraryId = group.LibraryId,
                        MediaIds = group.Movies.Select(x => x.Id).ToList(),
                        Qualities = group
                            .Movies.Select(movie =>
                            {
                                var data = movie.MediaDataList.OrderBy(x => x.Id).First();
                                return new PlexMediaQualityDTO
                                {
                                    MediaDataType = PlexMediaType.Movie,
                                    VideoQuality = data.VideoQuality,
                                    MediaId = movie.Id,
                                    DataId = data.Id,
                                };
                            })
                            .ToList(),
                        KeepCompletedInDownloadFolder = true,
                    })
                    .ToList(),
                DestinationFolderPathId,
                CustomDestinationFolderPath,
                owner
            )
        );

    private static ParentRow[] ExpectedParents(PlexMovie[] movies, IntegrationIdentity owner) =>
        movies
            .Select(x => new ParentRow(
                Guid.Empty,
                x.PlexApiRatingKey,
                x.PlexServerId,
                x.PlexLibraryId,
                owner.Id,
                null,
                x.Title,
                x.FullTitle,
                x.Year,
                DownloadStatus.Queued,
                default
            ))
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.PlexApiRatingKey)
            .ToArray();

    private static LogRow[] ExpectedLogs(FileRow[] files) =>
        files
            .Select(x => new LogRow(
                x.Id,
                x.ParentId,
                DownloadStatus.Queued,
                NotificationLevel.Information,
                $"DownloadTask {x.FileName} was queued for downloading"
            ))
            .OrderBy(x => x.DownloadTaskFileId)
            .ToArray();

    private static DownloadTaskMovie CreateExistingTask(PlexMovie movie, Seed seed, IntegrationIdentity owner)
    {
        var task = FakeData.GetMovieDownloadTask(seed, config => config.IncludeMultiPartMovies = true).Generate();
        task.UpdateInitProperty(nameof(DownloadTaskMovie.PlexApiRatingKey), movie.PlexApiRatingKey);
        task.PlexServerId = movie.PlexServerId;
        task.PlexLibraryId = movie.PlexLibraryId;
        task.SonarrIntegrationId = null;
        task.RadarrIntegrationId = owner.Id;
        task.SetDownloadStatus(DownloadStatus.Downloading);
        foreach (var child in task.Children)
        {
            child.PlexServerId = movie.PlexServerId;
            child.PlexLibraryId = movie.PlexLibraryId;
            child.SonarrIntegrationId = null;
            child.RadarrIntegrationId = owner.Id;
        }
        return task;
    }

    private static FileRow[] ExpectedFiles(Scenario scenario, ParentRow[] newParents)
    {
        var expected = new List<FileRow>();
        foreach (var movie in scenario.ExpectedNewMovies)
        {
            var parent = newParents.Single(x =>
                x.PlexLibraryId == movie.PlexLibraryId && x.PlexApiRatingKey == movie.PlexApiRatingKey
            );
            foreach (var data in movie.MediaDataList.OrderBy(x => x.Id))
                expected.Add(
                    new FileRow(
                        Guid.Empty,
                        parent.Id,
                        movie.PlexServerId,
                        movie.PlexLibraryId,
                        data.PlexApiRatingKey,
                        data.PlexApiMediaId,
                        data.PlexApiPartId,
                        data.VideoQuality,
                        DownloadStatus.Queued,
                        DestinationFolderPathId,
                        scenario.Owner.Id,
                        null,
                        data.GetFileName,
                        data.Key,
                        data.Size,
                        CustomDestinationFolderPath,
                        true
                    )
                );
        }
        return expected.OrderBy(x => x.ParentId).ThenBy(x => x.PlexApiMediaId).ThenBy(x => x.PlexApiPartId).ToArray();
    }

    private async Task<State> ReadState(IReaparrDbContext dbContext) =>
        new(await ReadParentRows(dbContext), await ReadFileRows(dbContext), await ReadLogRows(dbContext));

    private async Task<ParentRow[]> ReadParentRows(IReaparrDbContext dbContext) =>
        (await dbContext.DownloadTaskMovie.ToListAsync(CancellationToken))
            .Select(x => new ParentRow(
                x.Id,
                x.PlexApiRatingKey,
                x.PlexServerId,
                x.PlexLibraryId,
                x.RadarrIntegrationId,
                x.SonarrIntegrationId,
                x.Title,
                x.FullTitle,
                x.Year,
                x.DownloadStatus,
                x.CreatedAt
            ))
            .OrderBy(x => x.Id)
            .ToArray();

    private async Task<FileRow[]> ReadFileRows(IReaparrDbContext dbContext) =>
        (await dbContext.DownloadTaskMovieFile.ToListAsync(CancellationToken))
            .Select(x => new FileRow(
                x.Id,
                x.ParentId,
                x.PlexServerId,
                x.PlexLibraryId,
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.VideoQuality,
                x.DownloadStatus,
                x.DestinationFolderPathId,
                x.RadarrIntegrationId,
                x.SonarrIntegrationId,
                x.FileName,
                x.FileLocationUrl,
                x.DataTotal,
                x.DirectoryMeta.DestinationRootPath,
                x.DirectoryMeta.KeepCompletedInDownloadFolder
            ))
            .OrderBy(x => x.Id)
            .ToArray();

    private async Task<LogRow[]> ReadLogRows(IReaparrDbContext dbContext) =>
        (await dbContext.DownloadTaskMovieFileLogs.ToListAsync(CancellationToken))
            .Select(x => new LogRow(x.DownloadTaskFileId, x.DownloadTaskMovieId, x.Status, x.LogLevel, x.Message))
            .OrderBy(x => x.DownloadTaskFileId)
            .ToArray();

    private ReaparrDbContext CreateMeasuredContext(string databaseName, MovieDuplicateReadCounter reads)
    {
        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(databaseName, SqliteOpenMode.Memory);
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).AddInterceptors(reads);
        return new ReaparrDbContext(
            options.Options,
            Mock.Container.Resolve<ILogger>(),
            Mock.Container.Resolve<IPathProvider>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            databaseName
        );
    }

    private sealed record Scenario(
        MovieGroup[] Groups,
        IntegrationIdentity Owner,
        PlexMovie[] ExpectedNewMovies,
        ParentRow[] ExpectedNewParents,
        GenerateDownloadTaskMoviesCommand Command
    );

    private sealed record MovieGroup(int LibraryId, int PlexServerId, PlexMovie[] Movies);

    private sealed record State(ParentRow[] Parents, FileRow[] Files, LogRow[] Logs);

    private sealed record ParentRow(
        Guid Id,
        int PlexApiRatingKey,
        int PlexServerId,
        int PlexLibraryId,
        Guid? RadarrIntegrationId,
        Guid? SonarrIntegrationId,
        string Title,
        string FullTitle,
        int Year,
        DownloadStatus DownloadStatus,
        DateTime CreatedAt
    );

    private sealed record FileRow(
        Guid Id,
        Guid ParentId,
        int PlexServerId,
        int PlexLibraryId,
        int PlexApiRatingKey,
        int PlexApiMediaId,
        int PlexApiPartId,
        VideoQuality VideoQuality,
        DownloadStatus DownloadStatus,
        int? DestinationFolderPathId,
        Guid? RadarrIntegrationId,
        Guid? SonarrIntegrationId,
        string FileName,
        string FileLocationUrl,
        long DataTotal,
        string DestinationRootPath,
        bool KeepCompletedInDownloadFolder
    );

    private sealed record LogRow(
        Guid DownloadTaskFileId,
        Guid DownloadTaskMovieId,
        DownloadStatus Status,
        NotificationLevel LogLevel,
        string Message
    );

    private sealed class MovieDuplicateReadCounter : DbCommandInterceptor
    {
        public int DuplicateSelects { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result
        )
        {
            CountDuplicateSelect(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            CountDuplicateSelect(command);
            return ValueTask.FromResult(result);
        }

        private void CountDuplicateSelect(DbCommand command)
        {
            if (
                command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("FROM \"DownloadTaskMovie\"", StringComparison.Ordinal)
            )
                DuplicateSelects++;
        }
    }
}
