using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewOtherVideoCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewOtherVideoCommand>
{
    [Test]
    [Arguments("videoQuality:asc", "Actors")]
    [Arguments("videoQuality:desc", "Actors")]
    [Arguments("videoQuality:asc", "Countries")]
    [Arguments("videoQuality:desc", "Countries")]
    [Arguments("videoQuality:asc", "Genres")]
    [Arguments("videoQuality:desc", "Genres")]
    public async Task ShouldUsePersistedQualityRankForFilteredPage_WithExactQualitiesAndFullNavigation(
        string sort,
        string metadataField
    )
    {
        // Arrange
        var fixture = await ArrangeFilteredOtherVideoPageAsync();
        var metadataId = metadataField switch
        {
            "Actors" => fixture.Actor.Id,
            "Countries" => fixture.Country.Id,
            _ => fixture.Genre.Id,
        };
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.OtherVideos,
            PlexLibraryId = fixture.LibraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = 2,
                PageSize = 1,
                Sort = sort,
                Filter = $"{metadataField}:any:Id:eq:{metadataId}",
            },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(
            new GetMediaOverviewOtherVideoCommand(filter)
        );

        // Assert
        AssertFilteredOtherVideoPage(result, filter, fixture, sort);
    }


    private async Task<OtherVideoOverviewFixture> ArrangeFilteredOtherVideoPageAsync()
    {
        await SetupDatabase(
            84203,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 2;
                config.OtherVideoCount = 4;
            }
        );
        var dbContext = IDbContext;
        var libraryIds = await dbContext
            .PlexLibraries.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var videos = await dbContext
            .PlexOtherVideos.OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var target = videos.Where(x => x.PlexLibraryId == libraryIds[0]).ToList();
        target.Count.ShouldBe(4);
        videos.Count(x => x.PlexLibraryId == libraryIds[1]).ShouldBe(4);
        var selectedData = await dbContext
            .PlexOtherVideoData.Where(x => x.PlexOtherVideoId == target[2].Id)
            .SingleAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);

        var metadataSeed = new Seed(84204);
        var actor = FakeData.GetPlexActors(metadataSeed).Generate();
        var country = FakeData.GetPlexCountries(metadataSeed).Generate();
        var genre = FakeData.GetPlexGenresVideo(metadataSeed).Generate();
        dbContext.PlexActors.Add(actor);
        dbContext.PlexCountries.Add(country);
        dbContext.PlexGenresVideo.Add(genre);
        await dbContext.SaveChangesAsync(CancellationToken);
        foreach (var video in videos)
        {
            var targetIndex = target.FindIndex(x => x.Id == video.Id);
            var qualityRank = targetIndex switch
            {
                0 => 2,
                1 => 0,
                2 => 1,
                _ => 3,
            };
            var quality = targetIndex switch
            {
                0 => VideoQuality.UHD_4K,
                1 => VideoQuality.HD,
                _ => VideoQuality.FullHD,
            };
            var title = targetIndex switch
            {
                0 => "alpha",
                1 => "bravo",
                2 => "charlie",
                _ => "control",
            };
            var year = targetIndex == 3 ? 1999 : 2000;
            var mediaSize = targetIndex >= 0 ? (targetIndex + 1) * 100L : 9000L;
            await dbContext
                .PlexOtherVideos.Where(x => x.Id == video.Id)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(y => y.SearchTitle, title)
                            .SetProperty(y => y.Year, year)
                            .SetProperty(y => y.MediaSize, mediaSize)
                            .SetProperty(y => y.VideoQuality, quality)
                            .SetProperty(y => y.HasThumb, true),
                    CancellationToken
                );
            await dbContext
                .PlexOtherVideoData.Where(x => x.PlexOtherVideoId == video.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.VideoQuality, quality), CancellationToken);
            dbContext.MediaOverviewOtherVideoSnapshots.Add(
                new MediaOverviewOtherVideoSnapshot
                {
                    PlexOtherVideoId = video.Id,
                    PlexLibraryId = video.PlexLibraryId,
                    SearchTitle = title,
                    NavigationTitleLabel = MediaNavigationIndexBuilder.GetTitleLabel(title),
                    Year = year,
                    AddedAt = video.AddedAt,
                    UpdatedAt = video.UpdatedAt,
                    Duration = video.Duration,
                    MediaSize = mediaSize,
                    VideoQuality = quality,
                    TitleRank = targetIndex >= 0 ? targetIndex : 0,
                    QualityRank = qualityRank,
                    YearRank = qualityRank,
                    AddedAtRank = qualityRank,
                    UpdatedAtRank = qualityRank,
                    DurationRank = qualityRank,
                    MediaSizeRank = qualityRank,
                }
            );
            if (targetIndex != 3)
            {
                dbContext.PlexOtherVideoActors.Add(new PlexOtherVideoActors(actor.Id, video.Id));
                dbContext.PlexOtherVideoCountries.Add(new PlexOtherVideoCountries(country.Id, video.Id));
                dbContext.PlexOtherVideoGenres.Add(new PlexOtherVideoGenres(genre.Id, video.Id));
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        return new(libraryIds[0], target[2], selectedData, actor, country, genre);
    }

    private void AssertFilteredOtherVideoPage(
        Result<PagedMediaQueryResult> result,
        MediaQueryFilter filter,
        OtherVideoOverviewFixture fixture,
        string sort
    )
    {
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.QueryHash.ShouldBe(filter.QueryHash);
        (result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.MediaCount).ShouldBe(
            (2, 1, 3, 3)
        );
        (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((600L, 600L));
        result.Value.Items.Select(x => x.Id).ShouldBe([fixture.SelectedVideo.Id]);
        var item = result.Value.Items.Single();
        (item.Type, item.SortIndex, item.ChildCount, item.GrandChildCount).ShouldBe(
            (PlexMediaType.OtherVideos, 2, 0, 0)
        );
        (
            item.PlexLibraryId,
            item.PlexServerId,
            item.PlexApiRatingKey,
            item.PlexApiMetaDataKey,
            item.HasThumb
        ).ShouldBe(
            (
                fixture.LibraryId,
                fixture.SelectedVideo.PlexServerId,
                fixture.SelectedVideo.PlexApiRatingKey,
                fixture.SelectedVideo.PlexApiMetaDataKey,
                true
            )
        );
        (item.SearchTitle, item.MediaSize).ShouldBe(("charlie", 300L));
        item.Qualities.Select(x => (x.DataId, x.MediaId, x.MediaDataType, Quality: x.VideoQuality))
            .ShouldBe([(fixture.SelectedData.Id, fixture.SelectedVideo.Id, PlexMediaType.OtherVideos, VideoQuality.FullHD)]);
        result.Value.Qualities.ShouldBe([VideoQuality.FullHD.ToId()]);
        result.Value.Roles.ShouldBe([fixture.Actor.Id]);
        result.Value.Countries.ShouldBe([fixture.Country.Id]);
        result.Value.Genres.ShouldBe([fixture.Genre.Id]);
        result
            .Value.NavigationIndexes.Select(x => (x.Label, x.Index))
            .ShouldBe(
                sort.EndsWith("asc") ? [("720", 0), ("1080", 1), ("2160", 2)] : [("2160", 0), ("1080", 1), ("720", 2)]
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetOtherVideoMediaByTypeCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private sealed record OtherVideoOverviewFixture(
        int LibraryId,
        PlexOtherVideo SelectedVideo,
        PlexOtherVideoMediaData SelectedData,
        PlexActor Actor,
        PlexCountry Country,
        PlexGenreVideo Genre
    );

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPropagateTypedQueryFailure_WhenOtherVideoRequiresCanonicalFallback(bool snapshotMissing)
    {
        // Arrange
        var libraryId = 0;
        if (snapshotMissing)
        {
            await SetupDatabase(84210, config =>
            {
                config.PlexServerCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 1;
            });
            var dbContext = IDbContext;
            libraryId = await dbContext.PlexOtherVideos.Select(x => x.PlexLibraryId).SingleAsync(CancellationToken);
            (await dbContext.MediaOverviewOtherVideoSnapshots.CountAsync(CancellationToken)).ShouldBe(0);
        }
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.OtherVideos,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = 1,
                PageSize = 25,
                Sort = snapshotMissing ? "Title:asc" : "Title:asc,Year:desc",
            },
        };
        var expected = Result.Fail<PagedMediaQueryResult>("Canonical query failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<GetOtherVideoMediaByTypeCommand>(c => c.Filter == filter), CancellationToken))
            .ReturnsAsync(expected)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewOtherVideoCommand(filter));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBeSameAs(expected.Errors[0]);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        if (!snapshotMissing)
            Mock.Mock<IReaparrDbContextFactory>().Verify(x => x.CreateAsync(), Times.Never());
    }

    [Test]
    public void ShouldOrderRebuiltQualityRanks_ByQualityThenMediaId()
    {
        var snapshots = new[]
        {
            (Id: 3, Quality: VideoQuality.FullHD),
            (Id: 2, Quality: VideoQuality.SD),
            (Id: 1, Quality: VideoQuality.FullHD),
        }
            .Select(x => new MediaOverviewOtherVideoSnapshot
            {
                PlexOtherVideoId = x.Id,
                PlexLibraryId = 1,
                VideoQuality = x.Quality,
                QualityRank = 0,
                TitleRank = 0,
                YearRank = 0,
                AddedAtRank = 0,
                UpdatedAtRank = 0,
                DurationRank = 0,
                MediaSizeRank = 0,
            })
            .ToList();
        var values = snapshots.ToDictionary(
            x => x.PlexOtherVideoId,
            _ => new MediaOverviewRankValue("title", 2000, new DateTime(2026, 1, 1), null, 0, 0)
        );

        snapshots.AssignRanks(values);

        snapshots.OrderByDescending(x => x.QualityRank).Select(x => x.PlexOtherVideoId).ShouldBe([3, 1, 2]);
    }
}
