namespace Reaparr.Application.UnitTests;

public class GetTvShowDownloadPreviewCommandUnitTests : BaseCommandUnitTest<GetTvShowDownloadPreviewCommand>
{
    [Test]
    public async Task ShouldNotDuplicateEpisodes_WhenSameEpisodeIsRequestedTwice()
    {
        // Arrange
        await SetupDatabase(
            66778,
            config =>
            {
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );
        var episode = (await IDbContext.PlexTvShowEpisodes.ToListAsync(CancellationToken)).First();
        var query = new GetTvShowDownloadPreviewCommand([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                PlexServerId = episode.PlexServerId,
                PlexLibraryId = episode.PlexLibraryId,
                MediaIds = [episode.Id],
                Qualities = [],
            },
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                PlexServerId = episode.PlexServerId,
                PlexLibraryId = episode.PlexLibraryId,
                MediaIds = [episode.Id],
                Qualities = [],
            },
        ]);

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count().ShouldBe(1);
        result.Value.Single().Children.Count.ShouldBe(1);
        result.Value.Single().Children.Single().Children.Count.ShouldBe(1);
    }

    [Test]
    [Arguments(PlexMediaType.Episode, DownloadStatus.Queued)]
    [Arguments(PlexMediaType.Episode, DownloadStatus.Completed)]
    [Arguments(PlexMediaType.Season, DownloadStatus.Queued)]
    [Arguments(PlexMediaType.Season, DownloadStatus.Completed)]
    [Arguments(PlexMediaType.TvShow, DownloadStatus.Queued)]
    [Arguments(PlexMediaType.TvShow, DownloadStatus.Completed)]
    public async Task ShouldReturnExactSelectedHierarchy_WhenMatchingEpisodeTaskHasStatus(
        PlexMediaType selectionType,
        DownloadStatus status
    )
    {
        // Arrange
        await SetupDatabase(
            66779,
            config =>
            {
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );
        var dbContext = IDbContext;
        var tvShow = await dbContext.PlexTvShows.SingleAsync(CancellationToken);
        var seasons = await dbContext.PlexTvShowSeason.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var episodes = await dbContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .Where(x => x.TvShowId == tvShow.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        seasons.Count.ShouldBe(2);
        episodes.Count.ShouldBe(4);

        var targetEpisode = episodes[0];
        var targetSeason = seasons.Single(x => x.Id == targetEpisode.TvShowSeasonId);
        var queueSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.Episode,
            SelectionKind = null,
            PlexServerId = targetEpisode.PlexServerId,
            PlexLibraryId = targetEpisode.PlexLibraryId,
            MediaIds = [targetEpisode.Id],
            Qualities = [],
        };
        var creator = Mock.Create<GenerateDownloadTaskTvShowEpisodesCommandHandler>();
        var created = await creator.ExecuteAsync(
            new GenerateDownloadTaskTvShowEpisodesCommand([queueSelection]),
            CancellationToken
        );
        created.IsSuccess.ShouldBeTrue();
        created.Errors.Count.ShouldBe(0);
        created.Value.Episodes.ShouldBe(1);

        var queuedRoot = await dbContext
            .DownloadTaskTvShow.IncludeAll().AsTracking()
            .SingleAsync(x => x.PlexApiRatingKey == tvShow.PlexApiRatingKey, CancellationToken);
        var queuedEpisode = queuedRoot.Children
            .SelectMany(x => x.Children)
            .Single(x => x.PlexApiRatingKey == targetEpisode.PlexApiRatingKey);
        queuedEpisode.Children.Count.ShouldBe(1);
        queuedEpisode.Children.Single().DownloadStatus.ShouldBe(DownloadStatus.Queued);
        queuedEpisode.Children.Single().DownloadStatus = status;
        queuedRoot.Calculate();
        await dbContext.SaveChangesAsync(CancellationToken);

        var persistedRoot = await dbContext
            .DownloadTaskTvShow.IncludeAll()
            .SingleAsync(x => x.PlexApiRatingKey == tvShow.PlexApiRatingKey, CancellationToken);
        persistedRoot.DownloadStatus.ShouldBe(status);
        persistedRoot.Children
            .SelectMany(x => x.Children)
            .Single(x => x.PlexApiRatingKey == targetEpisode.PlexApiRatingKey)
            .Children.Single()
            .DownloadStatus.ShouldBe(status);

        List<int> selectedIds = selectionType switch
        {
            PlexMediaType.Episode => [targetEpisode.Id],
            PlexMediaType.Season => [targetSeason.Id],
            PlexMediaType.TvShow => [tvShow.Id],
            _ => throw new ArgumentOutOfRangeException(nameof(selectionType), selectionType, null),
        };
        var previewSelection = new DownloadMediaDTO
        {
            Type = selectionType,
            SelectionKind = null,
            PlexServerId = targetEpisode.PlexServerId,
            PlexLibraryId = targetEpisode.PlexLibraryId,
            MediaIds = selectedIds,
            Qualities = [],
        };
        var expectedEpisodes = selectionType switch
        {
            PlexMediaType.Episode => episodes.Where(x => x.Id == targetEpisode.Id).ToList(),
            PlexMediaType.Season => episodes.Where(x => x.TvShowSeasonId == targetSeason.Id).ToList(),
            PlexMediaType.TvShow => episodes.ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(selectionType), selectionType, null),
        };
        var expectedSeasons = seasons
            .Where(x => expectedEpisodes.Select(y => y.TvShowSeasonId).Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToList();

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(
            new GetTvShowDownloadPreviewCommand([previewSelection])
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var root = result.Value.Single();
        root.Id.ShouldBe(tvShow.Id);
        root.Title.ShouldBe(tvShow.Title);
        root.MediaType.ShouldBe(PlexMediaType.TvShow);
        root.Size.ShouldBe(expectedEpisodes.Sum(x => x.MediaSize));
        root.ChildCount.ShouldBe(expectedSeasons.Count);

        var actualSeasons = root.Children.OrderBy(x => x.Id).ToList();
        actualSeasons
            .Select(x => (x.Id, x.Title, x.Size, x.ChildCount, x.MediaType, x.TvShowId))
            .ShouldBe(
                expectedSeasons
                    .Select(x =>
                        (
                            x.Id,
                            x.Title,
                            expectedEpisodes.Where(y => y.TvShowSeasonId == x.Id).Sum(y => y.MediaSize),
                            expectedEpisodes.Count(y => y.TvShowSeasonId == x.Id),
                            PlexMediaType.Season,
                            x.TvShowId
                        )
                    )
                    .ToList()
            );

        foreach (var expectedSeason in expectedSeasons)
        {
            var seasonPreview = actualSeasons.Single(x => x.Id == expectedSeason.Id);
            var expectedSeasonEpisodes = expectedEpisodes
                .Where(x => x.TvShowSeasonId == expectedSeason.Id)
                .OrderBy(x => x.Id)
                .ToList();
            var actualEpisodes = seasonPreview.Children.OrderBy(x => x.Id).ToList();
            actualEpisodes
                .Select(x => (x.Id, x.Title, x.Size, x.ChildCount, x.MediaType, x.TvShowId, x.SeasonId))
                .ShouldBe(
                    expectedSeasonEpisodes
                        .Select(x =>
                            (
                                x.Id,
                                x.Title,
                                x.MediaSize,
                                x.ChildCount,
                                PlexMediaType.Episode,
                                x.TvShowId,
                                x.TvShowSeasonId
                            )
                        )
                        .ToList()
                );

            foreach (var expectedEpisode in expectedSeasonEpisodes)
            {
                var episodePreview = actualEpisodes.Single(x => x.Id == expectedEpisode.Id);
                episodePreview.Qualities.OrderBy(x => x.DataId)
                    .Select(x => (x.DataId, x.MediaId, x.VideoQuality, x.MediaDataType))
                    .ShouldBe(
                        expectedEpisode.MediaDataList
                            .OrderBy(x => x.Id)
                            .Select(x => (x.Id, expectedEpisode.Id, x.VideoQuality, x.Type))
                            .ToList()
                    );
            }
        }
    }
}
