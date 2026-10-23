using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewPhotoCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewPhotoCommand>
{
    [Test]
    [Arguments("sortIndex:asc")]
    [Arguments("sortIndex:desc")]
    public async Task ShouldPagePhotoAlbumsAndCountSharedFileBytesOnce_WithSnapshotNavigation(string sort)
    {
        // Arrange
        var fixture = await ArrangePhotoPageAsync();
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.PhotoAlbum,
            PlexLibraryId = fixture.LibraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = 2,
                PageSize = 1,
                Sort = sort,
                Filter = "SearchTitle:contains:a",
            },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewPhotoCommand(filter));

        // Assert
        AssertPhotoPage(result, filter, fixture, sort);
    }

    private async Task<PhotoOverviewFixture> ArrangePhotoPageAsync()
    {
        await SetupDatabase(
            84202,
            config =>
            {
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 4;
                config.PhotoCount = 2;
                config.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraryIds = await dbContext
            .PlexLibraries.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        (
            await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Type).ToListAsync(CancellationToken)
        ).ShouldBe([PlexMediaType.PhotoAlbum, PlexMediaType.PhotoAlbum]);
        var albums = await dbContext
            .PlexPhotoAlbums.Include(x => x.Memberships)
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var target = albums.Where(x => x.PlexLibraryId == libraryIds[0]).ToList();
        target.Count.ShouldBe(4);
        albums.Count(x => x.PlexLibraryId == libraryIds[1]).ShouldBe(4);
        target[2].Memberships.Count.ShouldBe(3);
        var sharedFileId = target[0].Memberships.OrderBy(x => x.PlexPhotoMediaDataId).First().PlexPhotoMediaDataId;
        var sharedMembership = new PlexPhotoAlbumMembership
        {
            PlexPhotoAlbumId = target[2].Id,
            PlexPhotoMediaDataId = sharedFileId,
        };
        dbContext.PlexPhotoAlbumMemberships.Add(sharedMembership);
        target[2].Memberships.Add(sharedMembership);
        foreach (var album in albums)
        {
            var targetIndex = target.FindIndex(x => x.Id == album.Id);
            var rank = targetIndex switch
            {
                0 => 2,
                1 => 0,
                2 => 1,
                _ => 3,
            };
            var title = targetIndex switch
            {
                0 => "alpha",
                1 => "bravo",
                2 => "charlie",
                _ => "control",
            };
            await dbContext
                .PlexPhotoAlbums.Where(x => x.Id == album.Id)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(y => y.SearchTitle, title).SetProperty(y => y.HasThumb, true),
                    CancellationToken
                );
            var fileIds = album.Memberships
                .Where(x => x != sharedMembership)
                .Select(x => x.PlexPhotoMediaDataId)
                .ToList();
            var mediaSize = targetIndex >= 0 ? (targetIndex + 1) * 100L : 9000L;
            await dbContext.PlexPhotoMediaData.Where(x => fileIds.Contains(x.Id)).ExecuteUpdateAsync(
                x => x.SetProperty(y => y.Size, mediaSize),
                CancellationToken
            );
            var albumFileIds = album.Memberships.Select(x => x.PlexPhotoMediaDataId).ToList();
            var aggregate = await dbContext.PlexPhotoMediaData.Where(x => albumFileIds.Contains(x.Id))
                .GroupBy(_ => 1)
                .Select(group => new { Duration = group.Sum(x => x.Duration ?? 0), MediaSize = group.Sum(x => x.Size) })
                .SingleAsync(CancellationToken);
            dbContext.MediaOverviewPhotoAlbumSnapshots.Add(
                new MediaOverviewPhotoAlbumSnapshot
                {
                    PlexPhotoAlbumId = album.Id,
                    PlexLibraryId = album.PlexLibraryId,
                    SearchTitle = title,
                    NavigationTitleLabel = MediaNavigationIndexBuilder.GetTitleLabel(title),
                    Duration = aggregate.Duration,
                    AddedAt = album.AddedAt,
                    UpdatedAt = album.UpdatedAt,
                    MediaSize = aggregate.MediaSize,
                    TitleRank = rank,
                    AddedAtRank = rank,
                    UpdatedAtRank = rank,
                    DurationRank = rank,
                    MediaSizeRank = rank,
                }
            );
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        (
            await dbContext.PlexPhotoAlbumMemberships
                .Where(x => x.PlexPhotoMediaDataId == sharedFileId)
                .OrderBy(x => x.PlexPhotoAlbumId)
                .Select(x => x.PlexPhotoAlbumId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(new[] { target[0].Id, target[2].Id }.OrderBy(x => x));
        return new(libraryIds[0], target[2]);
    }

    private void AssertPhotoPage(
        Result<PagedMediaQueryResult> result,
        MediaQueryFilter filter,
        PhotoOverviewFixture fixture,
        string sort
    )
    {
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.QueryHash.ShouldBe(filter.QueryHash);
        (result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.MediaCount).ShouldBe(
            (2, 1, 3, 3)
        );
        (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((1800L, 1800L));
        result.Value.Items.Select(x => x.Id).ShouldBe([fixture.SelectedAlbum.Id]);
        var item = result.Value.Items.Single();
        (item.Type, item.SortIndex, item.ChildCount, item.GrandChildCount).ShouldBe(
            (PlexMediaType.PhotoAlbum, 2, 4, 0)
        );
        (item.PlexLibraryId, item.PlexServerId, item.PlexApiRatingKey, item.HasThumb)
            .ShouldBe((fixture.LibraryId, fixture.SelectedAlbum.PlexServerId, fixture.SelectedAlbum.PlexApiRatingKey, true));
        (item.SearchTitle, item.MediaSize).ShouldBe(("charlie", 1000L));
        item.Qualities.ShouldBeEmpty();
        result.Value.Qualities.ShouldBeEmpty();
        result.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(
            sort.EndsWith("asc") ? [("B", 0), ("C", 1), ("A", 2)] : [("A", 0), ("C", 1), ("B", 2)]
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetPhotoAlbumMediaByTypeCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private sealed record PhotoOverviewFixture(int LibraryId, PlexPhotoAlbum SelectedAlbum);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPropagateTypedQueryFailure_WhenPhotoRequiresCanonicalFallback(bool snapshotMissing)
    {
        // Arrange
        var libraryId = 0;
        if (snapshotMissing)
        {
            await SetupDatabase(84209, config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
            });
            var dbContext = IDbContext;
            libraryId = await dbContext.PlexPhotoAlbums.Select(x => x.PlexLibraryId).SingleAsync(CancellationToken);
            (await dbContext.MediaOverviewPhotoAlbumSnapshots.CountAsync(CancellationToken)).ShouldBe(0);
        }
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.PhotoAlbum,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = 1,
                PageSize = 25,
                Sort = snapshotMissing ? "Title:asc" : "Title:asc,Duration:desc",
            },
        };
        var expected = Result.Fail<PagedMediaQueryResult>("Canonical query failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<GetPhotoAlbumMediaByTypeCommand>(c => c.Filter == filter), CancellationToken))
            .ReturnsAsync(expected)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewPhotoCommand(filter));

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

}
