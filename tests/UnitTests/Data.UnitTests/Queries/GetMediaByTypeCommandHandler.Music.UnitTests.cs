using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetMediaByTypeCommandHandlerMusicUnitTests : BaseCommandUnitTest<GetMediaByTypeCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnExactScopedMusicPage_WhenFilteringAndSortingCanonicalMedia(bool sortByQuality)
    {
        // Arrange
        await SetupDatabase(
            73101,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMusicLibraryCount = 2;
                cfg.MusicArtistCount = 6;
                cfg.MusicAlbumCount = 2;
                cfg.MusicTrackCount = 3;
            }
        );

        var dbContext = IDbContext;
        var libraryIds = await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetLibraryId = libraryIds[0];
        var controlLibraryId = libraryIds[1];
        var artists = await dbContext.PlexArtists.AsTracking().ToListAsync(CancellationToken);
        var targetArtists = artists.Where(x => x.PlexLibraryId == targetLibraryId).OrderBy(x => x.Id).ToList();
        var controlArtists = artists.Where(x => x.PlexLibraryId == controlLibraryId).OrderBy(x => x.Id).ToList();
        targetArtists.Count.ShouldBe(6);
        controlArtists.Count.ShouldBe(6);
        int[] years = [1999, 2000, 2000, 2001, 2001, 2002];
        foreach (var libraryArtists in new[] { targetArtists, controlArtists })
        {
            for (var i = 0; i < libraryArtists.Count; i++)
            {
                var artist = libraryArtists[i];
                artist.Year = years[i];
                artist.Duration = (i + 1) * 100;
                artist.MediaSize = (i + 1) * 100L;
                artist.Title = $"{artist.PlexLibraryId}-root-{i}";
                artist.HasThumb = i % 2 == 0;
                artist.Quality = VideoQuality.Unknown;
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();

        var albums = await dbContext.PlexAlbums.Where(x => x.PlexLibraryId == targetLibraryId)
            .OrderBy(x => x.PlexArtistId).ThenBy(x => x.Id)
            .Select(x => new { x.PlexArtistId, x.ChildCount }).ToListAsync(CancellationToken);
        albums.Select(x => x.PlexArtistId).ShouldBe(targetArtists.SelectMany(x => new[] { x.Id, x.Id }));
        albums.Select(x => x.ChildCount).ShouldBe(Enumerable.Repeat(3, 12));

        var expectedArtists = new[] { targetArtists[4], targetArtists[3] };
        var command = new GetMediaByTypeCommand
        {
            Filter = new MediaQueryFilter
            {
                MediaType = PlexMediaType.Music,
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
        response.Items.Select(x => x.Id).ShouldBe(expectedArtists.Select(x => x.Id));
        response.Items.Select(x => x.SortIndex).ShouldBe([3, 4]);
        response.Items.Select(x => new
        {
            x.Id, x.Title, x.SearchTitle, x.Year, x.Duration, x.MediaSize,
            x.PlexApiRatingKey, x.PlexApiMetaDataKey, x.AddedAt, x.UpdatedAt,
            x.PlexLibraryId, x.PlexServerId, x.Type, x.HasThumb,
        }).ShouldBe(expectedArtists.Select(x => new
        {
            x.Id, x.Title, x.SearchTitle, x.Year, x.Duration, x.MediaSize,
            x.PlexApiRatingKey, x.PlexApiMetaDataKey, x.AddedAt, x.UpdatedAt,
            x.PlexLibraryId, x.PlexServerId, Type = PlexMediaType.Music, x.HasThumb,
        }));
        response.Items.Select(x => x.ParentId).ShouldBe(new int?[] { null, null });
        response.Items.Select(x => x.ChildCount).ShouldBe([2, 2]);
        response.Items.Select(x => x.GrandChildCount).ShouldBe([6, 6]);
        response.Items.Select(x => x.ComparisonId).ShouldBe([0, 0]);
        if (sortByQuality)
        {
            response.NavigationIndexes.Select(x => new { x.Label, x.Index }).ShouldBe([
                new { Label = "#", Index = 0 },
            ]);
        }
        else
        {
            response.NavigationIndexes.Select(x => new { x.Label, x.Index }).ShouldBe([
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
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
