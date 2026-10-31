using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Reaparr.Application.UnitTests;

public class GetMediaDetailByIdMusicBatchingUnitTests
    : BaseEndpointUnitTest<GetMediaDetailByIdEndpoint, GetMediaDetailByIdEndpointRequest, ResultDTO<PlexMediaDTO>>
{
    [Test]
    [Arguments(0, 2)]
    [Arguments(1, 0)]
    [Arguments(1, 2)]
    [Arguments(3, 2)]
    public async Task ShouldPreserveLimitedHierarchyAndParts_WhenSelectedAlbumsAreBatched(int albumCount, int trackLimit)
    {
        // Arrange
        var expected = await ArrangeBatchFixtureAsync(albumCount, trackLimit);
        expected.Albums.Count.ShouldBe(albumCount);
        var dbContext = IDbContext;
        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener.AllListeners
            .Where(listener => listener.Name == "Microsoft.EntityFrameworkCore")
            .Subscribe(listener => commandSubscriptions.Add(listener
                .Select(entry => entry.Value!).OfType<CommandExecutedEventData>()
                .Where(command => command.Context?.Database.GetConnectionString() == connectionString)
                .Subscribe(command => commands.Enqueue(command.Command.CommandText))));
        SetupMusicComparison(expected);

        // Act
        var response = await TestEndpointHandleAsync(
            new GetMediaDetailByIdEndpointRequest(expected.Id, PlexMediaType.MusicArtist));

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(200);
        var result = response.Response.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var dto = result.Value.ShouldNotBeNull();
        (dto.Id, dto.PlexLibraryId, dto.PlexServerId, dto.ChildCount, dto.GrandChildCount).ShouldBe(
            (expected.Id, expected.PlexLibraryId, expected.PlexServerId, albumCount, albumCount * trackLimit));
        dto.Children.Select(x => x.Id).ShouldBe(
            expected.Albums.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.Id));
        foreach (var album in dto.Children)
        {
            var source = expected.Albums.Single(x => x.Id == album.Id);
            (album.ParentId, album.PlexLibraryId, album.ChildCount).ShouldBe(
                ((int?)expected.Id, expected.PlexLibraryId, trackLimit));
            album.Children.Select(x => (x.Id, x.DiscNumber, x.TrackNumber)).ShouldBe(
                source.Tracks.OrderBy(x => x.SortIndex).ThenBy(x => x.Id)
                    .Select(x => (x.Id, x.DiscNumber, x.TrackNumber)));
            foreach (var track in album.Children)
            {
                var original = source.Tracks.Single(x => x.Id == track.Id);
                (track.ParentId, track.PlexLibraryId).ShouldBe(((int?)album.Id, expected.PlexLibraryId));
                track.MediaData.OrderBy(x => x.Id)
                    .Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId, x.FileName, x.Size, x.AudioCodec, x.AudioQuality))
                    .ShouldBe(original.MediaDataList.OrderBy(x => x.Id)
                        .Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId, x.GetFileName, x.Size, x.AudioCodec, x.AudioQuality)));
            }
        }
        var trackCommands = commands.Where(sql => sql.Contains("FROM \"PlexMusicTracks\"")).ToList();
        if (albumCount == 0)
            trackCommands.ShouldBeEmpty();
        else
        {
            trackCommands.ShouldContain(sql => sql.Contains("\"PlexLibraryId\" ="));
            var albumPredicate = albumCount == 1 ? "\"PlexAlbumId\" =" : "\"PlexAlbumId\" IN";
            trackCommands.ShouldAllBe(sql => sql.Contains(albumPredicate));
        }
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldKeepTrackQueryCountConstant_WhenSelectedAlbumCountGrows()
    {
        // Arrange
        var large = await ArrangeBatchFixtureAsync(3, 2);
        var dbContext = IDbContext;
        await dbContext.PlexMusicArtists.Where(x => x.Id == large.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ChildCount, 1), CancellationToken);
        var small = await LoadBaselineHierarchyAsync(dbContext, large.Id);
        small.Albums.Count.ShouldBe(1);
        large.Albums.Count.ShouldBe(3);
        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener.AllListeners
            .Where(listener => listener.Name == "Microsoft.EntityFrameworkCore")
            .Subscribe(listener => commandSubscriptions.Add(listener
                .Select(entry => entry.Value!).OfType<CommandExecutedEventData>()
                .Where(command => command.Context?.Database.GetConnectionString() == connectionString)
                .Subscribe(command => commands.Enqueue(command.Command.CommandText))));
        SetupMusicComparison(small);
        SetupMusicComparison(large);

        // Act
        var smallResponse = await TestEndpointHandleAsync(
            new GetMediaDetailByIdEndpointRequest(small.Id, PlexMediaType.MusicArtist));
        var smallTrackCommands = commands.Count(sql => sql.Contains("FROM \"PlexMusicTracks\""));
        commands.Clear();
        await dbContext.PlexMusicArtists.Where(x => x.Id == large.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ChildCount, 3), CancellationToken);
        var largeResponse = await TestEndpointHandleAsync(
            new GetMediaDetailByIdEndpointRequest(large.Id, PlexMediaType.MusicArtist));
        var largeTrackCommands = commands.Count(sql => sql.Contains("FROM \"PlexMusicTracks\""));

        // Assert
        smallResponse.IsValid.ShouldBeTrue();
        largeResponse.IsValid.ShouldBeTrue();
        smallResponse.StatusCode.ShouldBe(200);
        largeResponse.StatusCode.ShouldBe(200);
        var smallResult = smallResponse.Response.ShouldNotBeNull();
        var largeResult = largeResponse.Response.ShouldNotBeNull();
        smallResult.IsSuccess.ShouldBeTrue();
        largeResult.IsSuccess.ShouldBeTrue();
        smallResult.Errors.Count.ShouldBe(0);
        largeResult.Errors.Count.ShouldBe(0);
        smallResult.Value.ShouldNotBeNull().Children.Select(x => x.Id).ShouldBe(
            small.Albums.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.Id));
        largeResult.Value.ShouldNotBeNull().Children.Select(x => x.Id).ShouldBe(
            large.Albums.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.Id));
        smallTrackCommands.ShouldBeGreaterThan(0);
        largeTrackCommands.ShouldBe(smallTrackCommands);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    private async Task<PlexMusicArtist> ArrangeBatchFixtureAsync(int albumCount, int trackLimit)
    {
        await SetupDatabase(62511, config =>
        {
            config.PlexAccountCount = 1;
            config.PlexMusicLibraryCount = 2;
            config.MusicArtistCount = 2;
            config.MusicAlbumCount = 4;
            config.MusicTrackCount = 3;
        });
        var dbContext = IDbContext;
        var artist = await dbContext.PlexMusicArtists.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        await dbContext.PlexMusicArtists.Where(x => x.Id == artist.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ChildCount, albumCount), CancellationToken);
        var albumIds = await dbContext.PlexMusicAlbums.Where(x => x.PlexArtistId == artist.Id)
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        albumIds.Count.ShouldBe(4);
        for (var index = 0; index < albumIds.Count; index++)
            await dbContext.PlexMusicAlbums.Where(x => x.Id == albumIds[index])
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SortIndex, albumIds.Count - index)
                    .SetProperty(x => x.ChildCount, trackLimit), CancellationToken);
        var trackIds = await dbContext.PlexMusicTracks.Where(x => albumIds.Contains(x.PlexAlbumId))
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        trackIds.Count.ShouldBe(12);
        for (var index = 0; index < trackIds.Count; index++)
            await dbContext.PlexMusicTracks.Where(x => x.Id == trackIds[index])
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.SortIndex, index % 3 == 0 ? 2 : 1)
                    .SetProperty(x => x.DiscNumber, index % 3 == 2 ? (int?)null : 2 - index % 3)
                    .SetProperty(x => x.TrackNumber, index % 3 == 2 ? (int?)null : 10 - index % 3), CancellationToken);
        await AddMultipartTrackAsync(dbContext, trackIds[0]);
        return await LoadBaselineHierarchyAsync(dbContext, artist.Id);
    }

    private async Task AddMultipartTrackAsync(IReaparrDbContext dbContext, int trackId)
    {
        var track = await dbContext.PlexMusicTracks.Include(x => x.MediaDataList)
            .SingleAsync(x => x.Id == trackId, CancellationToken);
        var original = track.MediaDataList.Single();
        var part = FakeData.GetPlexMusicTrackMediaData(new Seed(62512))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexTrackId, _ => track.Id)
            .RuleFor(x => x.PlexLibraryId, _ => track.PlexLibraryId)
            .RuleFor(x => x.PlexServerId, _ => track.PlexServerId)
            .RuleFor(x => x.PlexApiRatingKey, _ => track.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId)
            .RuleFor(x => x.PartIndex, _ => 1)
            .Generate();
        dbContext.PlexMusicTrackData.Add(part);
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexMusicTrackData.Where(x => x.PlexTrackId == trackId)
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken))
            .ShouldBe(new[] { original.Id, part.Id }.Order());
    }

    private async Task<PlexMusicArtist> LoadBaselineHierarchyAsync(IReaparrDbContext dbContext, int artistId)
    {
        // Keep the pre-batch limited query shape as the SQLite selection oracle.
        var artist = await dbContext.PlexMusicArtists.SingleAsync(x => x.Id == artistId, CancellationToken);
        artist.Albums = await dbContext.PlexMusicAlbums.Include(x => x.Qualities)
            .Where(x => x.PlexArtistId == artistId).Take(artist.ChildCount).ToListAsync(CancellationToken);
        artist.Albums = artist.Albums.OrderBy(x => x.SortIndex).ToList();
        foreach (var album in artist.Albums)
            album.Tracks = await dbContext.PlexMusicTracks.Include(x => x.MediaDataList)
                .Where(x => x.PlexAlbumId == album.Id).Take(album.ChildCount).ToListAsync(CancellationToken);
        return artist;
    }

    private void SetupMusicComparison(PlexMusicArtist expected)
    {
        var expectedIds = new[] { expected.Id }.Concat(expected.Albums.Select(x => x.Id))
            .Concat(expected.Albums.SelectMany(x => x.Tracks).Select(x => x.Id)).ToArray();
        var expectedTypes = new[] { PlexMediaType.MusicArtist }
            .Concat(expected.Albums.Select(_ => PlexMediaType.MusicAlbum))
            .Concat(expected.Albums.SelectMany(x => x.Tracks).Select(_ => PlexMediaType.MusicTrack)).ToArray();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<ApplyComparisonStateCommand>(command =>
                    command.PlexLibraryId == expected.PlexLibraryId
                    && command.MediaType == PlexMediaType.MusicArtist
                    && command.Items.Count == expectedIds.Length),
                CancellationToken))
            .Callback<ICommand<Result>, CancellationToken>((command, _) =>
            {
                var items = ((ApplyComparisonStateCommand)command).Items;
                items.Select(item => item.Id).ShouldBe(expectedIds);
                items.Select(item => item.Type).ShouldBe(expectedTypes);
            })
            .Returns<ICommand<Result>, CancellationToken>((command, ct) =>
                new ApplyOwnedMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<Quartz.IScheduler>().Object)
                    .ExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(
                        ((ApplyComparisonStateCommand)command).Items, expected.PlexLibraryId), ct))
            .Verifiable(Times.Once());
    }
}
