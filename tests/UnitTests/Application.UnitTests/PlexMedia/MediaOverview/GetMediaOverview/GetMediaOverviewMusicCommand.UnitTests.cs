using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewMusicCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewMusicCommand>
{
    [Test]
    [Arguments("sortIndex:asc", "Actors")]
    [Arguments("sortIndex:desc", "Actors")]
    [Arguments("sortIndex:asc", "Countries")]
    [Arguments("sortIndex:desc", "Countries")]
    [Arguments("sortIndex:asc", "Genres")]
    [Arguments("sortIndex:desc", "Genres")]
    public async Task ShouldReturnFilteredArtistPageInSnapshotOrder_WithFullNavigationAndHierarchy(
        string sort,
        string metadataField
    )
    {
        // Arrange
        var fixture = await ArrangeFilteredArtistPageAsync();
        var metadataId = metadataField switch
        {
            "Actors" => fixture.Actor.Id,
            "Countries" => fixture.Country.Id,
            _ => fixture.Genre.Id,
        };
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.MusicArtist,
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
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(c =>
                        c.MediaType == PlexMediaType.MusicArtist
                        && c.PlexLibraryId == fixture.LibraryId
                        && c.Items.Count == 1
                        && c.Items[0].Id == fixture.SelectedArtist.Id
                    ),
                    CancellationToken
                )
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (command, _) =>
                    ((ApplyComparisonStateCommand)command).Items[0].SetComparisonState(PlexMediaComparisonState.Partial)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMusicCommand(filter));

        // Assert
        AssertFilteredArtistPage(result, filter, fixture, sort);
    }

    private async Task<MusicOverviewFixture> ArrangeFilteredArtistPageAsync()
    {
        await SetupDatabase(
            84201,
            config =>
            {
                config.PlexMusicLibraryCount = 2;
                config.MusicArtistCount = 4;
                config.MusicAlbumCount = 2;
                config.MusicTrackCount = 3;
            }
        );
        var dbContext = IDbContext;
        var libraryIds = await dbContext
            .PlexLibraries.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var artists = await dbContext
            .PlexMusicArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var target = artists.Where(x => x.PlexLibraryId == libraryIds[0]).ToList();
        target.Count.ShouldBe(4);
        artists.Count(x => x.PlexLibraryId == libraryIds[1]).ShouldBe(4);
        target[2].Albums.Count.ShouldBe(2);
        target[2].Albums.Select(x => x.Tracks.Count).ShouldBe([3, 3]);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);

        var metadataSeed = new Seed(84202);
        var actor = FakeData.GetPlexActors(metadataSeed).Generate();
        var country = FakeData.GetPlexCountries(metadataSeed).Generate();
        var genre = FakeData.GetPlexGenresAudio(metadataSeed).Generate();
        dbContext.PlexActors.Add(actor);
        dbContext.PlexCountries.Add(country);
        dbContext.PlexGenresAudio.Add(genre);
        await dbContext.SaveChangesAsync(CancellationToken);
        foreach (var artist in artists)
        {
            var targetIndex = target.FindIndex(x => x.Id == artist.Id);
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
            var mediaSize = targetIndex >= 0 ? (targetIndex + 1) * 100L : 9000L;
            await dbContext
                .PlexMusicArtists.Where(x => x.Id == artist.Id)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(y => y.SearchTitle, title)
                            .SetProperty(y => y.MediaSize, mediaSize)
                            .SetProperty(y => y.HasThumb, true),
                    CancellationToken
                );
            dbContext.MediaOverviewMusicArtistSnapshots.Add(
                new MediaOverviewMusicArtistSnapshot
                {
                    PlexArtistId = artist.Id,
                    PlexLibraryId = artist.PlexLibraryId,
                    SearchTitle = title,
                    NavigationTitleLabel = MediaNavigationIndexBuilder.GetTitleLabel(title),
                    Duration = artist.Duration,
                    AddedAt = artist.AddedAt,
                    UpdatedAt = artist.UpdatedAt,
                    MediaSize = mediaSize,
                    TitleRank = rank,
                    AddedAtRank = rank,
                    UpdatedAtRank = rank,
                    DurationRank = rank,
                    MediaSizeRank = rank,
                }
            );
            if (targetIndex != 3)
            {
                dbContext.PlexMusicArtistActors.Add(new PlexMusicArtistActors(actor.Id, artist.Id));
                dbContext.PlexMusicArtistCountries.Add(new PlexMusicArtistCountries(country.Id, artist.Id));
                dbContext.PlexMusicArtistGenres.Add(new PlexMusicArtistGenres(genre.Id, artist.Id));
            }
        }
        dbContext.PlexMusicArtistMediaQualities.AddRange(
            new PlexMusicArtistMediaQuality
            {
                PlexLibraryId = libraryIds[0],
                PlexMusicArtistId = target[2].Id,
                AudioQuality = AudioQuality.MP3_320KbpsPlus,
            },
            new PlexMusicArtistMediaQuality
            {
                PlexLibraryId = libraryIds[0],
                PlexMusicArtistId = target[2].Id,
                AudioQuality = AudioQuality.Unknown,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        return new(libraryIds[0], target[2], actor, country, genre);
    }

    private void AssertFilteredArtistPage(
        Result<PagedMediaQueryResult> result,
        MediaQueryFilter filter,
        MusicOverviewFixture fixture,
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
        result.Value.Items.Select(x => x.Id).ShouldBe([fixture.SelectedArtist.Id]);
        var item = result.Value.Items.Single();
        (item.Type, item.SortIndex, item.ChildCount, item.GrandChildCount).ShouldBe(
            (PlexMediaType.MusicArtist, 2, 2, 6)
        );
        item.ComparisonId.ShouldBe(PlexMediaComparisonState.Partial.ToComparisonId());
        (item.PlexLibraryId, item.PlexServerId, item.PlexApiRatingKey, item.PlexApiMetaDataKey, item.HasThumb).ShouldBe(
            (
                fixture.LibraryId,
                fixture.SelectedArtist.PlexServerId,
                fixture.SelectedArtist.PlexApiRatingKey,
                fixture.SelectedArtist.PlexApiMetaDataKey,
                true
            )
        );
        (item.SearchTitle, item.MediaSize).ShouldBe(("charlie", 300L));
        item.Qualities.Select(x => x.AudioQuality).ShouldBe([AudioQuality.MP3_320KbpsPlus, AudioQuality.Unknown]);
        item.Qualities.ShouldAllBe(x =>
            x.MediaDataType == PlexMediaType.MusicArtist
            && x.MediaId == fixture.SelectedArtist.Id
            && x.DataId == -1
            && x.VideoQuality == VideoQuality.None
        );
        result.Value.Qualities.ShouldBeEmpty();
        result.Value.Roles.ShouldBe([fixture.Actor.Id]);
        result.Value.Countries.ShouldBe([fixture.Country.Id]);
        result.Value.Genres.ShouldBe([fixture.Genre.Id]);
        result
            .Value.NavigationIndexes.Select(x => (x.Label, x.Index))
            .ShouldBe(sort.EndsWith("asc") ? [("B", 0), ("C", 1), ("A", 2)] : [("A", 0), ("C", 1), ("B", 2)]);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetMusicArtistMediaByTypeCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
    }

    private sealed record MusicOverviewFixture(
        int LibraryId,
        PlexMusicArtist SelectedArtist,
        PlexActor Actor,
        PlexCountry Country,
        PlexGenreAudio Genre
    );

    [Test]
    [Arguments("sortIndex:asc")]
    [Arguments("sortIndex:desc")]
    public async Task ShouldReturnFirstFilteredNavigationOrdinalAcrossLibraries_WhenLabelsRepeatAndRanksHaveGaps(
        string sort
    )
    {
        // Arrange
        var fixture = await ArrangeCrossLibraryNavigationFixtureAsync();
        fixture.ArtistCount.ShouldBe(8);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.MusicArtist,
            PlexLibraryId = 0,
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
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(command =>
                        command.MediaType == PlexMediaType.MusicArtist
                        && command.PlexLibraryId == null
                        && command.Items.Count == 1
                        && command.Items[0].Id == fixture.ExpectedPageId
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(
            new GetMediaOverviewMusicCommand(filter)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Items.Select(x => x.Id).ShouldBe([fixture.ExpectedPageId]);
        (result.Value.TotalCount, result.Value.MediaCount).ShouldBe((3, 3));
        (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((1400L, 1400L));
        result.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe([("A", 0), ("B", 1)]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    private async Task<CrossLibraryNavigationFixture> ArrangeCrossLibraryNavigationFixtureAsync()
    {
        await SetupDatabase(
            84205,
            config =>
            {
                config.PlexMusicLibraryCount = 2;
                config.MusicArtistCount = 4;
            }
        );
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsEnabled, true),
            CancellationToken
        );
        var artists = await dbContext
            .PlexMusicArtists.OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var snapshots = await SeedCrossLibraryArtistSnapshotsAsync(dbContext, artists);
        dbContext.MediaOverviewMusicArtistSnapshots.AddRange(snapshots);
        await dbContext.SaveChangesAsync(CancellationToken);
        return new(artists[4].Id, artists.Count);
    }

    private async Task<List<MediaOverviewMusicArtistSnapshot>> SeedCrossLibraryArtistSnapshotsAsync(
        IReaparrDbContext dbContext,
        IReadOnlyList<PlexMusicArtist> artists
    )
    {
        var selectedTitles = new Dictionary<int, string>
        {
            [artists[1].Id] = "alpha",
            [artists[4].Id] = "beta",
            [artists[6].Id] = "atlas",
        };
        var snapshots = new List<MediaOverviewMusicArtistSnapshot>(artists.Count);
        for (var index = 0; index < artists.Count; index++)
        {
            var artist = artists[index];
            var title = selectedTitles.GetValueOrDefault(artist.Id, "xyz");
            var mediaSize = (index + 1) * 100L;
            await dbContext
                .PlexMusicArtists.Where(x => x.Id == artist.Id)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(y => y.SearchTitle, title).SetProperty(y => y.MediaSize, mediaSize),
                    CancellationToken
                );
            snapshots.Add(CreateNavigationSnapshot(artist, title, mediaSize, index));
        }
        return snapshots;
    }

    private static MediaOverviewMusicArtistSnapshot CreateNavigationSnapshot(
        PlexMusicArtist artist,
        string title,
        long mediaSize,
        int rank
    ) =>
        new()
        {
            PlexArtistId = artist.Id,
            PlexLibraryId = artist.PlexLibraryId,
            SearchTitle = title,
            NavigationTitleLabel = MediaNavigationIndexBuilder.GetTitleLabel(title),
            Duration = artist.Duration,
            AddedAt = artist.AddedAt,
            UpdatedAt = artist.UpdatedAt,
            MediaSize = mediaSize,
            TitleRank = rank,
            AddedAtRank = rank,
            UpdatedAtRank = rank,
            DurationRank = rank,
            MediaSizeRank = rank,
        };

    private sealed record CrossLibraryNavigationFixture(int ExpectedPageId, int ArtistCount);

    [Test]
    [Arguments(PlexMediaComparisonState.NotCompared)]
    [Arguments(PlexMediaComparisonState.Pending)]
    [Arguments(PlexMediaComparisonState.Owned)]
    [Arguments(PlexMediaComparisonState.Missing)]
    [Arguments(PlexMediaComparisonState.Partial)]
    public async Task ShouldUseCanonicalFallback_WhenMusicComparisonFilterIsRequested(PlexMediaComparisonState state)
    {
        // Arrange
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.MusicArtist,
            PlexLibraryId = 17,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            ComparisonState = state,
            Parameters = new FlexQueryParameters
            {
                Page = 2,
                PageSize = 3,
                Sort = "Title:desc",
                Filter = "SearchTitle:contains:artist",
            },
        };
        var page = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = 2,
            PageSize = 3,
            TotalCount = 4,
            MediaCount = 4,
            MediaSize = 1234,
            TotalMediaSize = 1234,
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<GetMusicArtistMediaByTypeCommand>(c => c.Filter == filter), CancellationToken))
            .ReturnsAsync(Result.Ok(page))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMusicCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeSameAs(page);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
    [Test]
    public async Task ShouldUseCanonicalFallback_WhenAudioQualitySortIsRequestedAndSnapshotExists()
    {
        // Arrange
        var libraryId = await ArrangeAudioQualitySnapshotAsync();
        (await IDbContext.MediaOverviewMusicArtistSnapshots.CountAsync(CancellationToken)).ShouldBe(1);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.MusicArtist,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 1, PageSize = 25, Sort = "audioQuality:desc" },
        };
        var expected = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = 1,
            PageSize = 25,
            TotalCount = 1,
            MediaCount = 1,
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetMusicArtistMediaByTypeCommand>(command => command.Filter == filter),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(expected))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(
            new GetMediaOverviewMusicCommand(filter)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeSameAs(expected);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPropagateTypedQueryFailure_WhenArtistSnapshotIsMissing()
    {
        // Arrange
        await SetupDatabase(84208, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
        });
        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexMusicArtists.Select(x => x.PlexLibraryId).SingleAsync(CancellationToken);
        (await dbContext.MediaOverviewMusicArtistSnapshots.CountAsync(CancellationToken)).ShouldBe(0);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.MusicArtist,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 1, PageSize = 25, Sort = "audioQuality:desc" },
        };
        var expected = Result.Fail<PagedMediaQueryResult>("Canonical query failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<GetMusicArtistMediaByTypeCommand>(c => c.Filter == filter), CancellationToken))
            .ReturnsAsync(expected)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMusicCommand(filter));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBeSameAs(expected.Errors[0]);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments("videoQuality:asc")]
    [Arguments("year:desc")]
    public void ShouldRejectUnsupportedSortFields_WhenValidatingMusicOverviewCommand(string sort)
    {
        // Arrange
        var command = new GetMediaOverviewMusicCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.MusicArtist,
                PlexLibraryId = 1,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Sort = sort },
            }
        );

        // Act
        var result = new GetMediaOverviewMusicCommandValidator().Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Select(error => error.PropertyName).ShouldBe(["Filter.Parameters.Sort"]);
    }

    private async Task<int> ArrangeAudioQualitySnapshotAsync()
    {
        await SetupDatabase(
            84207,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
            }
        );
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);
        var artist = await dbContext.PlexMusicArtists.SingleAsync(CancellationToken);
        dbContext.MediaOverviewMusicArtistSnapshots.Add(
            CreateNavigationSnapshot(artist, artist.SearchTitle, artist.MediaSize, rank: 0)
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        return artist.PlexLibraryId;
    }

}
