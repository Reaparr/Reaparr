using Reaparr.PublicAPI.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class SearchTvShowCommandUnitTests : BaseUnitTest<SearchTvShowCommandHandler>
{
    public SearchTvShowCommandUnitTests()
    {
        Mock.Mock<INetworkSettings>().SetupGet(x => x.Url).Returns("http://localhost");
    }

    [Test]
    public async Task ShouldReturnPagedEpisodes_WhenNoFiltersProvided()
    {
        // Arrange
        await SetupDatabase(
            1001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 5;
            }
        );

        var offset = 1;
        var limit = 3;
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = limit,
            Offset = offset,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        var expectedEpisodeTitles = await IDbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .Include(e => e.MediaDataList)
            .OrderBy(e => e.Id)
            .Skip(offset)
            .Take(limit)
            .Select(e => e.MediaDataList.OrderBy(md => md.PlexApiPartId).Select(md => md.GetFileName).First())
            .ToListAsync(CancellationToken);

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);
        Mock.Mock<INetworkSettings>().VerifyGet(x => x.Url, Times.Exactly(result.Value.Channel.Items.Count));

        // Assert
        // Response
        result.ShouldNotBeNull();
        result.Value.Channel.ShouldNotBeNull();
        result.Value.Channel.Title.ShouldBe("Reaparr Indexer");
        result.Value.Channel.Description.ShouldBe($"TV Search results for {cmd.Query}");
        result.Value.Channel.Language.ShouldBe("en-us");
        result.Value.Channel.Category.ShouldBe("search");

        result.Value.Channel.Items.ShouldNotBeNull();
        result.Value.Channel.Items.Count.ShouldBe(expectedEpisodeTitles.Count);
        result.Value.Channel.Items.Select(i => i.Title).ToList().ShouldBe(expectedEpisodeTitles);

        // Strict per-item assertions
        foreach (var item in result.Value.Channel.Items)
        {
            item.Guid.ShouldNotBeNull();
            item.Guid.IsPermaLink.ShouldBe("false");
            item.Guid.Value.ShouldNotBe(item.Link);

            item.Enclosure.ShouldNotBeNull();
            item.Enclosure.Type.ShouldBe("application/x-bittorrent");
            item.Enclosure.Length.ShouldBe(item.Size);

            // Required attributes
            item.Attributes.Any(a => a.Name == "season").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "episode").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "type" && a.Value == "series").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "language" && a.Value == "English").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "downloadvolumefactor" && a.Value == "0.0").ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "seeders" && int.Parse(a.Value) > 0).ShouldBeTrue();
            item.Attributes.Any(a => a.Name == "peers" && int.Parse(a.Value) > 0).ShouldBeTrue();

            // URL contains expected parameters
            item.Link.ShouldContain("/indexer/download");
            item.Link.ShouldContain("Type=Episode");
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
    public async Task ShouldEmitGenreCategory_WhenActiveSearchLoadsTypedGenre()
    {
        // Arrange
        await SetupDatabase(1002, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
        });
        using (var dbContext = IDbContext)
        {
            var tvShow = await dbContext.PlexTvShows.SingleAsync(CancellationToken);
            var genre = new PlexGenre { Name = "Anime", Key = "active-anime", Type = PlexGenreType.Anime };
            dbContext.PlexGenres.Add(genre);
            await dbContext.SaveChangesAsync(CancellationToken);
            dbContext.PlexTvShowGenres.Add(new PlexTvShowGenres(genre.Id, tvShow.PlexLibraryId, tvShow.Id));
            await dbContext.SaveChangesAsync(CancellationToken);
        }
        var command = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 1,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldHaveSingleItem();
        result.Value.Channel.Items.Single().Attributes.ShouldContain(x =>
            x.Name == "category" && x.Value == ((int)TorznabCategoryId.TV_Anime).ToString()
        );
    }

    [Test]
    public async Task ShouldNotReturnEpisodes_WhenPlexServerAccessWasRevoked()
    {
        // Arrange
        await SetupDatabase(
            4612,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
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

        var expectedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexServerId != revokedServerId)
            .Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var expectedTitles = expectedEpisodes
            .SelectMany(x => x.MediaDataList.OrderBy(y => y.PlexApiPartId).Select(y => y.GetFileName))
            .ToList();
        expectedTitles.ShouldNotBeEmpty();

        var command = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.Select(x => x.Title).ToList().ShouldBe(expectedTitles);
    }

    [Test]
    public async Task ShouldNotReturnEpisodes_WhenPlexLibraryAccessWasRevoked()
    {
        // Arrange
        await SetupDatabase(
            4613,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 2;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var revokedLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.TvShow)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .LastAsync(CancellationToken);
        await dbContext
            .PlexAccountLibraries.Where(x => x.PlexLibraryId == revokedLibraryId)
            .ExecuteDeleteAsync(CancellationToken);

        var expectedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId != revokedLibraryId)
            .Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var expectedTitles = expectedEpisodes
            .SelectMany(x => x.MediaDataList.OrderBy(y => y.PlexApiPartId).Select(y => y.GetFileName))
            .ToList();
        expectedTitles.ShouldNotBeEmpty();

        var command = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
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
            4615,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var command = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnAllEpisodes_WhenSeasonProvidedWithoutEpisode()
    {
        // Arrange
        await SetupDatabase(
            5501,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var episode = await IDbContext
            .PlexTvShowEpisodes.Include(e => e.TvShowSeason)
            .Include(e => e.TvShow)
            .OrderBy(e => e.Id)
            .FirstAsync(CancellationToken);

        var seasonNumber = episode.TvShowSeason!.SeasonNumber;
        var tvdb = episode.TvShow!.Guid_TVDB!.Value;
        var expectedEpisodeCount = await IDbContext
            .PlexTvShowEpisodes.Where(e =>
                e.TvShowSeason!.SeasonNumber == seasonNumber && e.TvShow!.Guid_TVDB == tvdb
            )
            .CountAsync(CancellationToken);

        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = seasonNumber,
            Episode = 0,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = tvdb,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result.Value.Channel.Items.Count.ShouldBe(expectedEpisodeCount);
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "season" && a.Value == seasonNumber.ToString())
            )
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnOnlyRequestedSeason_WhenSeasonProvidedWithoutEpisode()
    {
        // Arrange
        await SetupDatabase(
            5511,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );

        var targetEpisode = await IDbContext
            .PlexTvShowEpisodes.Include(e => e.TvShowSeason)
            .Include(e => e.TvShow)
            .Where(e => e.TvShowSeason!.SeasonNumber == 2)
            .OrderBy(e => e.Id)
            .FirstAsync(CancellationToken);

        var targetSeasonNumber = targetEpisode.TvShowSeason!.SeasonNumber;
        var tvdb = targetEpisode.TvShow!.Guid_TVDB!.Value;
        var expectedEpisodeCount = await IDbContext
            .PlexTvShowEpisodes.Where(e =>
                e.TvShowSeason!.SeasonNumber == targetSeasonNumber && e.TvShow!.Guid_TVDB == tvdb
            )
            .CountAsync(CancellationToken);

        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = targetSeasonNumber,
            Episode = 0,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = tvdb,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.Count.ShouldBe(expectedEpisodeCount);
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "season" && a.Value == targetSeasonNumber.ToString())
            )
            .ShouldBeTrue();
        result
            .Value.Channel.Items.Any(i =>
                i.Attributes.Any(a => a.Name == "season" && a.Value != targetSeasonNumber.ToString())
            )
            .ShouldBeFalse();
    }

    [Test]
    public void ShouldFailValidation_WhenEpisodeProvidedWithoutSeason()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 1,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 12345,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Any(error => error.PropertyName == nameof(SearchTvShowCommand.Season)).ShouldBeTrue();
    }

    [Test]
    public void ShouldFailValidation_WhenSeasonProvidedWithoutExternalId()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 1,
            Episode = 0,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Any(error => error.ErrorMessage.Contains("Provide at least one of")).ShouldBeTrue();
    }

    [Test]
    public void ShouldPassValidation_WhenSeasonProvidedWithoutEpisode()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 1,
            Episode = 0,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 12345,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnSpecificEpisode_WhenFilteredByImdbSeasonAndEpisode()
    {
        // Arrange
        await SetupDatabase(
            2002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var episode = await dbContext
            .PlexTvShowEpisodes.Include(e => e.TvShowSeason)
            .Include(e => e.TvShow)
            .OrderBy(e => e.Id)
            .FirstAsync(CancellationToken);

        var seasonNumber = episode.TvShowSeason!.SeasonNumber;
        var episodeNumber = episode.EpisodeNumber;
        var imdb = episode.TvShow!.Guid_IMDB!;

        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = seasonNumber,
            Episode = episodeNumber,
            Limit = 100,
            Offset = 0,
            IMDB_ID = imdb,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        // All returned items should correspond to the selected episode
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "season" && a.Value == seasonNumber.ToString())
            )
            .ShouldBeTrue();
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "episode" && a.Value == episodeNumber.ToString())
            )
            .ShouldBeTrue();
        result.Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "imdb" && a.Value == imdb)).ShouldBeTrue();

        // Titles should equal the part file name used during mapping
        var expectedTitle = await dbContext
            .PlexTvShowEpisodeData.Where(d => d.PlexTvShowEpisodeId == episode.Id)
            .OrderBy(d => d.PlexApiPartId)
            .Select(d => d.GetFileName)
            .FirstAsync(CancellationToken);
        result.Value.Channel.Items.Select(i => i.Title).Distinct().Single().ShouldBe(expectedTitle);

        // URLs should point to the torrent download endpoint
        result
            .Value.Channel.Items.All(i => i.Link.Contains("/indexer/download", StringComparison.Ordinal))
            .ShouldBeTrue();

        // Database state (no mutations expected)
        var episodeExists = await dbContext.PlexTvShowEpisodes.AnyAsync(e => e.Id == episode.Id, CancellationToken);
        episodeExists.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnSpecificEpisode_WhenFilteredByTmdbSeasonAndEpisode()
    {
        // Arrange
        await SetupDatabase(
            2103,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var episode = await IDbContext
            .PlexTvShowEpisodes.Include(e => e.TvShowSeason)
            .Include(e => e.TvShow)
            .OrderBy(e => e.Id)
            .FirstAsync(CancellationToken);

        var seasonNumber = episode.TvShowSeason!.SeasonNumber;
        var episodeNumber = episode.EpisodeNumber;
        var tmdb = episode.TvShow!.Guid_TMDB!.Value;

        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = seasonNumber,
            Episode = episodeNumber,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = tmdb,
            TVDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "season" && a.Value == seasonNumber.ToString())
            )
            .ShouldBeTrue();
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "episode" && a.Value == episodeNumber.ToString())
            )
            .ShouldBeTrue();
        result
            .Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "tmdbid" && a.Value == tmdb.ToString()))
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnSpecificEpisode_WhenFilteredByTvdbSeasonAndEpisode()
    {
        // Arrange
        await SetupDatabase(
            2204,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 10;
                config.TvShowSeasonCount = 3;
                config.TvShowEpisodeCount = 5;
            }
        );

        var episode = await IDbContext
            .PlexTvShowEpisodes.Include(e => e.TvShowSeason)
            .Include(e => e.TvShow)
            .OrderBy(e => e.Id)
            .FirstAsync(CancellationToken);

        var seasonNumber = episode.TvShowSeason!.SeasonNumber;
        var episodeNumber = episode.EpisodeNumber;
        var tvdb = episode.TvShow!.Guid_TVDB!.Value;

        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = seasonNumber,
            Episode = episodeNumber,
            Limit = 100,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = tvdb,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "season" && a.Value == seasonNumber.ToString())
            )
            .ShouldBeTrue();
        result
            .Value.Channel.Items.All(i =>
                i.Attributes.Any(a => a.Name == "episode" && a.Value == episodeNumber.ToString())
            )
            .ShouldBeTrue();
        result
            .Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "tvdbid" && a.Value == tvdb.ToString()))
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenNoEpisodesExist()
    {
        // Arrange
        await SetupDatabase(
            3003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 0;
                config.TvShowSeasonCount = 0;
                config.TvShowEpisodeCount = 0;
            }
        );

        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 50,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldCreateMultipleItemsPerEpisode_WhenMultiPartEpisodesEnabled()
    {
        // Arrange
        await SetupDatabase(
            3106,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
                config.IncludeMultiPartEpisodes = true;
            }
        );

        var offset = 0;
        var limit = 2;
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = limit,
            Offset = offset,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        var expectedTotal = await IDbContext.PlexTvShowEpisodeData.CountAsync(CancellationToken);

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.Count.ShouldBe(limit);
        result.Value.Channel.Response.Offset.ShouldBe(offset);
        result.Value.Channel.Response.Total.ShouldBe(expectedTotal);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenQueryProvidedWithoutSeasonEpisode()
    {
        // Arrange
        await SetupDatabase(
            3207,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var cmd = new SearchTvShowCommand
        {
            Query = "anything",
            Season = 0,
            Episode = 0,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public void ShouldValidate_WhenPagingOnlyProvided()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenSeasonAndEpisodeProvidedWithoutExternalIds()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 1,
            Episode = 1,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void ShouldPassValidation_WhenSeasonEpisodeWithImdbProvided()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 1,
            Episode = 2,
            Limit = 10,
            Offset = 0,
            IMDB_ID = "imdb://tt12345",
            TMDB_ID = 0,
            TVDB_ID = 0,
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
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 0,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldPassValidation_WhenLimitExceedsPreviousMax()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 501,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenOffsetIsNegative()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 10,
            Offset = -1,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenSeasonIsNegative()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = -1,
            Episode = 0,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenEpisodeIsNegative()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = -1,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = 0,
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
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = -1,
            TVDB_ID = 0,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public void ShouldFailValidation_WhenTvdbIdIsNegative()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = 0,
            Episode = 0,
            Limit = 10,
            Offset = 0,
            IMDB_ID = string.Empty,
            TMDB_ID = 0,
            TVDB_ID = -1,
        };

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldReturnSpecificEpisode_WhenAllExternalIdsProvided()
    {
        // Arrange
        await SetupDatabase(
            3308,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var episode = await IDbContext
            .PlexTvShowEpisodes.Include(e => e.TvShowSeason)
            .Include(e => e.TvShow)
            .OrderBy(e => e.Id)
            .FirstAsync(CancellationToken);

        var seasonNumber = episode.TvShowSeason!.SeasonNumber;
        var episodeNumber = episode.EpisodeNumber;
        var imdb = episode.TvShow!.Guid_IMDB!;
        var tmdb = episode.TvShow!.Guid_TMDB!.Value;
        var tvdb = episode.TvShow!.Guid_TVDB!.Value;

        var cmd = new SearchTvShowCommand
        {
            Query = string.Empty,
            Season = seasonNumber,
            Episode = episodeNumber,
            Limit = 100,
            Offset = 0,
            IMDB_ID = imdb.Replace("tt", ""),
            TMDB_ID = tmdb,
            TVDB_ID = tvdb,
        };

        // Act
        var result = await Sut.ExecuteAsync(cmd, CancellationToken);

        // Assert
        result.Value.Channel.Items.ShouldNotBeEmpty();
        result.Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "imdb" && a.Value == imdb)).ShouldBeTrue();
        result
            .Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "tmdbid" && a.Value == tmdb.ToString()))
            .ShouldBeTrue();
        result
            .Value.Channel.Items.All(i => i.Attributes.Any(a => a.Name == "tvdbid" && a.Value == tvdb.ToString()))
            .ShouldBeTrue();
    }
}
