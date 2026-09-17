using Reaparr.Application.Contracts;
using Reaparr.PublicAPI.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class SearchMovieCommandUnitTests : BaseCommandUnitTest<SearchMovieCommand>
{
    [Test]
    public async Task ShouldReturnPagedMovies_WhenNoFiltersProvided()
    {
        // Arrange
        await SetupDatabase(
            4001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 5;
                config.IncludeMultiPartMovies = false;
            }
        );

        var offset = 1;
        var limit = 3;
        var cmd = CreateCommand(
            limit: limit,
            offset: offset,
            apiKey: "integration-key",
            integration: new IntegrationIdentity(
                IntegrationType.Radarr,
                Guid.Parse("62655300-0000-0000-0000-000000000001")
            )
        );

        var expectedMovieTitles = await IDbContext
            .PlexMovieData.OrderByDescending(x => x.PlexMovie!.AddedAt)
            .ThenBy(x => x.PlexServer!.MachineIdentifier)
            .ThenBy(x => x.PlexMovie!.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .Skip(offset)
            .Take(limit)
            .Select(x => x.GetFileName)
            .ToListAsync(CancellationToken);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.ShouldNotBeNull();
        result.Value.Channel.Title.ShouldBe("Reaparr Indexer");
        result.Value.Channel.Description.ShouldBe($"Movie Search results for {cmd.Request.Query}");
        result.Value.Channel.Language.ShouldBe("en-us");
        result.Value.Channel.Category.ShouldBe("search");

        result.Value.Channel.Items.ShouldNotBeNull();
        result.Value.Channel.Items.Count.ShouldBe(expectedMovieTitles.Count);
        result.Value.Channel.Items.Select(i => i.Title).ToList().ShouldBe(expectedMovieTitles);

        foreach (var item in result.Value.Channel.Items)
        {
            item.Guid.ShouldNotBeNull();
            item.Guid.IsPermaLink.ShouldBe("false");
            item.Guid.Value.ShouldNotBe(item.Link);

            item.Enclosure.ShouldNotBeNull();
            item.Enclosure.Type.ShouldBe("application/x-bittorrent");
            item.Enclosure.Length.ShouldBe(item.Size);

            item.Attributes.Any(a => a.Name == "type" && a.Value == "movie").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "language" && a.Value == "English").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "downloadvolumefactor" && a.Value == "0.0").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "seeders" && int.Parse(a.Value) > 0).ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "peers" && int.Parse(a.Value) > 0).ShouldBeTrue();

            item.Attributes.Any(a => a.Name == "category").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "resolution").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "source").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "videoCodec").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "audioCodec").ShouldBeTrue();

            item.Link.ShouldContain("/api/public/integrations/62655300-0000-0000-0000-000000000001/indexer/download");
            item.Link.ShouldContain("apikey=integration-key");
            item.Link.ShouldContain("Type=Movie");
            item.Link.ShouldContain("MediaId=");
            item.Link.ShouldContain("DataId=");
            item.Link.ShouldContain("PartId=");
            item.Link.ShouldContain("PlexApiPartId=");
            item.Link.ShouldContain("Quality=");
            item.Link.ShouldContain("LibraryId=");
            item.Link.ShouldContain("ServerId=");
        }
    }

    [Test]
    public async Task ShouldNotReturnMovies_WhenPlexServerAccessWasRevoked()
    {
        // Arrange
        await SetupDatabase(
            4610,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var dbContext = IDbContext;
        var revokedServerId = await dbContext
            .PlexServers.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .LastAsync(CancellationToken);

        await dbContext
            .PlexAccountServers.Where(x => x.PlexServerId == revokedServerId)
            .ExecuteDeleteAsync(CancellationToken);

        var expectedTitles = await dbContext
            .PlexMovieData.Where(x => x.PlexServerId != revokedServerId)
            .OrderByDescending(x => x.PlexMovie!.AddedAt)
            .ThenBy(x => x.PlexServer!.MachineIdentifier)
            .ThenBy(x => x.PlexMovie!.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .Select(x => x.GetFileName)
            .ToListAsync(CancellationToken);
        expectedTitles.ShouldNotBeEmpty();

        var command = CreateCommand();

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.Select(x => x.Title).ToList().ShouldBe(expectedTitles);
    }

    [Test]
    public async Task ShouldNotReturnMovies_WhenPlexLibraryAccessWasRevoked()
    {
        // Arrange
        await SetupDatabase(
            4611,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
                config.MovieCount = 2;
            }
        );

        var dbContext = IDbContext;
        var revokedLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .LastAsync(CancellationToken);

        await dbContext
            .PlexAccountLibraries.Where(x => x.PlexLibraryId == revokedLibraryId)
            .ExecuteDeleteAsync(CancellationToken);

        var expectedTitles = await dbContext
            .PlexMovieData.Where(x => x.PlexLibraryId != revokedLibraryId)
            .OrderByDescending(x => x.PlexMovie!.AddedAt)
            .ThenBy(x => x.PlexServer!.MachineIdentifier)
            .ThenBy(x => x.PlexMovie!.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .Select(x => x.GetFileName)
            .ToListAsync(CancellationToken);
        expectedTitles.ShouldNotBeEmpty();

        var command = CreateCommand();

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.Select(x => x.Title).ToList().ShouldBe(expectedTitles);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenNoPlexAccountsExist()
    {
        // Arrange
        await SetupDatabase(
            4614,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var command = CreateCommand();

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnSpecificMovie_WhenFilteredByImdbId()
    {
        // Arrange
        await SetupDatabase(
            4102,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var dbContext = IDbContext;
        var movie = await dbContext
            .PlexMovies.Include(m => m.MediaDataList)
            .OrderBy(m => m.Id)
            .FirstAsync(m => !string.IsNullOrEmpty(m.Guid_IMDB), CancellationToken);

        var imdb = movie.Guid_IMDB!;

        var cmd = CreateCommand(imdbId: imdb.Replace("tt", string.Empty));

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result.Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "imdb" && a.Value == imdb)).ShouldBeTrue();

        var expectedTitles = movie.MediaDataList.OrderBy(md => md.PlexApiPartId).Select(md => md.GetFileName).ToList();
        result.Value.Channel.Items.Select(i => i.Title).ToList().ShouldBe(expectedTitles);
        result
            .Value.Channel.Items.All(i => i.Link.Contains("/indexer/download", StringComparison.Ordinal))
            .ShouldBeTrue();

        var movieExists = await dbContext.PlexMovies.AnyAsync(m => m.Id == movie.Id, CancellationToken);
        movieExists.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnSpecificMovie_WhenFilteredByTmdbId()
    {
        // Arrange
        await SetupDatabase(
            4203,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var movie = await IDbContext
            .PlexMovies.Include(m => m.MediaDataList)
            .OrderBy(m => m.Id)
            .FirstAsync(m => m.Guid_TMDB.HasValue, CancellationToken);

        var tmdb = movie.Guid_TMDB!.Value;

        var cmd = CreateCommand(tmdbId: tmdb);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result
            .Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "tmdbid" && a.Value == tmdb.ToString()))
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenNoMoviesExist()
    {
        // Arrange
        await SetupDatabase(
            4304,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 0;
            }
        );

        var cmd = CreateCommand(limit: 50);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldCreateMultipleItemsPerMovie_WhenMultiPartMoviesEnabled()
    {
        // Arrange
        await SetupDatabase(
            4405,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.IncludeMultiPartMovies = true;
            }
        );

        var offset = 0;
        var limit = 2;
        var cmd = CreateCommand(limit: limit, offset: offset);

        var expectedTotal = await IDbContext.PlexMovieData.CountAsync(CancellationToken);
        var movieCount = await IDbContext.PlexMovies.CountAsync(CancellationToken);
        expectedTotal.ShouldBeGreaterThan(movieCount);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.Count.ShouldBe(limit);
        result.Value.Channel.Response.Offset.ShouldBe(offset);
        result.Value.Channel.Response.Total.ShouldBe(expectedTotal);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenQueryNormalizesToEmpty()
    {
        // Arrange
        await SetupDatabase(
            4506,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var cmd = CreateCommand(query: "!!!", limit: 10);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnExactMovie_WhenNormalizedTitlePrefixMatches()
    {
        // Arrange
        await SetupDatabase(
            4510,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = false;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext
            .PlexMovies.Where(x => x.Id == movies[0].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(movie => movie.SearchTitle, "The Matching Movie".ToSearchTitle()),
                CancellationToken
            );
        await dbContext
            .PlexMovies.Where(x => x.Id == movies[1].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(movie => movie.SearchTitle, "Different Movie".ToSearchTitle()),
                CancellationToken
            );
        var expectedTitles = await dbContext
            .PlexMovieData.Where(x => x.PlexMovieId == movies[0].Id)
            .OrderBy(x => x.Id)
            .Select(x => x.GetFileName)
            .ToListAsync(CancellationToken);
        var command = CreateCommand(query: "THE MATCHING");

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(expectedTitles.Count);
        result.Value.Channel.Items.Select(x => x.Title).ShouldBe(expectedTitles);
    }

    [Test]
    [Arguments(TorznabCategoryId.Movies, 0, 2)]
    [Arguments(TorznabCategoryId.Movies_UHD, 0, 1)]
    [Arguments(TorznabCategoryId.Movies_UHD, 9_999, 1)]
    [Arguments((TorznabCategoryId)9_999, 0, 0)]
    public async Task ShouldApplyMovieCategorySemantics(
        TorznabCategoryId firstCategory,
        int secondCategory,
        int expectedCount
    )
    {
        // Arrange
        await SetupDatabase(
            4511 + secondCategory + (int)firstCategory,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = false;
            }
        );
        var dbContext = IDbContext;
        var rows = await dbContext.PlexMovieData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext
            .PlexMovieData.Where(x => x.Id == rows[0].Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(data => data.VideoResolution, VideoQuality.UHD_4K)
                        .SetProperty(data => data.GeneratedFilename, "uhd-movie.mkv"),
                CancellationToken
            );
        await dbContext
            .PlexMovieData.Where(x => x.Id == rows[1].Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(data => data.VideoResolution, VideoQuality.SD)
                        .SetProperty(data => data.GeneratedFilename, "sd-movie.mkv"),
                CancellationToken
            );
        var categories =
            secondCategory == 0 ? new[] { (int)firstCategory } : new[] { (int)firstCategory, secondCategory };
        var command = CreateCommand(categories: categories);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(expectedCount);
        var expectedTitles = expectedCount switch
        {
            0 => Array.Empty<string>(),
            1 => ["uhd-movie.mkv"],
            _ => ["uhd-movie.mkv", "sd-movie.mkv"],
        };
        result.Value.Channel.Items.Select(x => x.Title).Order().ShouldBe(expectedTitles.Order());
    }

    [Test]
    public async Task ShouldReturnSpecificMovie_WhenImdbIdAlreadyHasPrefix()
    {
        // Arrange
        await SetupDatabase(
            4507,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var movie = await IDbContext
            .PlexMovies.OrderBy(x => x.Id)
            .FirstAsync(x => x.Guid_IMDB != null, CancellationToken);
        var command = CreateCommand(imdbId: movie.Guid_IMDB);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result.Value.Channel.Items.ShouldAllBe(x =>
            x.Attributes.Any(attribute => attribute.Name == "imdb" && attribute.Value == movie.Guid_IMDB)
        );
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenImdbIdIsMalformed()
    {
        // Arrange
        await SetupDatabase(
            4508,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        var command = CreateCommand(imdbId: "imdb://tt123");

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenMovieExternalIdsConflict()
    {
        // Arrange
        await SetupDatabase(
            4509,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var movies = await IDbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await IDbContext
            .PlexMovies.Where(x => x.Id == movies[0].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(movie => movie.Guid_IMDB, "tt111111").SetProperty(movie => movie.Guid_TMDB, 111111),
                CancellationToken
            );
        await IDbContext
            .PlexMovies.Where(x => x.Id == movies[1].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(movie => movie.Guid_IMDB, "tt222222").SetProperty(movie => movie.Guid_TMDB, 222222),
                CancellationToken
            );
        var command = CreateCommand(imdbId: "tt111111", tmdbId: 222222);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public void ShouldValidate_WhenPagingOnlyProvided()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = CreateCommand(limit: 10);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldPassValidation_WhenLimitIsZero()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = CreateCommand(limit: 0);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldPassValidation_WhenLimitIsAtMaximum()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = CreateCommand(limit: TorznabSearchHelpers.MaxPageSize);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenLimitExceedsMaximum()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var command = CreateCommand(limit: TorznabSearchHelpers.MaxPageSize + 1);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
    }

    [Test]
    public void ShouldFailValidation_WhenPaginationWindowExceedsMaximum()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = CreateCommand(limit: 1, offset: 10_000);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenOffsetIsNegative()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = CreateCommand(limit: 10, offset: -1);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenTmdbIdIsNegative()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = CreateCommand(tmdbId: -1);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    private async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteCommandAsync(SearchMovieCommand command)
    {
        var networkSettings = Mock.Mock<INetworkSettings>();
        networkSettings.SetupGet(x => x.Url).Returns("http://localhost").Verifiable(Times.AtMostOnce());

        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        networkSettings.Verify();
        return result;
    }
    private static SearchMovieCommand CreateCommand(
        int limit = 100,
        int offset = 0,
        string query = "",
        string? imdbId = null,
        int tmdbId = 0,
        int[]? categories = null,
        string apiKey = "",
        IntegrationIdentity? integration = null
    ) =>
        new(
            new TorznabRequest
            {
                Type = TorznabQueryType.Movie,
                Query = query,
                Season = 0,
                Episode = 0,
                TvdbId = 0,
                ImdbId = imdbId ?? string.Empty,
                TmdbId = tmdbId,
                ApiKey = apiKey,
                Limit = limit,
                Offset = offset,
                Categories = categories ?? [],
                Attributes = [],
                Integration = integration ?? new IntegrationIdentity(IntegrationType.Radarr, Guid.Empty),
            }
        );

}
