using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetMediaByTypeCommandHandlerPhotoAlbumUnitTests : BaseCommandUnitTest<GetMediaByTypeCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnExactScopedPhotoAlbumPage_WhenFilteringAndSortingCanonicalMedia(bool sortByQuality)
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
        (await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Type).ToListAsync(CancellationToken)).ShouldBe([
            PlexMediaType.PhotoAlbum,
            PlexMediaType.PhotoAlbum,
        ]);
        var targetLibraryId = libraryIds[0];
        var controlLibraryId = libraryIds[1];
        var albums = await dbContext.PlexPhotoAlbums.AsTracking().ToListAsync(CancellationToken);
        var targetAlbums = albums.Where(x => x.PlexLibraryId == targetLibraryId).OrderBy(x => x.Id).ToList();
        var controlAlbums = albums.Where(x => x.PlexLibraryId == controlLibraryId).OrderBy(x => x.Id).ToList();
        targetAlbums.Count.ShouldBe(6);
        controlAlbums.Count.ShouldBe(6);
        int[] years = [1999, 2000, 2000, 2001, 2001, 2002];
        foreach (var libraryAlbums in new[] { targetAlbums, controlAlbums })
        {
            for (var i = 0; i < libraryAlbums.Count; i++)
            {
                var album = libraryAlbums[i];
                album.Year = years[i];
                album.Duration = (i + 1) * 100;
                album.MediaSize = (i + 1) * 100L;
                album.Title = $"{album.PlexLibraryId}-root-{i}";
                album.HasThumb = i % 2 == 0;
                album.Quality = VideoQuality.Unknown;
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();

        var photoParentIds = await dbContext
            .PlexPhotoImages.Where(x => x.PlexLibraryId == targetLibraryId)
            .OrderBy(x => x.PlexPhotoAlbumId)
            .ThenBy(x => x.Id)
            .Select(x => x.PlexPhotoAlbumId)
            .ToListAsync(CancellationToken);
        photoParentIds.ShouldBe(targetAlbums.SelectMany(x => new[] { x.Id, x.Id, x.Id }));

        var expectedAlbums = new[] { targetAlbums[4], targetAlbums[3] };
        var command = new GetMediaByTypeCommand
        {
            Filter = new MediaQueryFilter
            {
                MediaType = PlexMediaType.PhotoAlbum,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 2,
                    Filter = "Year:gte:2000",
                    Sort = sortByQuality ? "quality:desc,Year:asc,Duration:desc" : "Year:asc,Duration:desc",
                },
            },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var response = result.Value;
        response.QueryHash.ShouldBe(command.Filter.QueryHash);
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(2);
        response.TotalCount.ShouldBe(5);
        response.MediaCount.ShouldBe(5);
        response.MediaSize.ShouldBe(2000);
        response.TotalMediaSize.ShouldBe(2000);
        response.MovieCount.ShouldBe(0);
        response.TvShowCount.ShouldBe(0);
        response.SeasonCount.ShouldBe(0);
        response.EpisodeCount.ShouldBe(0);
        response.TotalMovieCount.ShouldBe(0);
        response.TotalTvShowCount.ShouldBe(0);
        response.TotalSeasonCount.ShouldBe(0);
        response.TotalEpisodeCount.ShouldBe(0);
        response.Items.Select(x => x.Id).ShouldBe(expectedAlbums.Select(x => x.Id));
        response.Items.Select(x => x.SortIndex).ShouldBe([3, 4]);
        response
            .Items.Select(x => new
            {
                x.Id,
                x.Title,
                x.SearchTitle,
                x.Year,
                x.Duration,
                x.MediaSize,
                x.PlexApiRatingKey,
                x.PlexApiMetaDataKey,
                x.AddedAt,
                x.UpdatedAt,
                x.PlexLibraryId,
                x.PlexServerId,
                x.Type,
                x.HasThumb,
            })
            .ShouldBe(
                expectedAlbums.Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.SearchTitle,
                    x.Year,
                    x.Duration,
                    x.MediaSize,
                    x.PlexApiRatingKey,
                    x.PlexApiMetaDataKey,
                    x.AddedAt,
                    x.UpdatedAt,
                    x.PlexLibraryId,
                    x.PlexServerId,
                    Type = PlexMediaType.PhotoAlbum,
                    x.HasThumb,
                })
            );
        response.Items.Select(x => x.ParentId).ShouldBe(new int?[] { null, null });
        response.Items.Select(x => x.ChildCount).ShouldBe([3, 3]);
        response.Items.Select(x => x.GrandChildCount).ShouldBe([0, 0]);
        response.Items.Select(x => x.ComparisonId).ShouldBe([0, 0]);
        if (sortByQuality)
        {
            response.NavigationIndexes.Select(x => new { x.Label, x.Index }).ShouldBe([new { Label = "#", Index = 0 }]);
        }
        else
        {
            response
                .NavigationIndexes.Select(x => new { x.Label, x.Index })
                .ShouldBe([
                    new { Label = "2000", Index = 0 },
                    new { Label = "2001", Index = 2 },
                    new { Label = "2002", Index = 4 },
                ]);
        }
        response.Items.SelectMany(x => x.Qualities).ShouldBeEmpty();
        response.Qualities.ShouldBeEmpty();
        response.Roles.ShouldBeEmpty();
        response.Countries.ShouldBeEmpty();
        response.Genres.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
