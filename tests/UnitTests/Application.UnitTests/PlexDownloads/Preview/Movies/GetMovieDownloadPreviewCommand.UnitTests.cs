namespace Reaparr.Application.UnitTests;

public class GetMovieDownloadPreviewCommandUnitTests : BaseCommandUnitTest<GetMovieDownloadPreviewCommand>
{
    [Test]
    public async Task ShouldReturnMoviePreviewWithSelectedQualities()
    {
        // Arrange
        await SetupDatabase(
            23456,
            config =>
            {
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        var movies = await IDbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(CancellationToken);
        var query = new GetMovieDownloadPreviewCommand([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Movie,
                SelectionKind = null,
                PlexServerId = movies[0].PlexServerId,
                PlexLibraryId = movies[0].PlexLibraryId,
                MediaIds = movies.Select(x => x.Id).ToList(),
                Qualities = movies
                    .SelectMany(x =>
                        x.MediaDataList.Take(1)
                            .Select(data => new PlexMediaQualityDTO
                            {
                                MediaId = x.Id,
                                DataId = data.Id,
                                MediaDataType = PlexMediaType.Movie,
                                VideoQuality = VideoQuality.UHD_4K,
                            })
                    )
                    .ToList(),
            },
        ]);

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count().ShouldBe(2);
        result.Value.ShouldAllBe(x => x.MediaType == PlexMediaType.Movie);
        result.Value.ShouldAllBe(x => x.Qualities.Count == 1);
        result.Value.ShouldAllBe(x => x.Size > 0);
    }

    [Test]
    [Arguments(DownloadStatus.Queued)]
    [Arguments(DownloadStatus.Completed)]
    public async Task ShouldReturnSelectedMoviesWithExactSizes_WhenMatchingTaskHasStatus(
        DownloadStatus status
    )
    {
        // Arrange
        await SetupDatabase(
            23457,
            config =>
            {
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetMovie = movies[0];
        var queueSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.Movie,
            SelectionKind = null,
            PlexServerId = targetMovie.PlexServerId,
            PlexLibraryId = targetMovie.PlexLibraryId,
            MediaIds = [targetMovie.Id],
            Qualities = [],
        };
        var creator = Mock.Create<GenerateDownloadTaskMoviesCommandHandler>();
        var created = await creator.ExecuteAsync(
            new GenerateDownloadTaskMoviesCommand([queueSelection]),
            CancellationToken
        );
        created.IsSuccess.ShouldBeTrue();
        created.Errors.Count.ShouldBe(0);
        created.Value.Movies.ShouldBe(1);

        var queuedTask = await dbContext
            .DownloadTaskMovie.IncludeAll().AsTracking()
            .SingleAsync(x => x.PlexApiRatingKey == targetMovie.PlexApiRatingKey, CancellationToken);
        queuedTask.Children.Count.ShouldBe(1);
        queuedTask.Children.Single().DownloadStatus.ShouldBe(DownloadStatus.Queued);
        queuedTask.Children.Single().DownloadStatus = status;
        queuedTask.Calculate();
        await dbContext.SaveChangesAsync(CancellationToken);

        var persistedTask = await dbContext
            .DownloadTaskMovie.IncludeAll()
            .SingleAsync(x => x.PlexApiRatingKey == targetMovie.PlexApiRatingKey, CancellationToken);
        persistedTask.DownloadStatus.ShouldBe(status);
        persistedTask.Children.Single().DownloadStatus.ShouldBe(status);

        var previewSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.Movie,
            SelectionKind = null,
            PlexServerId = targetMovie.PlexServerId,
            PlexLibraryId = targetMovie.PlexLibraryId,
            MediaIds = movies.Select(x => x.Id).ToList(),
            Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(
            new GetMovieDownloadPreviewCommand([previewSelection])
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expectedMovies = movies.OrderByNatural(x => x.Title).ToList();
        var previews = result.Value.ToList();
        previews
            .Select(x => (x.Id, x.Title, x.Size, x.ChildCount, x.MediaType))
            .ShouldBe(
                expectedMovies
                    .Select(x => (x.Id, x.Title, x.MediaDataList.Sum(y => y.Size), x.ChildCount, x.Type))
                    .ToList()
            );

        foreach (var movie in expectedMovies)
        {
            var preview = previews.Single(x => x.Id == movie.Id);
            preview.Qualities.OrderBy(x => x.DataId)
                .Select(x => (x.DataId, x.MediaId, x.VideoQuality, x.MediaDataType))
                .ShouldBe(
                    movie.MediaDataList
                        .OrderBy(x => x.Id)
                        .Select(x => (x.Id, movie.Id, x.VideoQuality, x.Type))
                        .ToList()
                );
        }
    }
}
