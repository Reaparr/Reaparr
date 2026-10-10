using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewOtherVideoCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewOtherVideoCommand>
{
    [Test]
    [Arguments("quality:asc", "Actors")]
    [Arguments("quality:desc", "Actors")]
    [Arguments("quality:asc", "Countries")]
    [Arguments("quality:desc", "Countries")]
    [Arguments("quality:asc", "Genres")]
    [Arguments("quality:desc", "Genres")]
    public async Task ShouldUsePersistedQualityRankForFilteredPage_WithExactQualitiesAndFullNavigation(string sort, string metadataField)
    {
        // Arrange
        await SetupDatabase(84203, config =>
        {
            config.PlexOtherVideoLibraryCount = 2;
            config.OtherVideoCount = 4;
        });
        var dbContext = IDbContext;
        var libraryIds = await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var videos = await dbContext.PlexOtherVideos.OrderBy(x => x.PlexLibraryId).ThenBy(x => x.Id).ToListAsync(CancellationToken);
        var target = videos.Where(x => x.PlexLibraryId == libraryIds[0]).ToList();
        target.Count.ShouldBe(4);
        videos.Count(x => x.PlexLibraryId == libraryIds[1]).ShouldBe(4);
        var selectedData = await dbContext.PlexOtherVideoData.Where(x => x.PlexOtherVideoId == target[2].Id)
            .SingleAsync(CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);
        var metadataSeed = new Seed(84204);
        var actor = FakeData.GetPlexActors(metadataSeed).Generate();
        var country = FakeData.GetPlexCountries(metadataSeed).Generate();
        var genre = FakeData.GetPlexGenres(metadataSeed).Generate();
        dbContext.PlexActors.Add(actor);
        dbContext.PlexCountries.Add(country);
        dbContext.PlexGenres.Add(genre);
        await dbContext.SaveChangesAsync(CancellationToken);
        foreach (var video in videos)
        {
            var targetIndex = target.FindIndex(x => x.Id == video.Id);
            var qualityRank = targetIndex switch { 0 => 2, 1 => 0, 2 => 1, _ => 3 };
            var quality = targetIndex switch { 0 => VideoQuality.UHD_4K, 1 => VideoQuality.HD, _ => VideoQuality.FullHD };
            var title = targetIndex switch { 0 => "alpha", 1 => "bravo", 2 => "charlie", _ => "control" };
            await dbContext.PlexOtherVideos.Where(x => x.Id == video.Id).ExecuteUpdateAsync(x => x
                .SetProperty(y => y.SearchTitle, title)
                .SetProperty(y => y.Year, targetIndex == 3 ? 1999 : 2000)
                .SetProperty(y => y.MediaSize, targetIndex >= 0 ? (targetIndex + 1) * 100L : 9000L)
                .SetProperty(y => y.Quality, quality)
                .SetProperty(y => y.HasThumb, true), CancellationToken);
            await dbContext.PlexOtherVideoData.Where(x => x.PlexOtherVideoId == video.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Quality, quality), CancellationToken);
            dbContext.MediaOverviewOtherVideoSnapshots.Add(new MediaOverviewOtherVideoSnapshot
            {
                PlexOtherVideoId = video.Id, PlexLibraryId = video.PlexLibraryId,
                TitleRank = targetIndex >= 0 ? targetIndex : 0, QualityRank = qualityRank,
                YearRank = qualityRank, AddedAtRank = qualityRank, UpdatedAtRank = qualityRank,
                DurationRank = qualityRank, MediaSizeRank = qualityRank,
            });
            if (targetIndex != 3)
            {
                dbContext.PlexOtherVideoActors.Add(new PlexOtherVideoActors(actor.Id, video.PlexLibraryId, video.Id));
                dbContext.PlexOtherVideoCountries.Add(new PlexOtherVideoCountries(country.Id, video.PlexLibraryId, video.Id));
                dbContext.PlexOtherVideoGenres.Add(new PlexOtherVideoGenres(genre.Id, video.PlexLibraryId, video.Id));
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var metadataId = metadataField switch { "Actors" => actor.Id, "Countries" => country.Id, _ => genre.Id };
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.OtherVideos, PlexLibraryId = libraryIds[0],
            FilterOfflineMedia = false, FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 2, PageSize = 1, Sort = sort, Filter = $"{metadataField}:any:Id:eq:{metadataId}" },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewOtherVideoCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.QueryHash.ShouldBe(filter.QueryHash);
        (result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.MediaCount).ShouldBe((2, 1, 3, 3));
        (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((600L, 600L));
        result.Value.Items.Select(x => x.Id).ShouldBe([target[2].Id]);
        var item = result.Value.Items.Single();
        (item.Type, item.SortIndex, item.ChildCount, item.GrandChildCount).ShouldBe((PlexMediaType.OtherVideos, 2, 0, 0));
        (item.PlexLibraryId, item.PlexServerId, item.PlexApiRatingKey, item.PlexApiMetaDataKey, item.HasThumb)
            .ShouldBe((libraryIds[0], target[2].PlexServerId, target[2].PlexApiRatingKey, target[2].PlexApiMetaDataKey, true));
        (item.SearchTitle, item.MediaSize).ShouldBe(("charlie", 300L));
        item.Qualities.Select(x => (x.DataId, x.MediaId, x.MediaDataType, x.Quality))
            .ShouldBe([(selectedData.Id, target[2].Id, PlexMediaType.OtherVideos, VideoQuality.FullHD)]);
        result.Value.Qualities.ShouldBe([VideoQuality.FullHD.ToId()]);
        result.Value.Roles.ShouldBe([actor.Id]);
        result.Value.Countries.ShouldBe([country.Id]);
        result.Value.Genres.ShouldBe([genre.Id]);
        result.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(sort.EndsWith("asc")
            ? [("720", 0), ("1080", 1), ("2160", 2)] : [("2160", 0), ("1080", 1), ("720", 2)]);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<GetMediaByTypeCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public void ShouldOrderRebuiltQualityRanks_ByQualityThenMediaId()
    {
        var snapshots = new[] { (Id: 3, Quality: VideoQuality.FullHD), (Id: 2, Quality: VideoQuality.SD), (Id: 1, Quality: VideoQuality.FullHD) }
            .Select(x => new MediaOverviewOtherVideoSnapshot
            {
                PlexOtherVideoId = x.Id,
                PlexLibraryId = 1,
                Quality = x.Quality,
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
