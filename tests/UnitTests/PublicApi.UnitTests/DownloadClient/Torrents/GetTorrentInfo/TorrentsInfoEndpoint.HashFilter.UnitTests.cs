using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reactive.Linq;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using Reaparr.PublicAPI.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class TorrentsInfoEndpointHashFilterUnitTests
    : BaseEndpointUnitTest<TorrentsInfoEndpoint, TorrentsInfoEndpointRequest, List<QBittorrentTorrentInfo>>
{
    private const int EligibleFileCount = 20;
    private const int ControlFileCount = 2;
    private const string Category = "torrent-contract";
    private const string MovieSavePath = "/contract-downloads/Movies/movie-batch";
    private const string EpisodeSavePath = "/contract-downloads/TvShows/show-batch/season-01";
    private const string MovieTableName = "DownloadTaskMovieFile";
    private const string EpisodeTableName = "DownloadTaskTvShowEpisodeFile";

    [Test]
    [Arguments(HashFilterScenario.SingleMovie)]
    [Arguments(HashFilterScenario.SingleEpisode)]
    [Arguments(HashFilterScenario.MixedCasePipeList)]
    [Arguments(HashFilterScenario.All)]
    [Arguments(HashFilterScenario.Omitted)]
    [Arguments(HashFilterScenario.NoMatch)]
    public async Task ShouldRestrictSqlRows_WhilePreservingResponseContract(HashFilterScenario scenario)
    {
        // Arrange
        var fixture = await SetupFixtureAsync();
        var expected = GetScenarioExpectation(scenario);
        fixture.MovieFileCount.ShouldBe(EligibleFileCount + ControlFileCount);
        fixture.EpisodeFileCount.ShouldBe(EligibleFileCount + ControlFileCount);

        var connectionString = ((DbContext)IDbContext).Database.GetConnectionString();
        var movieReads = new ConcurrentQueue<int>();
        var episodeReads = new ConcurrentQueue<int>();
        using var querySubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener.AllListeners.Subscribe(listener =>
        {
            if (listener.Name != "Microsoft.EntityFrameworkCore")
                return;

            querySubscriptions.Add(
                listener.Subscribe(eventData =>
                {
                    if (
                        eventData.Value is not DataReaderDisposingEventData reader
                        || reader.Context?.Database.GetConnectionString() != connectionString
                    )
                        return;

                    // Table names only identify the endpoint's two ToListAsync readers; ReadCount is the proof.
                    var commandText = reader.Command.CommandText;
                    var target = commandText.Contains(MovieTableName, StringComparison.Ordinal)
                        ? movieReads
                        : commandText.Contains(EpisodeTableName, StringComparison.Ordinal)
                            ? episodeReads
                            : null;
                    if (target is null)
                        return;

                    // RelationalDataReader increments ReadCount before each ReadAsync; ToListAsync performs one final false read.
                    // Subtract that completion read to assert rows returned; see EF Core v10 RelationalDataReader.ReadAsync:
                    // https://raw.githubusercontent.com/dotnet/efcore/v10.0.0/src/EFCore.Relational/Storage/RelationalDataReader.cs
                    target.Enqueue(reader.ReadCount - 1);
                })
            );
        });

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new TorrentsInfoEndpointRequest
            {
                Hashes = expected.Hashes,
                Category = Category.ToUpperInvariant(),
            },
            integrationIdentity: fixture.OwnerIdentity
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.ValidationErrors.Count.ShouldBe(0);
        endpointResult.Response.ShouldNotBeNull();
        var actual = endpointResult
            .Response!
            .Select(x => (x.Hash, x.State, x.Category, x.SavePath, x.ContentPath))
            .OrderBy(x => x.Hash)
            .ToList();
        var expectedRows = expected
            .Episodes.Concat(expected.Movies)
            .Select(x => (x.Hash, x.State, x.Category, x.SavePath, x.ContentPath))
            .OrderBy(x => x.Hash)
            .ToList();
        actual.ShouldBe(expectedRows);
        movieReads.Count.ShouldBe(1);
        episodeReads.Count.ShouldBe(1);
        movieReads.Single().ShouldBe(expected.MovieReaderRows);
        episodeReads.Single().ShouldBe(expected.EpisodeReaderRows);
    }

    private async Task<TestFixture> SetupFixtureAsync()
    {
        await SetupDatabase(
            71471,
            config =>
            {
                config.PlexServerCount = 2;
                config.MovieDownloadTasksCount = EligibleFileCount + ControlFileCount;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = EligibleFileCount + ControlFileCount;
                config.RadarrIntegrationCount = 2;
            }
        );

        var dbContext = IDbContext;
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var integrations = await dbContext.RadarrIntegrations.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var nonOwnedServerId = servers[0].Id;
        var ownedServerId = servers[1].Id;
        var ownerIntegration = integrations[0];
        var foreignIntegration = integrations[1];
        var ownership = (Owner: ownerIntegration.Id, Foreign: foreignIntegration.Id,
            NonOwnedServer: nonOwnedServerId, OwnedServer: ownedServerId);

        await dbContext
            .PlexServers.Where(x => x.Id == nonOwnedServerId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(x => x.IsEnabled, true).SetProperty(x => x.OwnedOverride, false),
                CancellationToken
            );
        await dbContext
            .PlexServers.Where(x => x.Id == ownedServerId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(x => x.IsEnabled, true).SetProperty(x => x.OwnedOverride, true),
                CancellationToken
            );
        await dbContext
            .RadarrIntegrations.Where(x => x.Id == ownerIntegration.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Category, Category), CancellationToken);
        await dbContext
            .RadarrIntegrations.Where(x => x.Id == foreignIntegration.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Category, "foreign-category"), CancellationToken);

        var movieFiles = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var episodeFiles = await dbContext
            .DownloadTaskTvShowEpisodeFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        for (var index = 0; index < movieFiles.Count; index++)
            ConfigureFile(dbContext, movieFiles[index], index, ownership);
        for (var index = 0; index < episodeFiles.Count; index++)
            ConfigureFile(dbContext, episodeFiles[index], index, ownership);

        await dbContext.SaveChangesAsync(CancellationToken);

        return new TestFixture(
            ownerIntegration.Id.ToRadarrIdentity(),
            movieFiles.Count,
            episodeFiles.Count
        );
    }

    private static void ConfigureFile(
        IReaparrDbContext dbContext,
        DownloadTaskFileBase file,
        int index,
        (Guid Owner, Guid Foreign, int NonOwnedServer, int OwnedServer) ownership)
    {
        var isEpisode = file.MediaType == PlexMediaType.Episode;
        var isForeignOwnerControl = index == EligibleFileCount;
        var isOwnedServerControl = index == EligibleFileCount + 1;
        file.PlexServerId = isOwnedServerControl ? ownership.OwnedServer : ownership.NonOwnedServer;
        file.RadarrIntegrationId = isForeignOwnerControl ? ownership.Foreign : ownership.Owner;
        file.FileName = isEpisode ? $"episode-{index:00}.mkv" : $"movie-{index:00}.mkv";
        file.DownloadStatus = isEpisode && index == 0 ? DownloadStatus.Downloading : DownloadStatus.Completed;
        file.DirectoryMeta.DownloadRootPath = "/contract-downloads";
        file.DirectoryMeta.DestinationRootPath = "/contract-destinations";
        file.DirectoryMeta.MovieFolder = "movie-batch";
        file.DirectoryMeta.TvShowFolder = "show-batch";
        file.DirectoryMeta.SeasonFolder = "season-01";
        dbContext.Entry(file).CurrentValues.SetValues(new { HashId = GetStoredHash(isEpisode, index) });
        dbContext.Entry(file).State = EntityState.Modified;
    }

    private static ScenarioExpectation GetScenarioExpectation(HashFilterScenario scenario)
    {
        var movie = ExpectedTorrent(false, 0);
        var episode = ExpectedTorrent(true, 0);

        return scenario switch
        {
            HashFilterScenario.SingleMovie => new(movie.Hash.ToUpperInvariant(), [movie], [], 1, 0),
            HashFilterScenario.SingleEpisode => new(episode.Hash.ToUpperInvariant(), [], [episode], 0, 1),
            HashFilterScenario.MixedCasePipeList => new(
                $" {movie.Hash.ToUpperInvariant()} | {episode.Hash} ",
                [movie],
                [episode],
                1,
                1
            ),
            HashFilterScenario.NoMatch => new(HashFor(0xFFFF).ToUpperInvariant(), [], [], 0, 0),
            _ => GetUnfilteredExpectation(scenario),
        };
    }

    private static ScenarioExpectation GetUnfilteredExpectation(HashFilterScenario scenario)
    {
        if (scenario is not (HashFilterScenario.All or HashFilterScenario.Omitted))
            throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);

        return new(
            scenario == HashFilterScenario.All ? "all" : null,
            Enumerable.Range(0, EligibleFileCount).Select(index => ExpectedTorrent(false, index)).ToList(),
            Enumerable.Range(0, EligibleFileCount).Select(index => ExpectedTorrent(true, index)).ToList(),
            EligibleFileCount,
            EligibleFileCount);
    }

    private static TorrentExpectation ExpectedTorrent(bool isEpisode, int index)
    {
        var savePath = isEpisode ? EpisodeSavePath : MovieSavePath;
        var fileName = isEpisode ? $"episode-{index:00}.mkv" : $"movie-{index:00}.mkv";
        return new(
            GetStoredHash(isEpisode, index),
            isEpisode && index == 0 ? "downloading" : "pausedUP",
            Category,
            savePath,
            Path.Combine(savePath, fileName)
        );
    }

    private static string GetStoredHash(bool isEpisode, int index)
    {
        // Keep each family's distinct prefix outside the four-character mixed-case suffix.
        var familyPrefix = isEpisode ? 0x300000 : 0x100000;
        return index == 0 ? MixedCaseHash(familyPrefix, "aBcD") : HashFor(familyPrefix + index);
    }

    private static string MixedCaseHash(int value, string suffix) => $"{HashFor(value)[..36]}{suffix}";

    private static string HashFor(int value) => value.ToString("x40", CultureInfo.InvariantCulture);

    private sealed record TestFixture(IntegrationIdentity OwnerIdentity, int MovieFileCount, int EpisodeFileCount);

    private sealed record TorrentExpectation(
        string Hash,
        string State,
        string Category,
        string SavePath,
        string ContentPath
    );

    private sealed record ScenarioExpectation(
        string? Hashes,
        IReadOnlyList<TorrentExpectation> Movies,
        IReadOnlyList<TorrentExpectation> Episodes,
        int MovieReaderRows,
        int EpisodeReaderRows
    );

    public enum HashFilterScenario
    {
        SingleMovie,
        SingleEpisode,
        MixedCasePipeList,
        All,
        Omitted,
        NoMatch,
    }
}
