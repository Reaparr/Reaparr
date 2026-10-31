using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetPhotoAlbumMediaByTypeCommandUnitTests : BaseCommandUnitTest<GetPhotoAlbumMediaByTypeCommand>
{
    [Test]
    public void ShouldRejectComparisonSorting_WhenPhotoAlbumsAreRequested()
    {
        // Arrange
        var command = new GetPhotoAlbumMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.PhotoAlbum,
                PlexLibraryId = 1,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Sort = "comparisonState:asc" },
            }
        );

        // Act
        var result = new GetPhotoAlbumMediaByTypeCommandValidator().Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Select(x => x.PropertyName).ShouldBe(["Filter.Parameters.Sort"]);
    }

    [Test]
    [Arguments("sortIndex:asc", 3, 4)]
    [Arguments("Title:desc", 3, 2)]
    [Arguments("videoQuality:desc,Title:desc", 3, 2)]
    public async Task ShouldPageScopedPhotoAlbumsWithDerivedFileTotals_WhenFilteringCanonicalMedia(
        string sort,
        int firstIndex,
        int secondIndex
    )
    {
        // Arrange
        await SetupDatabase(
            73101,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexPhotoLibraryCount = 2;
                cfg.PhotoAlbumCount = 6;
                cfg.PhotoCount = 2;
                cfg.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraryIds = await dbContext
            .PlexLibraries.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetLibraryId = libraryIds[0];
        var targetAlbums = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == targetLibraryId)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        targetAlbums.Count.ShouldBe(6);
        var memberships = await dbContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbum!.PlexLibraryId == targetLibraryId)
            .OrderBy(x => x.PlexPhotoAlbumId)
            .ThenBy(x => x.PlexPhotoMediaDataId)
            .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
            .ToListAsync(CancellationToken);
        memberships.Select(x => x.PlexPhotoAlbumId).ShouldBe(targetAlbums.SelectMany(x => new[] { x.Id, x.Id, x.Id }));
        for (var index = 0; index < targetAlbums.Count; index++)
        {
            var albumId = targetAlbums[index].Id;
            var title = index == 0 ? "excluded" : $"target-{index}";
            await dbContext
                .PlexPhotoAlbums.Where(x => x.Id == albumId)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(y => y.Title, title).SetProperty(y => y.SearchTitle, title),
                    CancellationToken
                );
            var fileIds = memberships
                .Where(x => x.PlexPhotoAlbumId == albumId)
                .Select(x => x.PlexPhotoMediaDataId)
                .ToList();
            await dbContext
                .PlexPhotoMediaData.Where(x => fileIds.Contains(x.Id))
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(y => y.Size, (index + 1) * 100L)
                            .SetProperty(y => y.Duration, (int?)((index + 1) * 100)),
                    CancellationToken
                );
        }
        var controlBefore = await dbContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == libraryIds[1])
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Size,
                x.Duration,
                x.Title,
            })
            .ToListAsync(CancellationToken);
        var command = new GetPhotoAlbumMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.PhotoAlbum,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 2,
                    Filter = "SearchTitle:contains:target",
                    Sort = sort,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var response = result.Value;
        (response.QueryHash, response.Page, response.PageSize).ShouldBe((command.Filter.QueryHash, 2, 2));
        (response.TotalCount, response.MediaCount, response.MediaSize, response.TotalMediaSize).ShouldBe(
            (5, 5, 6000L, 6000L)
        );
        response
            .Items.Select(x => new
            {
                x.Id,
                x.Title,
                x.MediaSize,
                x.Duration,
                x.ChildCount,
                x.Type,
                x.PlexLibraryId,
            })
            .ShouldBe([
                new
                {
                    Id = targetAlbums[firstIndex].Id,
                    Title = $"target-{firstIndex}",
                    MediaSize = (firstIndex + 1) * 300L,
                    Duration = (firstIndex + 1) * 300,
                    ChildCount = 3,
                    Type = PlexMediaType.PhotoAlbum,
                    PlexLibraryId = targetLibraryId,
                },
                new
                {
                    Id = targetAlbums[secondIndex].Id,
                    Title = $"target-{secondIndex}",
                    MediaSize = (secondIndex + 1) * 300L,
                    Duration = (secondIndex + 1) * 300,
                    ChildCount = 3,
                    Type = PlexMediaType.PhotoAlbum,
                    PlexLibraryId = targetLibraryId,
                },
            ]);
        response.Items.Select(x => x.SortIndex).ShouldBe([3, 4]);
        response.Items.Select(x => x.ParentId).ShouldBe(new int?[] { null, null });
        response.Items.SelectMany(x => x.Qualities).ShouldBeEmpty();
        response.Qualities.ShouldBeEmpty();
        (response.MovieCount, response.TvShowCount, response.SeasonCount, response.EpisodeCount).ShouldBe((0, 0, 0, 0));
        (
            await dbContext
                .PlexPhotoMediaData.Where(x => x.PlexLibraryId == libraryIds[1])
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Size,
                    x.Duration,
                    x.Title,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(controlBefore);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldCountSharedPhotoBytesOnce_WhenSeveralSelectedAlbumsContainTheSameFile()
    {
        // Arrange
        await SetupDatabase(73102, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var albums = FakeData.GetPlexPhotoAlbums(new Seed(73103)).Generate(2);
        foreach (var album in albums)
        {
            album.PlexLibraryId = library.Id;
            album.PlexServerId = library.PlexServerId;
        }
        var files = FakeData.GetPlexPhotoMediaData(new Seed(73104)).Generate(2);
        foreach (var file in files)
        {
            file.PlexLibraryId = library.Id;
            file.PlexServerId = library.PlexServerId;
        }
        files[0].UpdateInitProperty(nameof(PlexPhotoMediaData.Size), 321L);
        files[1].UpdateInitProperty(nameof(PlexPhotoMediaData.Size), 999L);
        dbContext.PlexPhotoAlbums.AddRange(albums);
        dbContext.PlexPhotoMediaData.AddRange(files);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexPhotoAlbumMemberships.AddRange(
            albums.Select(album => new PlexPhotoAlbumMembership
            {
                PlexPhotoAlbumId = album.Id,
                PlexPhotoMediaDataId = files[0].Id,
            })
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        (
            await dbContext
                .PlexPhotoAlbumMemberships.OrderBy(x => x.PlexPhotoAlbumId)
                .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(
            albums.OrderBy(x => x.Id).Select(x => new { PlexPhotoAlbumId = x.Id, PlexPhotoMediaDataId = files[0].Id })
        );
        var request = new GetPhotoAlbumMediaByTypeCommand(
            new MediaQueryFilter
            {
                PlexLibraryId = library.Id,
                MediaType = PlexMediaType.PhotoAlbum,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Page = 1, PageSize = 100 },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.TotalCount, result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((2, 321L, 321L));
        result
            .Value.Items.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.MediaSize,
                x.ChildCount,
            })
            .ShouldBe(
                albums
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        MediaSize = 321L,
                        ChildCount = 1,
                    })
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
