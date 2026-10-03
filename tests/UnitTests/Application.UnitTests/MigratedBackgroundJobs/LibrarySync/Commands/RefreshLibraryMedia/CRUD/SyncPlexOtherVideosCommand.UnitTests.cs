namespace Reaparr.Application.UnitTests;

public class SyncPlexOtherVideosCommandUnitTests : BaseCommandUnitTest<SyncPlexOtherVideosCommand>
{
    [Test]
    [Arguments("initial")]
    [Arguments("unchanged")]
    [Arguments("update")]
    [Arguments("force")]
    [Arguments("empty")]
    public async Task ShouldReconcileOnlySelectedLibrary_WithAccurateReportsAndOriginals(string scenario)
    {
        // Arrange
        await SetupDatabase(625201, x =>
        {
            x.PlexServerCount = 2;
            x.PlexOtherVideoLibraryCount = 2;
            x.OtherVideoCount = 2;
        });
        var db = IDbContext;
        var library = await db.PlexLibraries.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var videos = await db.PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id)
            .Include(x => x.MediaDataList).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var originalIds = videos.Select(x => x.Id).ToArray();
        var beforeData = await db.PlexOtherVideoData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.PlexOtherVideoId, x.PlexApiPartId, x.Size, x.OriginalFilename }).ToListAsync(CancellationToken);
        var control = await db.PlexOtherVideos.Where(x => x.PlexLibraryId != library.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexLibraryId, x.PlexServerId, x.MediaSize }).ToListAsync(CancellationToken);
        var controlData = await db.PlexOtherVideoData.Where(x => x.PlexLibraryId != library.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.PlexOtherVideoId, x.PlexApiPartId, x.Size }).ToListAsync(CancellationToken);
        videos.Count.ShouldBe(2);
        beforeData.Count.ShouldBe(2);
        long addedSize = 0;
        if (scenario is "initial" or "force")
        {
            var originals = FakeData.GetPlexOtherVideoMediaData(new Seed(625211)).Generate(2);
            originals[0].UpdateInitProperty(nameof(BasePlexMediaData.PlexApiMediaId), videos[0].MediaDataList.Single().PlexApiMediaId);
            originals[0].UpdateInitProperty(nameof(PlexOtherVideoMediaData.PartIndex), 1);
            videos[0].MediaDataList.AddRange(originals);
            addedSize = originals.Sum(x => x.Size);
            videos[0].MediaSize += addedSize;
        }
        if (scenario == "initial")
            await db.PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(CancellationToken);
        if (scenario == "unchanged")
        {
            videos[0].MediaSize = 1;
            videos[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 1L);
        }
        if (scenario == "update")
        {
            videos[0].UpdatedAt = videos[0].UpdatedAt?.AddSeconds(1) ?? new DateTime(2026, 1, 1);
            videos[0].Title = "Updated original";
            videos[0].MediaSize = 123;
            videos[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 123L);
            videos.RemoveAt(1);
        }
        if (scenario == "empty")
            videos.Clear();
        library.OtherVideos.Clear();
        library.OtherVideos.AddRange(videos);
        var expectedSize = scenario == "empty" ? 0 : scenario == "update" ? 123 : beforeData.Sum(x => x.Size) + addedSize;

        // Act
        var result = await TestHandlerExecuteAsync<CrudOtherVideosReport>(
            new SyncPlexOtherVideosCommand(new InsertMediaMetaDataCommandResponse(library), scenario == "force"));

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var expectedReport = scenario switch
        {
            "initial" => new CrudOtherVideosReport { CreatedOtherVideos = 2 },
            "force" => new CrudOtherVideosReport { CreatedOtherVideos = 2, DeletedOtherVideos = 2 },
            "update" => new CrudOtherVideosReport { UpdatedOtherVideos = 1, DeletedOtherVideos = 1 },
            "empty" => new CrudOtherVideosReport { DeletedOtherVideos = 2 },
            _ => new CrudOtherVideosReport { UnchangedOtherVideos = 2 },
        };
        result.Value.ShouldBe(expectedReport);
        var after = await db.PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey).ToListAsync(CancellationToken);
        after.Select(x => x.PlexApiRatingKey).ShouldBe(videos.Select(x => x.PlexApiRatingKey).Order());
        after.ShouldAllBe(x => x.PlexLibraryId == library.Id && x.PlexServerId == library.PlexServerId);
        if (scenario is "unchanged" or "update")
            after.Select(x => x.Id).Order().ShouldBe(originalIds.Take(videos.Count).Order());
        if (scenario == "update")
            after.Single().Title.ShouldBe("Updated original");
        var afterData = await db.PlexOtherVideoData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.PlexOtherVideoId, x.PlexApiPartId, x.Size, x.OriginalFilename }).ToListAsync(CancellationToken);
        if (scenario == "unchanged")
            afterData.ShouldBe(beforeData);
        else
            afterData.Select(x => (x.PlexOtherVideoId, x.PlexApiPartId, x.Size, x.OriginalFilename)).OrderBy(x => x.PlexApiPartId)
                .ShouldBe(videos.SelectMany(v => v.MediaDataList.Select(d => (v.Id, d.PlexApiPartId, d.Size, d.OriginalFilename))).OrderBy(x => x.PlexApiPartId));
        var metrics = await db.PlexLibraries.SingleAsync(x => x.Id == library.Id, CancellationToken);
        metrics.OtherVideoCount.ShouldBe(videos.Count);
        metrics.MediaSize.ShouldBe(expectedSize);
        (await db.PlexOtherVideos.Where(x => x.PlexLibraryId != library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexLibraryId, x.PlexServerId, x.MediaSize }).ToListAsync(CancellationToken)).ShouldBe(control);
        (await db.PlexOtherVideoData.Where(x => x.PlexLibraryId != library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexOtherVideoId, x.PlexApiPartId, x.Size }).ToListAsync(CancellationToken)).ShouldBe(controlData);
    }
}
