using Reaparr.PublicAPI.Contracts;
using Reaparr.Application.Contracts;

using Reaparr.Settings.Contracts;
namespace Reaparr.PublicAPI.UnitTests;

public class SearchTvShowCommandUnitTests : BaseCommandUnitTest<SearchTvShowCommand>
{
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
        var cmd = CreateCommand(limit: limit, offset: offset);

        var expectedEpisodeTitles = await IDbContext
            .PlexTvShowEpisodeData.OrderByDescending(x => x.PlexTvShowEpisode!.AddedAt)
            .ThenBy(x => x.PlexServer!.MachineIdentifier)
            .ThenBy(x => x.PlexTvShowEpisode!.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .Skip(offset)
            .Take(limit)
            .Select(x => x.GetFileName)
            .ToListAsync(CancellationToken);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        // Response
        result.ShouldNotBeNull();
        result.Value.Channel.ShouldNotBeNull();
        result.Value.Channel.Title.ShouldBe("Reaparr Indexer");
        result.Value.Channel.Description.ShouldBe($"TV Search results for {cmd.Request.Query}");
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
    public async Task ShouldPageByPublicationDate_WhenNewestEpisodeFallsAfterIdentifierPrefix()
    {
        // Arrange
        await SetupDatabase(
            1003,
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

        var dbContext = IDbContext;
        var episodes = await dbContext.PlexTvShowEpisodes.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var newestEpisode = episodes[^1];
        await dbContext
            .PlexTvShowEpisodes.Where(x => x.Id == episodes[0].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.AddedAt, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                CancellationToken
            );
        await dbContext
            .PlexTvShowEpisodes.Where(x => x.Id == episodes[1].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.AddedAt, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
                CancellationToken
            );
        await dbContext
            .PlexTvShowEpisodes.Where(x => x.Id == newestEpisode.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.AddedAt, new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc)),
                CancellationToken
            );

        var newestTitle = await dbContext
            .PlexTvShowEpisodeData.Where(x => x.PlexTvShowEpisodeId == newestEpisode.Id)
            .OrderBy(x => x.PlexApiPartId)
            .Select(x => x.GetFileName)
            .FirstAsync(CancellationToken);
        var command = CreateCommand(limit: 2);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.Count.ShouldBe(2);
        result.Value.Channel.Items[0].Title.ShouldBe(newestTitle);
    }

    [Test]
    public async Task ShouldEmitGenreCategory_WhenActiveSearchLoadsTypedGenre()
    {
        // Arrange
        await SetupDatabase(
            1002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        using (var dbContext = IDbContext)
        {
            var tvShow = await dbContext.PlexTvShows.SingleAsync(CancellationToken);
            var genre = new PlexGenre
            {
                Name = "Anime",
                Key = "active-anime",
                Type = PlexGenreType.Anime,
            };
            dbContext.PlexGenres.Add(genre);
            await dbContext.SaveChangesAsync(CancellationToken);
            dbContext.PlexTvShowGenres.Add(new PlexTvShowGenres(genre.Id, tvShow.PlexLibraryId, tvShow.Id));
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        var command = CreateCommand(limit: 1);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.ShouldHaveSingleItem();
        result
            .Value.Channel.Items.Single()
            .Attributes.ShouldContain(x =>
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

        var expectedTitles = await dbContext
            .PlexTvShowEpisodeData.Where(x => x.PlexServerId != revokedServerId)
            .OrderByDescending(x => x.PlexTvShowEpisode!.AddedAt)
            .ThenBy(x => x.PlexServer!.MachineIdentifier)
            .ThenBy(x => x.PlexTvShowEpisode!.PlexApiRatingKey)
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

        var expectedTitles = await dbContext
            .PlexTvShowEpisodeData.Where(x => x.PlexLibraryId != revokedLibraryId)
            .OrderByDescending(x => x.PlexTvShowEpisode!.AddedAt)
            .ThenBy(x => x.PlexServer!.MachineIdentifier)
            .ThenBy(x => x.PlexTvShowEpisode!.PlexApiRatingKey)
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

        var command = CreateCommand();

        // Act
        var result = await ExecuteCommandAsync(command);

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
            .PlexTvShowEpisodes.Where(e => e.TvShowSeason!.SeasonNumber == seasonNumber && e.TvShow!.Guid_TVDB == tvdb)
            .CountAsync(CancellationToken);

        var cmd = CreateCommand(season: seasonNumber, tvdbId: tvdb);

        // Act
        var result = await ExecuteCommandAsync(cmd);

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

        var cmd = CreateCommand(season: targetSeasonNumber, tvdbId: tvdb);

        // Act
        var result = await ExecuteCommandAsync(cmd);

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
        var cmd = CreateCommand(episode: 1, tvdbId: 12345);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Any(error => error.PropertyName.EndsWith(nameof(TorznabRequest.Season))).ShouldBeTrue();
    }

    [Test]
    public void ShouldFailValidation_WhenSeasonProvidedWithoutExternalId()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = CreateCommand(season: 1);

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
        var cmd = CreateCommand(season: 1, tvdbId: 12345);

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

        var cmd = CreateCommand(season: seasonNumber, episode: episodeNumber, imdbId: imdb);

        // Act
        var result = await ExecuteCommandAsync(cmd);

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

        var cmd = CreateCommand(season: seasonNumber, episode: episodeNumber, tmdbId: tmdb);

        // Act
        var result = await ExecuteCommandAsync(cmd);

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

        var cmd = CreateCommand(season: seasonNumber, episode: episodeNumber, tvdbId: tvdb);

        // Act
        var result = await ExecuteCommandAsync(cmd);

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

        var cmd = CreateCommand(limit: 50);

        // Act
        var result = await ExecuteCommandAsync(cmd);

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
        var cmd = CreateCommand(limit: limit, offset: offset);

        var expectedTotal = await IDbContext.PlexTvShowEpisodeData.CountAsync(CancellationToken);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.Items.Count.ShouldBe(limit);
        result.Value.Channel.Response.Offset.ShouldBe(offset);
        result.Value.Channel.Response.Total.ShouldBe(expectedTotal);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenTvShowImdbIdIsMalformed()
    {
        // Arrange
        await SetupDatabase(
            3107,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var command = CreateCommand(imdbId: "ttnot-a-number");

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenTvShowExternalIdsConflict()
    {
        // Arrange
        await SetupDatabase(
            3108,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );

        var shows = await IDbContext.PlexTvShows.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await IDbContext
            .PlexTvShows.Where(x => x.Id == shows[0].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(show => show.Guid_IMDB, "tt111111").SetProperty(show => show.Guid_TMDB, 111111),
                CancellationToken
            );
        await IDbContext
            .PlexTvShows.Where(x => x.Id == shows[1].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(show => show.Guid_IMDB, "tt222222").SetProperty(show => show.Guid_TMDB, 222222),
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

        var cmd = CreateCommand(query: "anything", limit: 10);

        // Act
        var result = await ExecuteCommandAsync(cmd);

        // Assert
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public void ShouldValidate_WhenPagingOnlyProvided()
    {
        // Arrange
        var validator = new SearchTvShowCommandValidator();
        var cmd = CreateCommand(limit: 10);

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
        var cmd = CreateCommand(season: 1, episode: 1);

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
        var cmd = CreateCommand(season: 1, episode: 2, imdbId: "imdb://tt12345");

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
        var validator = new SearchTvShowCommandValidator();
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
        var validator = new SearchTvShowCommandValidator();
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
        var validator = new SearchTvShowCommandValidator();
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
        var validator = new SearchTvShowCommandValidator();
        var cmd = CreateCommand(limit: 10, offset: -1);

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
        var cmd = CreateCommand(season: -1);

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
        var cmd = CreateCommand(episode: -1);

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
        var cmd = CreateCommand(tmdbId: -1);

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
        var cmd = CreateCommand(tvdbId: -1);

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

        var cmd = CreateCommand(
            season: seasonNumber,
            episode: episodeNumber,
            imdbId: imdb.Replace("tt", string.Empty),
            tmdbId: tmdb,
            tvdbId: tvdb
        );

        // Act
        var result = await ExecuteCommandAsync(cmd);

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
    private async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteCommandAsync(SearchTvShowCommand command)
    {
        var networkSettings = Mock.Mock<INetworkSettings>();
        networkSettings.SetupGet(x => x.Url).Returns("http://localhost").Verifiable(Times.AtMostOnce());

        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        networkSettings.Verify();
        return result;
    }

    private static SearchTvShowCommand CreateCommand(
        int limit = 100,
        int offset = 0,
        string query = "",
        int season = 0,
        int episode = 0,
        int tvdbId = 0,
        int tmdbId = 0,
        string imdbId = ""
    ) =>
        new(
            new TorznabRequest
            {
                Type = TorznabQueryType.TvSearch,
                Query = query,
                Season = season,
                Episode = episode,
                TvdbId = tvdbId,
                ImdbId = imdbId,
                TmdbId = tmdbId,
                ApiKey = "",
                Limit = limit,
                Offset = offset,
                Categories = [],
                Attributes = [],
                Integration = new IntegrationIdentity(IntegrationType.Sonarr, Guid.Empty),
            }
        );

}
