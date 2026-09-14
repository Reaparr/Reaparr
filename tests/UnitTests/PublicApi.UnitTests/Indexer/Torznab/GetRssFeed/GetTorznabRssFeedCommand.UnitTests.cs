using Reaparr.Application.Contracts;
using Reaparr.PublicAPI.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class GetTorznabRssFeedCommandUnitTests : BaseCommandUnitTest<GetTorznabRssFeedCommand>
{
    public GetTorznabRssFeedCommandUnitTests()
    {
        Mock.Mock<INetworkSettings>().SetupGet(x => x.Url).Returns("http://localhost");
    }

    [Test]
    public async Task ShouldPageMoviesByCanonicalOrder_WhenCatalogIsUnchanged()
    {
        // Arrange
        await SetupDatabase(
            7631,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 5;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var expected = await IDbContext
            .PlexMovieData.OrderByDescending(x => x.PlexMovie!.AddedAt)
            .ThenByDescending(x => x.PlexMovie!.PlexServerId)
            .ThenByDescending(x => x.PlexMovie!.PlexApiRatingKey)
            .Select(x => !string.IsNullOrEmpty(x.GeneratedFilename) ? x.GeneratedFilename : x.OriginalFilename)
            .ToListAsync(CancellationToken);
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 3,
            Offset = 1,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Response.Offset.ShouldBe(1);
        result.Value.Channel.Response.Total.ShouldBe(command.Offset + command.Limit + 1);
        result.Value.Channel.Items.Select(x => x.Title).ShouldBe(expected.Skip(1).Take(3));
        result.Value.Channel.Items.Select(x => x.Guid.Value).Distinct().Count().ShouldBe(3);
        result.Value.Channel.Items.All(x => x.Guid.Value != x.Link).ShouldBeTrue();
        result.Value.Channel.Items.All(x => x.Attributes.Any(a => a.Name == "size")).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnSameGuidAndPublicationDate_WhenFeedIsReadAgain()
    {
        // Arrange
        await SetupDatabase(
            7632,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
                config.SonarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken)).Id.ToSonarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [5000],
            IncludeMovies = false,
            IncludeEpisodes = true,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var first = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);
        var second = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        first
            .Value.Channel.Items.Select(x => (x.Guid.Value, x.PubDate))
            .ShouldBe(second.Value.Channel.Items.Select(x => (x.Guid.Value, x.PubDate)));
        first.Value.Channel.Items.All(x => x.Attributes.Any(a => a.Name == "category")).ShouldBeTrue();
        first.Value.Channel.Items.All(x => x.Attributes.Any(a => a.Name == "season")).ShouldBeTrue();
        first.Value.Channel.Items.All(x => x.Attributes.Any(a => a.Name == "episode")).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnEmptyFeed_WhenOnlyUnknownCategoriesAreRequested()
    {
        // Arrange
        await SetupDatabase(
            7633,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [9999],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldGloballyPageMoviesAndEpisodes_WhenAddedAtTies()
    {
        // Arrange
        await SetupDatabase(
            7634,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
                config.RadarrIntegrationCount = 1;
            }
        );
        var tiedAddedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await IDbContext.PlexMovies.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.AddedAt, tiedAddedAt),
            CancellationToken
        );
        await IDbContext.PlexTvShowEpisodes.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.AddedAt, tiedAddedAt),
            CancellationToken
        );
        var movieRows = await IDbContext.PlexMovieData
            .Select(x => new
            {
                x.PlexMovie!.AddedAt,
                x.PlexMovie.PlexServerId,
                x.PlexMovie.PlexApiRatingKey,
                Title = x.GetFileName,
            })
            .ToListAsync(CancellationToken);
        var episodeRows = await IDbContext.PlexTvShowEpisodeData
            .Select(x => new
            {
                x.PlexTvShowEpisode!.AddedAt,
                x.PlexTvShowEpisode.PlexServerId,
                x.PlexTvShowEpisode.PlexApiRatingKey,
                Title = x.GetFileName,
            })
            .ToListAsync(CancellationToken);
        var expectedTitles = movieRows.Concat(episodeRows)
            .OrderByDescending(x => x.AddedAt)
            .ThenByDescending(x => x.PlexServerId)
            .ThenByDescending(x => x.PlexApiRatingKey)
            .Skip(1)
            .Take(3)
            .Select(x => x.Title)
            .ToList();
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = true,
            Limit = 3,
            Offset = 1,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Response.Total.ShouldBe(4);
        result.Value.Channel.Response.Offset.ShouldBe(1);
        result.Value.Channel.Items.Select(x => x.Title).ShouldBe(expectedTitles);
    }

    [Test]
    public void ShouldAcceptMaximumPaginationWindow()
    {
        // Arrange
        var validator = new GetTorznabRssFeedCommandValidator();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()),
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 10_000,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldRejectPaginationWindowBeyondMaximum()
    {
        // Arrange
        var validator = new GetTorznabRssFeedCommandValidator();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()),
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 1,
            Offset = 10_000,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldUseKnownCategory_WhenKnownAndUnknownCategoriesAreRequested()
    {
        // Arrange
        await SetupDatabase(
            7635,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var category = GetMovieQualityCategory(await IDbContext.PlexMovieData.FirstAsync(CancellationToken));
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [category, 9999],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result
            .Value.Channel.Items.All(x => x.Attributes.Any(a => a.Name == "category" && a.Value == category.ToString()))
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldNotDuplicateItems_WhenParentAndChildCategoriesAreRequested()
    {
        // Arrange
        await SetupDatabase(
            7636,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var totalParts = await IDbContext.PlexMovieData.CountAsync(CancellationToken);
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [(int)TorznabCategoryId.Movies, (int)TorznabCategoryId.Movies_HD],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Response.Total.ShouldBe(totalParts);
        result.Value.Channel.Items.Select(x => x.Guid.Value).Distinct().Count().ShouldBe(totalParts);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenMovieSearchUsesOnlyTvCategories()
    {
        // Arrange
        await SetupDatabase(
            7637,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [(int)TorznabCategoryId.TV],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnLookaheadTotal_WhenMoreFilteredItemsExist()
    {
        // Arrange
        await SetupDatabase(
            7638,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 4;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 1,
            Offset = 1,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Response.Total.ShouldBe(command.Offset + command.Limit + 1);
        result.Value.Channel.Items.ShouldHaveSingleItem();
    }

    [Test]
    public async Task ShouldReturnEmptyPage_WhenOffsetExceedsFilteredTotal()
    {
        // Arrange
        await SetupDatabase(
            7640,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 4;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var category = GetMovieQualityCategory(await IDbContext.PlexMovieData.FirstAsync(CancellationToken));
        var expectedTotal = (await IDbContext.PlexMovieData.ToListAsync(CancellationToken)).Count(x =>
            GetMovieQualityCategory(x) == category
        );
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [category],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 1,
            Offset = expectedTotal + 1,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldBeEmpty();
        result.Value.Channel.Response.Total.ShouldBe(expectedTotal);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenTvSearchUsesOnlyMovieCategories()
    {
        // Arrange
        await SetupDatabase(
            7639,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
                config.SonarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken)).Id.ToSonarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [(int)TorznabCategoryId.Movies],
            IncludeMovies = false,
            IncludeEpisodes = true,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnOnlyRequestedAttributes_WhenExtendedIsDisabled()
    {
        // Arrange
        await SetupDatabase(
            7640,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = ["category", "SIZE", "unknown"],
            IncludeAllAttributes = false,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result
            .Value.Channel.Items.All(x =>
                x.Attributes.All(a => a.Name is "category" or "size")
                && x.Attributes.Any(a => a.Name == "category")
                && x.Attributes.Count(a => a.Name == "size") == 1
            )
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnAllAttributes_WhenExtendedIsEnabled()
    {
        // Arrange
        await SetupDatabase(
            7641,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.RadarrIntegrationCount = 1;
            }
        );
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = ["category"],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var names = result.Value.Channel.Items.Single().Attributes.Select(x => x.Name).ToList();
        names.ShouldBe([
            "size", "category", "category", "category", "seeders", "peers", "type", "language",
            "downloadvolumefactor", "uploadvolumefactor", "resolution", "source", "videoCodec", "audioCodec",
            "tmdbid", "imdb",
        ]);
    }

    [Test]
    public async Task ShouldHideUnavailableMedia_AndRestoreSameReleaseAfterRecovery()
    {
        await SetupDatabase(7642, ConfigureMovieFeed);
        var command = await CreateMovieFeedCommand();
        var beforeOutage = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);
        var expected = beforeOutage.Value.Channel.Items.Select(x => (x.Guid.Value, x.PubDate)).ToList();

        using (var dbContext = IDbContext)
        {
            await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsSuccessful, false),
                CancellationToken
            );
        }

        var duringOutage = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        using (var dbContext = IDbContext)
        {
            await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsSuccessful, true),
                CancellationToken
            );
        }

        var afterRecovery = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        expected.ShouldNotBeEmpty();
        duringOutage.Value.Channel.Items.ShouldBeEmpty();
        afterRecovery.Value.Channel.Items.Select(x => (x.Guid.Value, x.PubDate)).ShouldBe(expected);
    }

    [Test]
    public async Task ShouldHideMedia_WhenDownloadsArePaused()
    {
        await SetupDatabase(7643, ConfigureMovieFeed);
        var command = await CreateMovieFeedCommand();
        using (var dbContext = IDbContext)
        {
            await dbContext.PlexServers.ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsDownloadsPausedByUser, true),
                CancellationToken
            );
        }

        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldHideMedia_WhenServerAccessIsRemoved()
    {
        await SetupDatabase(7644, ConfigureMovieFeed);
        var command = await CreateMovieFeedCommand();
        using (var dbContext = IDbContext)
            await dbContext.PlexAccountServers.ExecuteDeleteAsync(CancellationToken);

        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldHideMedia_WhenLibraryAccessIsRemoved()
    {
        await SetupDatabase(7645, ConfigureMovieFeed);
        var command = await CreateMovieFeedCommand();
        using (var dbContext = IDbContext)
            await dbContext.PlexAccountLibraries.ExecuteDeleteAsync(CancellationToken);

        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldNotTrackOrModifyEntities_WhenFeedIsRead()
    {
        await SetupDatabase(7646, ConfigureMovieFeed);
        var command = await CreateMovieFeedCommand();
        using var dbContext = IDbContext;
        dbContext.ClearChangeTracker();
        var mediaIdsBefore = await dbContext.PlexMovieData.Select(x => x.Id).ToListAsync(CancellationToken);
        var handler = new GetTorznabRssFeedCommandHandler(dbContext, Mock.Mock<INetworkSettings>().Object);

        var result = await handler.ExecuteAsync(command, CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        (await dbContext.SaveChangesAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.PlexMovieData.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(mediaIdsBefore);
    }

    [Test]
    [Arguments(PlexGenreType.Anime, TorznabCategoryId.TV_Anime)]
    [Arguments(PlexGenreType.Documentary, TorznabCategoryId.TV_Documentary)]
    [Arguments(PlexGenreType.Sport, TorznabCategoryId.TV_Sport)]
    public async Task ShouldFilterAndEmitTvGenreCategory_WhenGenreTypeMatches(
        PlexGenreType genreType,
        TorznabCategoryId category
    )
    {
        // Arrange
        await SetupDatabase(7647, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
            config.SonarrIntegrationCount = 1;
        });
        using (var dbContext = IDbContext)
        {
            var tvShow = await dbContext.PlexTvShows.SingleAsync(CancellationToken);
            var genre = new PlexGenre { Name = genreType.ToString(), Key = $"torznab-{genreType}", Type = genreType };
            dbContext.PlexGenres.Add(genre);
            await dbContext.SaveChangesAsync(CancellationToken);
            dbContext.PlexTvShowGenres.Add(new PlexTvShowGenres(genre.Id, tvShow.PlexLibraryId, tvShow.Id));
            await dbContext.SaveChangesAsync(CancellationToken);
        }
        var integration = (await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken)).Id.ToSonarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [(int)category],
            IncludeMovies = false,
            IncludeEpisodes = true,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldHaveSingleItem();
        result.Value.Channel.Items.Single().Attributes.ShouldContain(x =>
            x.Name == "category" && x.Value == ((int)category).ToString()
        );
    }

    [Test]
    public async Task ShouldNotEmitSpecificTvGenreCategory_WhenGenreTypeIsGroup()
    {
        // Arrange
        await SetupDatabase(7648, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
            config.SonarrIntegrationCount = 1;
        });
        using (var dbContext = IDbContext)
        {
            var tvShow = await dbContext.PlexTvShows.SingleAsync(CancellationToken);
            var genre = new PlexGenre { Name = "Sport / Documentary", Key = "torznab-group", Type = PlexGenreType.Group };
            dbContext.PlexGenres.Add(genre);
            await dbContext.SaveChangesAsync(CancellationToken);
            dbContext.PlexTvShowGenres.Add(new PlexTvShowGenres(genre.Id, tvShow.PlexLibraryId, tvShow.Id));
            await dbContext.SaveChangesAsync(CancellationToken);
        }
        var integration = (await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken)).Id.ToSonarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [(int)TorznabCategoryId.TV_Sport],
            IncludeMovies = false,
            IncludeEpisodes = true,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldKeepMovieAndEpisodeGenresSeparate_WhenMediaIdsOverlap()
    {
        // Arrange
        await SetupDatabase(7649, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        using (var dbContext = IDbContext)
        {
            var movie = await dbContext.PlexMovies.SingleAsync(CancellationToken);
            var tvShow = await dbContext.PlexTvShows.SingleAsync(CancellationToken);
            var foreign = new PlexGenre { Name = "Foreign", Key = "mixed-foreign", Type = PlexGenreType.Foreign };
            var anime = new PlexGenre { Name = "Anime", Key = "mixed-anime", Type = PlexGenreType.Anime };
            dbContext.PlexGenres.AddRange(foreign, anime);
            await dbContext.SaveChangesAsync(CancellationToken);
            dbContext.PlexMovieGenres.Add(new PlexMovieGenres(foreign.Id, movie.PlexLibraryId, movie.Id));
            dbContext.PlexTvShowGenres.Add(new PlexTvShowGenres(anime.Id, tvShow.PlexLibraryId, tvShow.Id));
            await dbContext.SaveChangesAsync(CancellationToken);
        }
        var integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        var command = new GetTorznabRssFeedCommand
        {
            Integration = integration,
            Categories = [],
            IncludeMovies = true,
            IncludeEpisodes = true,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var movieItem = result.Value.Channel.Items.Single(x => x.Attributes.Any(a => a.Name == "type" && a.Value == "movie"));
        var episodeItem = result.Value.Channel.Items.Single(x => x.Attributes.Any(a => a.Name == "type" && a.Value == "series"));
        movieItem.Attributes.ShouldContain(x => x.Name == "category" && x.Value == ((int)TorznabCategoryId.Movies_Foreign).ToString());
        movieItem.Attributes.ShouldNotContain(x => x.Name == "category" && x.Value == ((int)TorznabCategoryId.TV_Anime).ToString());
        episodeItem.Attributes.ShouldContain(x => x.Name == "category" && x.Value == ((int)TorznabCategoryId.TV_Anime).ToString());
        episodeItem.Attributes.ShouldNotContain(x => x.Name == "category" && x.Value == ((int)TorznabCategoryId.Movies_Foreign).ToString());
    }

    private static int GetMovieQualityCategory(PlexMovieMediaData mediaData)
    {
        if (mediaData.Source == ReleaseSource.DVD || mediaData.VideoResolution is VideoQuality.SD or VideoQuality.DVD)
            return (int)TorznabCategoryId.Movies_SD;

        return mediaData.VideoResolution is VideoQuality.UHD_4K or VideoQuality.UHD_8K
            ? (int)TorznabCategoryId.Movies_UHD
            : (int)TorznabCategoryId.Movies_HD;
    }

    [Test]
    public async Task ShouldHideDisabledServer_AndRestoreSameReleaseIdentityWhenEnabled()
    {
        // Arrange
        await SetupDatabase(7650, ConfigureMovieFeed);
        var command = await CreateMovieFeedCommand();
        var first = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);
        var expected = first.Value.Channel.Items.Select(x => x.Guid.Value).ToList();
        using (var dbContext = IDbContext)
        {
            await dbContext.PlexServers.IgnoreQueryFilters().ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsEnabled, false),
                CancellationToken
            );
            ((DbContext)dbContext).ChangeTracker.Clear();
        }

        // Act
        var disabled = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);
        using (var dbContext = IDbContext)
        {
            await dbContext.PlexServers.IgnoreQueryFilters().ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsEnabled, true),
                CancellationToken
            );
            ((DbContext)dbContext).ChangeTracker.Clear();
        }
        var restored = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        expected.ShouldNotBeEmpty();
        disabled.Value.Channel.Items.ShouldBeEmpty();
        restored.Value.Channel.Items.Select(x => x.Guid.Value).ShouldBe(expected);
    }

    [Test]
    public async Task ShouldDenyAccess_WhenServerAndLibraryAccessBelongToDifferentAccounts()
    {
        // Arrange
        await SetupDatabase(7651, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        using (var dbContext = IDbContext)
        {
            var accounts = await dbContext.PlexAccounts.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
            await dbContext.PlexAccountServers.Where(x => x.PlexAccountId == accounts[1]).ExecuteDeleteAsync(CancellationToken);
            await dbContext.PlexAccountLibraries.Where(x => x.PlexAccountId == accounts[0]).ExecuteDeleteAsync(CancellationToken);
        }
        var command = await CreateMovieFeedCommand();

        // Act
        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    private static void ConfigureMovieFeed(FakeDataConfig config)
    {
        config.PlexServerCount = 1;
        config.PlexAccountCount = 1;
        config.PlexMovieLibraryCount = 1;
        config.MovieCount = 2;
        config.RadarrIntegrationCount = 1;
    }

    private async Task<GetTorznabRssFeedCommand> CreateMovieFeedCommand() =>
        new()
        {
            Integration = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity(),
            Categories = [(int)TorznabCategoryId.Movies],
            IncludeMovies = true,
            IncludeEpisodes = false,
            Limit = 50,
            Offset = 0,
            TorznabApiKey = "rss-key",
            Attributes = [],
            IncludeAllAttributes = true,
        };
}
