using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetMusicArtistMediaByTypeCommandUnitTests : BaseCommandUnitTest<GetMusicArtistMediaByTypeCommand>
{

    [Test]
    [Arguments(false, "Actors")]
    [Arguments(true, "Actors")]
    [Arguments(false, "Countries")]
    [Arguments(true, "Countries")]
    [Arguments(false, "Genres")]
    [Arguments(true, "Genres")]
    public async Task ShouldReturnExactScopedMusicPage_WhenFilteringAndSortingCanonicalMedia(
        bool sortByQuality,
        string metadataField
    )
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
        var libraryIds = await dbContext
            .PlexLibraries.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetLibraryId = libraryIds[0];
        var controlLibraryId = libraryIds[1];
        var artists = await dbContext.PlexMusicArtists.AsTracking().ToListAsync(CancellationToken);
        var targetArtists = artists.Where(x => x.PlexLibraryId == targetLibraryId).OrderBy(x => x.Id).ToList();
        var controlArtists = artists.Where(x => x.PlexLibraryId == controlLibraryId).OrderBy(x => x.Id).ToList();
        targetArtists.Count.ShouldBe(6);
        controlArtists.Count.ShouldBe(6);
        var metadataSeed = new Seed(73102);
        var actor = FakeData.GetPlexActors(metadataSeed).Generate();
        var country = FakeData.GetPlexCountries(metadataSeed).Generate();
        var genre = FakeData.GetPlexGenresAudio(metadataSeed).Generate();
        dbContext.PlexActors.Add(actor);
        dbContext.PlexCountries.Add(country);
        dbContext.PlexGenresAudio.Add(genre);
        foreach (var libraryArtists in new[] { targetArtists, controlArtists })
        {
            for (var i = 0; i < libraryArtists.Count; i++)
            {
                var artist = libraryArtists[i];
                artist.Duration = (i + 1) * 100;
                artist.MediaSize = (i + 1) * 100L;
                artist.Title = $"{artist.PlexLibraryId}-root-{i}";
                artist.HasThumb = i % 2 == 0;
                if (i > 0)
                {
                    artist.Actors.Add(actor);
                    artist.Countries.Add(country);
                    artist.Genres.Add(genre);
                }
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();

        var albums = await dbContext
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == targetLibraryId)
            .OrderBy(x => x.PlexArtistId)
            .ThenBy(x => x.Id)
            .Select(x => new { x.PlexArtistId, x.ChildCount })
            .ToListAsync(CancellationToken);
        albums.Select(x => x.PlexArtistId).ShouldBe(targetArtists.SelectMany(x => new[] { x.Id, x.Id }));
        albums.Select(x => x.ChildCount).ShouldBe(Enumerable.Repeat(3, 12));

        var expectedArtists = new[] { targetArtists[3], targetArtists[2] };
        var metadataId = metadataField switch
        {
            "Actors" => actor.Id,
            "Countries" => country.Id,
            _ => genre.Id,
        };
        var command = new GetMusicArtistMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.MusicArtist,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 2,
                    Filter = $"{metadataField}:any:Id:eq:{metadataId}",
                    Sort = sortByQuality ? "videoQuality:desc,Duration:desc" : "Duration:desc",
                },
            }
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(c =>
                        c.MediaType == PlexMediaType.MusicArtist
                        && c.PlexLibraryId == targetLibraryId
                        && c.Items.Select(item => item.Id).SequenceEqual(expectedArtists.Select(item => item.Id))
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

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
                expectedArtists.Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.SearchTitle,
                    Year = 0,
                    x.Duration,
                    x.MediaSize,
                    x.PlexApiRatingKey,
                    x.PlexApiMetaDataKey,
                    x.AddedAt,
                    x.UpdatedAt,
                    x.PlexLibraryId,
                    x.PlexServerId,
                    Type = PlexMediaType.MusicArtist,
                    x.HasThumb,
                })
            );
        response.Items.Select(x => x.ParentId).ShouldBe(new int?[] { null, null });
        response.Items.Select(x => x.ChildCount).ShouldBe([2, 2]);
        response.Items.Select(x => x.GrandChildCount).ShouldBe([6, 6]);
        response.Items.Select(x => x.ComparisonId).ShouldBe([0, 0]);
        response
            .NavigationIndexes.Select(x => new { x.Label, x.Index })
            .ShouldBe([
                new { Label = "10–20 min", Index = 0 },
                new { Label = "0–10 min", Index = 1 },
            ]);
        response.Items.SelectMany(x => x.Qualities).ShouldBeEmpty();
        response.Qualities.ShouldBeEmpty();
        response.Roles.ShouldBe([actor.Id]);
        response.Countries.ShouldBe([country.Id]);
        response.Genres.ShouldBe([genre.Id]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(null, null)]
    [Arguments("Title:asc", PlexMediaComparisonState.Owned)]
    [Arguments("comparisonState:desc,Title:asc", null)]
    public async Task ShouldProjectMusicArtistQualitiesInPersistedIdOrderAcrossQueryBranches(
        string? sort,
        PlexMediaComparisonState? comparisonState
    )
    {
        // Arrange
        await SetupDatabase(73104, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 3;
        });
        var dbContext = IDbContext;
        var artist = await dbContext.PlexMusicArtists.SingleAsync(CancellationToken);
        dbContext.PlexMusicArtistMediaQualities.AddRange(
            new PlexMusicArtistMediaQuality { PlexLibraryId = artist.PlexLibraryId, PlexMusicArtistId = artist.Id, AudioQuality = AudioQuality.MP3_320KbpsPlus },
            new PlexMusicArtistMediaQuality { PlexLibraryId = artist.PlexLibraryId, PlexMusicArtistId = artist.Id, AudioQuality = AudioQuality.Unknown }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var persisted = await dbContext.PlexMusicArtistMediaQualities.OrderBy(x => x.Id)
            .Select(x => x.AudioQuality).ToListAsync(CancellationToken);
        persisted.ShouldBe([AudioQuality.MP3_320KbpsPlus, AudioQuality.Unknown]);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.MusicArtist,
            PlexLibraryId = artist.PlexLibraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            ComparisonState = comparisonState,
            Parameters = new FlexQueryParameters { Page = 1, PageSize = 2, Sort = sort },
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<ApplyComparisonStateCommand>(c =>
                c.MediaType == PlexMediaType.MusicArtist && c.PlexLibraryId == artist.PlexLibraryId
                && c.Items.Count == 1 && c.Items[0].Id == artist.Id), CancellationToken))
            .Callback<ICommand<Result>, CancellationToken>((command, _) =>
                ((ApplyComparisonStateCommand)command).Items[0].SetComparisonState(PlexMediaComparisonState.Owned))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMusicArtistMediaByTypeCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(1);
        var item = result.Value.Items.Single();
        (item.Id, item.Type, item.ParentId, item.ChildCount, item.GrandChildCount)
            .ShouldBe((artist.Id, PlexMediaType.MusicArtist, (int?)null, 2, 6));
        item.Qualities.Select(x => (x.AudioQuality, x.VideoQuality, x.DataId, x.MediaId, x.MediaDataType)).ShouldBe([
            (AudioQuality.MP3_320KbpsPlus, VideoQuality.None, -1, artist.Id, PlexMediaType.MusicArtist),
            (AudioQuality.Unknown, VideoQuality.None, -1, artist.Id, PlexMediaType.MusicArtist),
        ]);
        result.Value.Qualities.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldIgnoreVideoQualitySort_WhenOnlyVideoQualitySortIsRequestedForMusic()
    {
        // Arrange
        await SetupDatabase(73103, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 3;
            config.MusicAlbumCount = 0;
        });
        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        var artists = await dbContext.PlexMusicArtists.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        int[] sortIndexes = [30, 10, 20];
        for (var index = 0; index < artists.Count; index++)
            dbContext.Entry(artists[index]).Property(nameof(BasePlexMedia.SortIndex)).CurrentValue = sortIndexes[index];
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedIds = new[] { artists[1].Id, artists[2].Id };
        var command = new GetMusicArtistMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.MusicArtist,
                PlexLibraryId = libraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Page = 1, PageSize = 2, Sort = "videoQuality:desc" },
            }
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(
                It.Is<ApplyComparisonStateCommand>(state =>
                    state.MediaType == PlexMediaType.MusicArtist && state.PlexLibraryId == libraryId
                ),
                CancellationToken
            ))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(3);
        result.Value.Items.Select(x => x.Id).ShouldBe(expectedIds);
        result.Value.Items.Select(x => x.SortIndex).ShouldBe([1, 2]);
    }

    [Test]
    public async Task ShouldFilterBeforePagingAndCountOnlyMatches_WhenMusicComparisonStateIsRequested()
    {
        // Arrange
        await SetupDatabase(
            87501,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 4;
            }
        );
        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        var artists = await dbContext.PlexMusicArtists.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        artists.Count.ShouldBe(4);
        for (var i = 0; i < artists.Count; i++)
        {
            artists[i].Duration = (i + 1) * 100;
            artists[i].MediaSize = (i + 1) * 100L;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.MusicArtist,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            ComparisonState = PlexMediaComparisonState.Partial,
            Parameters = new FlexQueryParameters
            {
                Page = 2,
                PageSize = 1,
                Sort = "Duration:asc,comparisonState:desc",
            },
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(c =>
                        c.MediaType == PlexMediaType.MusicArtist
                        && c.PlexLibraryId == libraryId
                        && c.Items.Count == artists.Count
                        && c.Items.Select(item => item.Id).ToHashSet().SetEquals(artists.Select(item => item.Id))
                    ),
                    CancellationToken
                )
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (command, _) =>
                {
                    foreach (var item in ((ApplyComparisonStateCommand)command).Items)
                        item.SetComparisonState(
                            item.Id == artists[1].Id || item.Id == artists[3].Id
                                ? PlexMediaComparisonState.Partial
                                : PlexMediaComparisonState.Owned
                        );
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(
            new GetMusicArtistMediaByTypeCommand(filter)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.TotalCount, result.Value.MediaCount, result.Value.Page, result.Value.PageSize).ShouldBe(
            (2, 2, 2, 1)
        );
        (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((600L, 600L));
        result
            .Value.Items.Select(x => (x.Id, x.SortIndex, x.ComparisonId))
            .ShouldBe(new[] { (artists[3].Id, 2, PlexMediaComparisonState.Partial.ToComparisonId()) });
        result.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(new[] { ("0–10 min", 0) });
        result.Value.Qualities.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(PlexMediaComparisonState.NotCompared, true)]
    [Arguments(PlexMediaComparisonState.Pending, true)]
    [Arguments(PlexMediaComparisonState.Owned, true)]
    [Arguments(PlexMediaComparisonState.Missing, true)]
    [Arguments(PlexMediaComparisonState.Partial, true)]
    [Arguments(PlexMediaComparisonState.HigherQuality, false)]
    [Arguments(PlexMediaComparisonState.PartialAndHigherQuality, false)]
    public void ShouldRejectOnlyUnsupportedMusicStates_WhenValidatingCanonicalLookup(
        PlexMediaComparisonState state,
        bool valid
    )
    {
        // Arrange
        var command = new GetMusicArtistMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.MusicArtist,
                PlexLibraryId = 1,
                ComparisonState = state,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters(),
            }
        );
 
        // Act
        var result = new GetMusicArtistMediaByTypeCommandValidator().Validate(command);

        // Assert
        result.IsValid.ShouldBe(valid);
        result.Errors.Select(x => x.PropertyName).ShouldBe(valid ? [] : new[] { "Filter.ComparisonState" });
    }
}
