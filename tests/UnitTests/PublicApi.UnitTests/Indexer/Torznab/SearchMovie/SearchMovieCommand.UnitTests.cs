using Reaparr.Application.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class SearchMovieCommandUnitTests : BaseUnitTest<SearchMovieCommandHandler>
{
    public SearchMovieCommandUnitTests()
    {
        Mock.Mock<INetworkSettings>().SetupGet(x => x.Url).Returns("http://localhost");
    }

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
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = limit,
            Offset = offset,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            Integration = new IntegrationIdentity(
                IntegrationType.Radarr,
                Guid.Parse("62655300-0000-0000-0000-000000000001")
            ),
            TorznabApiKey = "integration-key",
        };

        var movies = await IDbContext
            .PlexMovies.AsNoTracking()
            .Include(m => m.MediaDataList)
            .OrderBy(m => m.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(CancellationToken);

        var expectedMovieTitles = movies
            .SelectMany(m => m.MediaDataList.OrderBy(md => md.PlexApiPartId).Select(md => md.GetFileName))
            .ToList();

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.ShouldNotBeNull();
        result.Value.Channel.Title.ShouldBe("Reaparr Indexer");
        result.Value.Channel.Description.ShouldBe($"Movie Search results for {cmd.Query}");
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

        var expectedMovies = await dbContext
            .PlexMovies.Where(x => x.PlexServerId != revokedServerId)
            .Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var expectedTitles = expectedMovies
            .SelectMany(x => x.MediaDataList.OrderBy(y => y.PlexApiPartId).Select(y => y.GetFileName))
            .ToList();
        expectedTitles.ShouldNotBeEmpty();

        var command = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

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

        var expectedMovies = await dbContext
            .PlexMovies.Where(x => x.PlexLibraryId != revokedLibraryId)
            .Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var expectedTitles = expectedMovies
            .SelectMany(x => x.MediaDataList.OrderBy(y => y.PlexApiPartId).Select(y => y.GetFileName))
            .ToList();
        expectedTitles.ShouldNotBeEmpty();

        var command = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

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

        var command = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

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

        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 100,
            Offset = 0,
            IMDB_ID = imdb.Replace("tt", string.Empty),
            TMDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

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

        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = tmdb,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

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

        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 50,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

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
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = limit,
            Offset = offset,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

        var expectedTotal = await IDbContext.PlexMovieData.CountAsync(CancellationToken);
        var movieCount = await IDbContext.PlexMovies.CountAsync(CancellationToken);
        expectedTotal.ShouldBeGreaterThan(movieCount);

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

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

        var cmd = new SearchMovieCommand
        {
            Query = "!!!",
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public void ShouldValidate_WhenPagingOnlyProvided()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

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
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 0,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

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
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 10_000,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenPaginationWindowExceedsMaximum()
    {
        // Arrange
        var validator = new SearchMovieCommandValidator();
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 1,
            Offset = 10_000,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

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
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 10,
            Offset = -1,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
        };

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
        var cmd = new SearchMovieCommand
        {
            Query = string.Empty,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = -1,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }
}
