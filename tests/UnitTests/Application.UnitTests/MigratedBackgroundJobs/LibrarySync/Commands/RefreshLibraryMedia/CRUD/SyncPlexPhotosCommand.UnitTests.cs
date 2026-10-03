namespace Reaparr.Application.UnitTests;

public class SyncPlexPhotosCommandUnitTests : BaseCommandUnitTest<SyncPlexPhotosCommand>
{
    [Test]
    [Arguments("initial")]
    [Arguments("unchanged")]
    [Arguments("update")]
    [Arguments("move")]
    [Arguments("force")]
    [Arguments("empty")]
    public async Task ShouldReconcileAlbumsAndPhotos_WithScopedOriginalsCountsAndReports(string scenario)
    {
        // Arrange
        await SetupDatabase(625202, x =>
        {
            x.PlexServerCount = 2;
            x.PlexPhotoLibraryCount = 2;
            x.PhotoAlbumCount = 2;
            x.PhotoCount = 1;
            x.PhotoClipCount = 1;
        });
        var db = IDbContext;
        var library = await db.PlexLibraries.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var albums = await db.PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id)
            .Include(x => x.Photos).ThenInclude(x => x.MediaDataList).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var photos = albums.SelectMany(x => x.Photos).OrderBy(x => x.Id).ToList();
        foreach (var album in albums)
        foreach (var photo in album.Photos)
            photo.PlexPhotoAlbum = album;
        var originalIds = photos.Select(x => x.Id).ToArray();
        var beforeData = await db.PlexPhotoData.Where(x => x.PlexLibraryId == library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexPhotoId, x.PlexApiPartId, x.Size, x.OriginalFilename }).ToListAsync(CancellationToken);
        var control = await db.PlexPhotos.Where(x => x.PlexLibraryId != library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexPhotoAlbumId, x.PlexApiRatingKey, x.PlexLibraryId, x.PlexServerId, x.MediaSize }).ToListAsync(CancellationToken);
        var controlData = await db.PlexPhotoData.Where(x => x.PlexLibraryId != library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexPhotoId, x.PlexApiPartId, x.Size }).ToListAsync(CancellationToken);
        albums.Count.ShouldBe(2);
        photos.Count.ShouldBe(4);
        beforeData.Count.ShouldBe(4);
        long addedSize = 0;
        if (scenario is "initial" or "force")
        {
            var originals = FakeData.GetPlexPhotoMediaData(new Seed(625212)).Generate(2);
            originals[0].UpdateInitProperty(nameof(BasePlexMediaData.PlexApiMediaId), photos[0].MediaDataList.Single().PlexApiMediaId);
            photos[0].MediaDataList.AddRange(originals);
            addedSize = originals.Sum(x => x.Size);
            photos[0].MediaSize += addedSize;
        }
        if (scenario == "initial")
            await db.PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(CancellationToken);
        if (scenario == "unchanged")
        {
            photos[0].MediaSize = 1;
            photos[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 1L);
        }
        if (scenario == "update")
        {
            albums.RemoveAt(1);
            photos.RemoveRange(1, photos.Count - 1);
            albums[0].UpdatedAt = albums[0].UpdatedAt?.AddDays(1) ?? DateTime.UnixEpoch;
            photos[0].UpdatedAt = photos[0].UpdatedAt?.AddDays(1) ?? DateTime.UnixEpoch;
            photos[0].Title = "Updated photo";
            photos[0].MediaSize = 123;
            photos[0].MediaDataList.Single().UpdateInitProperty(nameof(BasePlexMediaData.Size), 123L);
        }
        if (scenario == "move")
        {
            foreach (var photo in albums[0].Photos)
                photo.PlexPhotoAlbum = albums[1];
            albums.RemoveAt(0);
        }
        if (scenario == "empty")
        {
            albums.Clear();
            photos.Clear();
        }
        library.PhotoAlbums.AddRange(albums);
        library.Photos.AddRange(photos);
        var metadata = new InsertMediaMetaDataCommandResponse(library) { PhotoClipCount = scenario is "empty" or "update" ? 0 : 2 };

        // Act
        var result = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(metadata, scenario == "force"));

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var expectedReport = scenario switch
        {
            "initial" => new CrudPhotosReport { CreatedPhotoAlbums = 2, CreatedPhotos = 4 },
            "force" => new CrudPhotosReport { CreatedPhotoAlbums = 2, CreatedPhotos = 4, DeletedPhotoAlbums = 2, DeletedPhotos = 4 },
            "update" => new CrudPhotosReport { UpdatedPhotoAlbums = 1, DeletedPhotoAlbums = 1, UpdatedPhotos = 1, DeletedPhotos = 3 },
            "move" => new CrudPhotosReport { DeletedPhotoAlbums = 1, UnchangedPhotoAlbums = 1, UpdatedPhotos = 2, UnchangedPhotos = 2 },
            "empty" => new CrudPhotosReport { DeletedPhotoAlbums = 2, DeletedPhotos = 4 },
            _ => new CrudPhotosReport { UnchangedPhotoAlbums = 2, UnchangedPhotos = 4 },
        };
        result.Value.ShouldBe(expectedReport);
        var after = await db.PlexPhotos.Where(x => x.PlexLibraryId == library.Id).OrderBy(x => x.PlexApiRatingKey).ToListAsync(CancellationToken);
        after.Select(x => (x.PlexApiRatingKey, x.PlexPhotoAlbumId))
            .ShouldBe(photos.OrderBy(x => x.PlexApiRatingKey).Select(x => (x.PlexApiRatingKey, x.PlexPhotoAlbum!.Id)));
        if (scenario is "unchanged" or "move")
            after.Select(x => x.Id).Order().ShouldBe(originalIds.Order());
        if (scenario == "update")
        {
            after.Single().Id.ShouldBe(originalIds[0]);
            after.Single().Title.ShouldBe("Updated photo");
        }
        after.ShouldAllBe(x => x.PlexLibraryId == library.Id && x.PlexServerId == library.PlexServerId);
        (await db.PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id).OrderBy(x => x.PlexApiRatingKey)
            .Select(x => x.PlexApiRatingKey).ToListAsync(CancellationToken)).ShouldBe(albums.Select(x => x.PlexApiRatingKey).Order());
        var afterData = await db.PlexPhotoData.Where(x => x.PlexLibraryId == library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexPhotoId, x.PlexApiPartId, x.Size, x.OriginalFilename }).ToListAsync(CancellationToken);
        if (scenario == "unchanged")
            afterData.ShouldBe(beforeData);
        else
            afterData.Select(x => (x.PlexPhotoId, x.PlexApiPartId, x.Size, x.OriginalFilename)).OrderBy(x => x.PlexApiPartId)
                .ShouldBe(photos.SelectMany(p => p.MediaDataList.Select(d => (p.Id, d.PlexApiPartId, d.Size, d.OriginalFilename))).OrderBy(x => x.PlexApiPartId));
        var metrics = await db.PlexLibraries.SingleAsync(x => x.Id == library.Id, CancellationToken);
        metrics.PhotoAlbumCount.ShouldBe(albums.Count);
        metrics.PhotoCount.ShouldBe(photos.Count - metadata.PhotoClipCount);
        metrics.PhotoClipCount.ShouldBe(metadata.PhotoClipCount);
        metrics.MediaSize.ShouldBe(scenario switch { "empty" => 0, "update" => 123, _ => beforeData.Sum(x => x.Size) + addedSize });
        (await db.PlexPhotos.Where(x => x.PlexLibraryId != library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexPhotoAlbumId, x.PlexApiRatingKey, x.PlexLibraryId, x.PlexServerId, x.MediaSize }).ToListAsync(CancellationToken)).ShouldBe(control);
        (await db.PlexPhotoData.Where(x => x.PlexLibraryId != library.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexPhotoId, x.PlexApiPartId, x.Size }).ToListAsync(CancellationToken)).ShouldBe(controlData);
    }
}
