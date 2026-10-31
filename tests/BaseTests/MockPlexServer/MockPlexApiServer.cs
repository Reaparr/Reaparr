using LukeHagar.PlexAPI.SDK.Models.Components;
using Moq.Contrib.HttpClient;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.BaseTests;

public class MockPlexApiServer : IMockPlexApiServer
{
    private Seed _seed = new(1);
    private Action<PlexApiDataConfig> _options = null!;
    private PlexApiDataConfig _config = null!;

    private readonly List<PlexDevice> _servers = [];

    /// <summary>
    /// Key: PlexDevice.ClientIdentifier (server key)
    /// </summary>
    private readonly Dictionary<string, List<Connections>> _connections = [];

    /// <summary>
    /// Key: PlexDevice.ClientIdentifier (server key)
    /// </summary>
    private readonly Dictionary<string, List<LibrarySection>> _libraries = [];

    /// <summary>
    /// Key: LibrarySection.Uuid (library Uuid)
    /// </summary>
    private readonly Dictionary<string, List<Metadata>> _movies = [];

    /// <summary>
    /// Key: LibrarySection.Uuid (library Uuid)
    /// </summary>
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly Dictionary<string, List<Metadata>> _tvShows = [];

    /// <summary>
    /// Key: LibrarySection.Uuid (library Uuid)
    /// </summary>
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly Dictionary<string, List<Metadata>> _seasons = [];

    /// <summary>
    /// Key: LibrarySection.Uuid (library Uuid)
    /// </summary>
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly Dictionary<string, List<Metadata>> _episodes = [];

    private readonly Dictionary<string, List<Metadata>> _artists = [];
    private readonly Dictionary<string, List<Metadata>> _albums = [];
    private readonly Dictionary<string, List<Metadata>> _tracks = [];
    private readonly Dictionary<string, List<Metadata>> _photoAlbums = [];
    private readonly Dictionary<string, List<Metadata>> _photos = [];
    private readonly Dictionary<string, Dictionary<string, List<Metadata>>> _photoAlbumMembers = [];
    private readonly Dictionary<string, Dictionary<string, List<Metadata>>> _photoFolderMembers = [];
    private readonly Dictionary<string, List<Metadata>> _otherVideos = [];

    private IReaparrDbContext _dbContext;

    public MockPlexApiServer(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Setup(Mock<HttpMessageHandler> handler, Action<PlexApiDataConfig> options)
    {
        _options = options;
        _config = PlexApiDataConfig.FromOptions(_options);
        _seed = _config.Seed;

        SetupServers(handler);

        if (_config.SetServerResourcesResponse == HttpStatusCode.Unauthorized)
            return;

        SetupIdentityRequest(handler);

        SetupLibraries(handler);

        SetupMedia(handler);
    }

    private void SetupServers(Mock<HttpMessageHandler> handler)
    {
        var devices = new List<PlexDevice>();

        // If we are generating from the database, fetch existing servers
        if (_config.GenerateFromDatabase)
        {
            var plexApiDTOList = _dbContext.PlexServers.IncludeConnections().ToList().ToPlexApiDTO();
            devices.AddRange(plexApiDTOList);
        }

        for (var i = 0; i < _config.PlexServerAccessCount; i++)
        {
            var tempSeed = new Seed(_seed.Next());

            var plexServerWithNonHttps = FakePlexApiData.GetServerResource(tempSeed).Generate();
            var plexServerWithHttps = FakePlexApiData
                .GetServerResource(
                    tempSeed,
                    y =>
                    {
                        y.PlexServerAccessConnectionsIncludeHttps = true;
                    }
                )
                .Generate();

            plexServerWithNonHttps.Connections.AddRange(plexServerWithHttps.Connections);

            devices.Add(plexServerWithNonHttps);
        }

        // Add devices and connections to the internal lists
        foreach (var device in devices)
        {
            _connections.TryAdd(device.ClientIdentifier, device.Connections);

            if (_servers.All(s => s.ClientIdentifier != device.ClientIdentifier))
                _servers.Add(device);
        }

        var uriBuilder = new UriBuilder("https://plex.tv/") { Path = "/api/v2/resources" };
        handler
            .SetupRequestAnyQuery(HttpMethod.Get, uriBuilder.Uri)
            .ReturnsAsync(
                (HttpRequestMessage req, CancellationToken _) =>
                {
                    if (_config.SetServerResourcesResponse == HttpStatusCode.Unauthorized)
                    {
                        return FakePlexApiData
                            .GetPlexUnauthorizedResponseMessage(req)
                            .ToJsonHttpResponse(req, HttpStatusCode.Unauthorized);
                    }

                    var queryDict = req.ParseQueryToDictionary();
                    var includeHttps = false;
                    var includeRelay = false;
                    var includeIPv6 = false;

                    if (queryDict.TryGetValue("includeHttps", out var includeHttpsValue))
                        includeHttps = includeHttpsValue == "1";

                    if (queryDict.TryGetValue("includeRelay", out var includeRelayValue))
                        includeRelay = includeRelayValue == "1";

                    if (queryDict.TryGetValue("includeIPv6", out var includeIPv6Value))
                        includeIPv6 = includeIPv6Value == "1";

                    foreach (var server in _servers)
                    {
                        server.Connections = server
                            .Connections.Where(connection =>
                                connection.Protocol
                                    == (includeHttps ? PlexDeviceProtocol.Https : PlexDeviceProtocol.Http)
                                || connection.Relay == includeRelay
                                || connection.IPv6 == includeIPv6
                            )
                            .ToList();
                    }

                    return FakePlexApiData
                        .GetServerResourcesResponse(HttpStatusCode.OK, _seed, devices: _servers, req)
                        .PlexDevices.ToJsonHttpResponse(req, HttpStatusCode.OK);
                }
            );
    }

    private void SetupIdentityRequest(Mock<HttpMessageHandler> handler)
    {
        foreach (var server in _servers)
        {
            foreach (var connection in _connections[server.ClientIdentifier])
            {
                handler.SetupIdentityRequest(_seed, connection.Uri);
            }
        }
    }

    private void SetupLibraries(Mock<HttpMessageHandler> handler)
    {
        // If we are generating from the database, fetch existing libraries
        if (_config.GenerateFromDatabase)
        {
            var plexServers = _dbContext.PlexServers.IncludeLibraries().ToList();

            foreach (var plexServer in plexServers)
                _libraries[plexServer.MachineIdentifier] = plexServer.PlexLibraries.ToList().ToPlexApiDTO();
        }

        foreach (var server in _servers)
        {
            // Keep DB-backed libraries intact when GenerateFromDatabase is enabled.
            // This preserves existing library keys/ids expected by refresh-media tests.
            if (_libraries.ContainsKey(server.ClientIdentifier))
                continue;

            var libraries = new List<LibrarySection>();

            if (_config.MovieLibraryCount > 0)
            {
                libraries.AddRange(
                    FakePlexApiData
                        .GetLibrariesResponseDirectory(_seed, PlexMediaType.Movie)
                        .Generate(_config.MovieLibraryCount)
                );
            }

            if (_config.TvShowLibraryCount > 0)
            {
                libraries.AddRange(
                    FakePlexApiData
                        .GetLibrariesResponseDirectory(_seed, PlexMediaType.TvShow)
                        .Generate(_config.TvShowLibraryCount)
                );
            }

            if (_config.MusicLibraryCount > 0)
                libraries.AddRange(
                    FakePlexApiData
                        .GetLibrariesResponseDirectory(_seed, PlexMediaType.MusicArtist)
                        .Generate(_config.MusicLibraryCount)
                );

            if (_config.PhotoLibraryCount > 0)
                libraries.AddRange(
                    FakePlexApiData
                        .GetLibrariesResponseDirectory(_seed, PlexMediaType.PhotoAlbum)
                        .Generate(_config.PhotoLibraryCount)
                );

            if (_config.OtherVideoLibraryCount > 0)
                libraries.AddRange(
                    FakePlexApiData
                        .GetLibrariesResponseDirectory(_seed, PlexMediaType.OtherVideos)
                        .Generate(_config.OtherVideoLibraryCount)
                );

            _libraries[server.ClientIdentifier] = libraries;
        }

        // Setup libraries responses
        foreach (var server in _servers)
        {
            foreach (
                var library in _libraries[server.ClientIdentifier]
                    .Where(x => x.Type.ToPlexMediaType() == PlexMediaType.PhotoAlbum)
            )
                library.Location = [new LibrarySectionLocation { Id = 1, Path = "/fixture/photos" }];
            foreach (var connection in _connections[server.ClientIdentifier])
            {
                var uriBuilder = new UriBuilder(connection.Uri) { Path = "/library/sections/all" };
                handler
                    .SetupRequest(
                        HttpMethod.Get,
                        request =>
                            request.RequestUri!.GetLeftPart(UriPartial.Path)
                                == uriBuilder.Uri.GetLeftPart(UriPartial.Path)
                            || request.RequestUri.GetLeftPart(UriPartial.Path)
                                == new Uri(new UriBuilder(connection.Uri).Uri, "/library/sections").GetLeftPart(
                                    UriPartial.Path
                                )
                    )
                    .ReturnsAsync(
                        (HttpRequestMessage req, CancellationToken _) =>
                        {
                            if (_config.SetLibrarySectionsResponse != HttpStatusCode.OK)
                                return new { Error = "Configured library sections failure" }.ToJsonHttpResponse(
                                    req,
                                    _config.SetLibrarySectionsResponse
                                );

                            var response = FakePlexApiData.GetAllLibrariesResponse(
                                HttpStatusCode.OK,
                                _seed,
                                request: req
                            );
                            response.Object.ShouldNotBeNull();
                            response.Object.MediaContainer.ShouldNotBeNull();
                            response.Object.MediaContainer.Directory = _libraries[server.ClientIdentifier];
                            response.Object.MediaContainer.Size = response.Object.MediaContainer.Directory.Count;
                            response.Object.MediaContainer.TotalSize = response.Object.MediaContainer.Directory.Count;
                            return response.Object.ToJsonHttpResponse(req, HttpStatusCode.OK);
                        }
                    );
            }
        }
    }

    private void SetupMedia(Mock<HttpMessageHandler> handler)
    {
        foreach (var server in _servers)
        {
            // Generate media for each library
            foreach (var library in _libraries[server.ClientIdentifier])
            {
                var type = library.Type.ToPlexMediaType();
                var libraryKey = library.Uuid;

                if (type == PlexMediaType.Movie && library.Agent != "com.plexapp.agents.none")
                {
                    var movies = FakePlexApiData
                        .GetMediaMetaDataMetadata(_seed, PlexMediaType.Movie, _options)
                        .Generate(_config.MoviesPerLibraryCount);

                    _movies.TryAdd(libraryKey, movies);
                    continue;
                }

                if (type == PlexMediaType.Movie && library.Agent == "com.plexapp.agents.none")
                    type = PlexMediaType.OtherVideos;

                if (type == PlexMediaType.TvShow)
                {
                    var tvShowList = new List<Metadata>();
                    var seasonList = new List<Metadata>();
                    var episodeList = new List<Metadata>();

                    var tvShows = FakePlexApiData
                        .GetMediaMetaDataMetadata(_seed, PlexMediaType.TvShow, _options)
                        .Generate(_config.TvShowsPerLibraryCount);

                    tvShowList.AddRange(tvShows);
                    foreach (var tvShow in tvShows)
                    {
                        var seasons = FakePlexApiData
                            .GetMediaMetaDataMetadata(_seed, PlexMediaType.Season, _options)
                            .Generate(_config.SeasonsPerTvShowCount);

                        seasons.ForEach(x => x.SetParentValues(tvShow));

                        seasonList.AddRange(seasons);
                        foreach (var season in seasons)
                        {
                            var episodes = FakePlexApiData
                                .GetMediaMetaDataMetadata(_seed, PlexMediaType.Episode, _options)
                                .Generate(_config.EpisodesPerSeasonCount);

                            episodes.ForEach(x => x.SetParentValues(season));
                            episodes.ForEach(x => x.SetGrandparentValues(tvShow));
                            episodeList.AddRange(episodes);
                        }
                    }

                    _tvShows.TryAdd(libraryKey, tvShowList);
                    _seasons.TryAdd(libraryKey, seasonList);
                    _episodes.TryAdd(libraryKey, episodeList);
                    continue;
                }

                if (type == PlexMediaType.MusicArtist)
                {
                    var artistList = FakePlexApiData
                        .GetMediaMetaDataMetadata(_seed, PlexMediaType.MusicArtist, _options)
                        .Generate(_config.ArtistsPerLibraryCount);
                    var albumList = new List<Metadata>();
                    var trackList = new List<Metadata>();
                    foreach (var artist in artistList)
                    {
                        var artistAlbums = FakePlexApiData
                            .GetMediaMetaDataMetadata(_seed, PlexMediaType.MusicAlbum, _options)
                            .Generate(_config.AlbumsPerArtistCount);
                        artistAlbums.ForEach(x => x.SetParentValues(artist));
                        albumList.AddRange(artistAlbums);
                        foreach (var album in artistAlbums)
                        {
                            var albumTracks = FakePlexApiData
                                .GetMediaMetaDataMetadata(_seed, PlexMediaType.MusicTrack, _options)
                                .Generate(_config.TracksPerAlbumCount);
                            albumTracks.ForEach(x => x.SetParentValues(album));
                            albumTracks.ForEach(x => x.SetGrandparentValues(artist));
                            trackList.AddRange(albumTracks);
                        }
                    }
                    _artists.TryAdd(libraryKey, artistList);
                    _albums.TryAdd(libraryKey, albumList);
                    _tracks.TryAdd(libraryKey, trackList);
                    continue;
                }

                if (type == PlexMediaType.PhotoAlbum)
                {
                    var albumList = FakePlexApiData
                        .GetMediaMetaDataMetadata(_seed, PlexMediaType.PhotoAlbum, _options)
                        .Generate(_config.PhotoAlbumsPerLibraryCount);
                    var photoList = new List<Metadata>();
                    var albumMembers = new Dictionary<string, List<Metadata>>(StringComparer.Ordinal);
                    Metadata? sharedAsset = null;

                    for (var albumIndex = 0; albumIndex < albumList.Count; albumIndex++)
                    {
                        var album = albumList[albumIndex];
                        var photos = FakePlexApiData
                            .GetMediaMetaDataMetadata(_seed, PlexMediaType.PhotoImage, _options)
                            .Generate(_config.PhotosPerAlbumCount + _config.PhotoClipsPerAlbumCount);
                        if (_config.PhotoAlbumsOverlap && albumIndex > 0 && sharedAsset is not null && photos.Count > 0)
                            photos[0] = sharedAsset;

                        photos.ForEach(x => x.SetParentValues(album));
                        for (var i = _config.PhotosPerAlbumCount; i < photos.Count; i++)
                        {
                            photos[i].Type = "clip";
                            photos[i].Subtype = null;
                            foreach (var media in photos[i].Media ?? [])
                            {
                                media.Container = "mp4";
                                media.VideoCodec = "h264";
                                media.AudioCodec = "aac";
                                foreach (var part in media.Part ?? [])
                                    part.File = $"/fixture/photos/clip-{part.Id}.mp4";
                            }
                        }

                        if (_config.PhotoAlbumsOverlap && sharedAsset is null && photos.Count > 0)
                            sharedAsset = photos[0];

                        var members = photos.DistinctBy(x => x.RatingKey, StringComparer.Ordinal).ToList();
                        albumMembers[album.RatingKey!] = members;
                        foreach (var photo in members)
                        {
                            if (photoList.All(x => x.RatingKey != photo.RatingKey))
                                photoList.Add(photo);
                        }
                    }

                    _photoAlbums.TryAdd(libraryKey, albumList);
                    _photos.TryAdd(libraryKey, photoList);
                    _photoAlbumMembers.TryAdd(libraryKey, albumMembers);
                    _photoFolderMembers.TryAdd(
                        libraryKey,
                        new Dictionary<string, List<Metadata>>(StringComparer.Ordinal) { ["root"] = photoList }
                    );
                    continue;
                }

                if (type == PlexMediaType.OtherVideos)
                {
                    var videos = FakePlexApiData
                        .GetMediaMetaDataMetadata(_seed, PlexMediaType.Movie, _options)
                        .Generate(_config.OtherVideosPerLibraryCount);
                    _otherVideos.TryAdd(libraryKey, videos);
                    continue;
                }

                throw new InvalidOperationException($"Unsupported library type: {library.Type}");
            }
        }

        // Setup media responses
        foreach (var server in _servers)
        {
            foreach (var library in _libraries[server.ClientIdentifier])
            {
                foreach (var connection in _connections[server.ClientIdentifier])
                {
                    var uriBuilder = new UriBuilder(connection.Uri) { Path = $"/library/sections/{library.Key}/all" };

                    handler
                        .SetupRequestAnyQuery(HttpMethod.Get, uriBuilder.Uri)
                        .ReturnsAsync(
                            (HttpRequestMessage req, CancellationToken _) =>
                            {
                                var queryDict = req.ParseQueryToDictionary();

                                int containerStart = 0,
                                    containerSize = 0;

                                if (
                                    queryDict.TryGetValue("X-Plex-Container-Start", out var containerStartValue)
                                    && int.TryParse(containerStartValue, out var start)
                                )
                                    containerStart = start;

                                if (
                                    queryDict.TryGetValue("X-Plex-Container-Size", out var containerSizeValue)
                                    && int.TryParse(containerSizeValue, out var size)
                                )
                                    containerSize = size;
                                if (
                                    req.Headers.TryGetValues("X-Plex-Container-Start", out var starts)
                                    && int.TryParse(starts.Single(), out var headerStart)
                                )
                                    containerStart = headerStart;
                                if (
                                    req.Headers.TryGetValues("X-Plex-Container-Size", out var sizes)
                                    && int.TryParse(sizes.Single(), out var headerSize)
                                )
                                    containerSize = headerSize;

                                // Photos use Plex's numeric type 13 for images and 12 for clips.
                                var libraryType = PlexMediaType.Unknown;
                                queryDict.TryGetValue("type", out var type);
                                if (
                                    library.Type.ToPlexMediaType() == PlexMediaType.PhotoAlbum
                                    && (type == "12" || type == "13")
                                )
                                    libraryType = PlexMediaType.PhotoImage;
                                else if (type is not null)
                                    libraryType = type.ToPlexMediaTypeFromTypeInt();
                                if (
                                    library.Agent == "com.plexapp.agents.none"
                                    && library.Scanner == "Plex Video Files Scanner"
                                    && libraryType == PlexMediaType.Movie
                                )
                                    libraryType = PlexMediaType.OtherVideos;

                                var responseBody = FakePlexApiData.GetLibrarySectionsAllResponseBody(
                                    _seed,
                                    library,
                                    options: _options
                                );

                                var fullList =
                                    library.Type.ToPlexMediaType() == PlexMediaType.PhotoAlbum
                                        ? type switch
                                        {
                                            "12" => _photos[library.Uuid].Where(x => x.Type == "clip").ToList(),
                                            "13" => _photos[library.Uuid].Where(x => x.Type != "clip").ToList(),
                                            "14" => _photoAlbums[library.Uuid],
                                            _ => _photos[library.Uuid],
                                        }
                                        : GetMediaItems(library.Uuid, libraryType);

                                // Apply slicing based on containerStart and containerSize
                                if (containerSize > 0)
                                {
                                    var take = containerSize;
                                    if (_config.IncompleteMediaType == libraryType && fullList.Count > containerStart)
                                        take = Math.Max(
                                            0,
                                            Math.Min(containerSize, fullList.Count - containerStart) - 1
                                        );
                                    responseBody.MediaContainer!.Metadata = fullList
                                        .Skip(containerStart)
                                        .Take(take)
                                        .ToList();
                                }
                                else
                                {
                                    // No size specified: return everything from containerStart to the end
                                    responseBody.MediaContainer!.Metadata = fullList.Skip(containerStart).ToList();
                                }

                                responseBody.MediaContainer!.Size = responseBody.MediaContainer.Metadata!.Count;
                                responseBody.MediaContainer!.Offset = containerStart;
                                responseBody.MediaContainer!.TotalSize = fullList.Count;

                                return FakePlexApiData
                                    .GetLibrarySectionsAllResponse(
                                        HttpStatusCode.OK,
                                        _seed,
                                        library,
                                        responseBody,
                                        request: req
                                    )
                                    .MediaContainerWithMetadata!.ToJsonHttpResponse(req, HttpStatusCode.OK);
                            }
                        );

                    // Mock the metadata retrieval for specific items
                    handler
                        .SetupRequest(
                            HttpMethod.Get,
                            uri =>
                                uri.RequestUri!.AbsolutePath.StartsWith("/library/metadata/")
                                && !uri.RequestUri.AbsolutePath.EndsWith("/children", StringComparison.Ordinal)
                        )
                        .ReturnsAsync(
                            (HttpRequestMessage request, CancellationToken _) =>
                            {
                                var urlEncodedIds = request.RequestUri!.AbsolutePath.Split("/library/metadata/")[1];
                                var metadataIds = Uri.UnescapeDataString(urlEncodedIds)
                                    .Split(',')
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .Select(int.Parse)
                                    .ToArray();

                                var metadataList = metadataIds
                                    .Select(x => GetMediaItem(x.ToString()))
                                    .Where(x => x is not null)
                                    .Select(x => x!)
                                    .ToList();

                                var responseBody = FakePlexApiData.GetMediaMetaDataResponseBodyResponse(
                                    _seed,
                                    library,
                                    options: _options
                                );

                                responseBody.MediaContainer!.Metadata = metadataList;
                                responseBody.MediaContainer!.Size = metadataList.Count;

                                return FakePlexApiData
                                    .GetMediaMetaDataAsync(HttpStatusCode.OK, _seed, library, responseBody, request)
                                    .MediaContainerWithMetadata!.ToJsonHttpResponse(request, HttpStatusCode.OK);
                            }
                        );

                    if (library.Type.ToPlexMediaType() == PlexMediaType.PhotoAlbum)
                    {
                        handler
                            .SetupRequest(
                                HttpMethod.Get,
                                request =>
                                    request.RequestUri!.GetLeftPart(UriPartial.Authority)
                                        == new UriBuilder(connection.Uri).Uri.GetLeftPart(UriPartial.Authority)
                                    && (
                                        IsPhotoFolderRequest(request, library.Key)
                                        || _photoAlbums[library.Uuid]
                                            .Any(album =>
                                                request.RequestUri.AbsolutePath
                                                == $"/library/metadata/{album.RatingKey}/children"
                                            )
                                    )
                            )
                            .ReturnsAsync(
                                (HttpRequestMessage request, CancellationToken _) =>
                                {
                                    var requestUri = request.RequestUri!;
                                    var query = request.ParseQueryToDictionary();
                                    List<Metadata> metadata;
                                    if (requestUri.AbsolutePath.EndsWith("/children", StringComparison.Ordinal))
                                    {
                                        var albumKey = requestUri.AbsolutePath[
                                            "/library/metadata/".Length..^"/children".Length
                                        ];
                                        metadata = _photoAlbumMembers[library.Uuid].GetValueOrDefault(albumKey) ?? [];
                                    }
                                    else
                                    {
                                        var parent = query.TryGetValue("parent", out var parentKey)
                                            ? parentKey
                                            : "root";
                                        metadata =
                                            _photoFolderMembers[library.Uuid].GetValueOrDefault(parent)
                                            ?? _photoFolderMembers[library.Uuid].GetValueOrDefault("root")
                                            ?? [];
                                    }
                                    var offset =
                                        request.Headers.TryGetValues("X-Plex-Container-Start", out var offsets)
                                            ? int.Parse(
                                                offsets.Single(),
                                                System.Globalization.CultureInfo.InvariantCulture
                                            )
                                        : query.TryGetValue("X-Plex-Container-Start", out var start)
                                            ? int.Parse(start, System.Globalization.CultureInfo.InvariantCulture)
                                        : 0;
                                    var size =
                                        request.Headers.TryGetValues("X-Plex-Container-Size", out var sizes)
                                            ? int.Parse(
                                                sizes.Single(),
                                                System.Globalization.CultureInfo.InvariantCulture
                                            )
                                        : query.TryGetValue("X-Plex-Container-Size", out var pageSize)
                                            ? int.Parse(pageSize, System.Globalization.CultureInfo.InvariantCulture)
                                        : 50;
                                    var page = metadata.Skip(offset).Take(size).ToList();
                                    return new
                                    {
                                        MediaContainer = new
                                        {
                                            offset,
                                            size = page.Count,
                                            totalSize = metadata.Count,
                                            Metadata = page,
                                        },
                                    }.ToJsonHttpResponse(request, HttpStatusCode.OK);
                                }
                            );
                    }
                }
            }
        }
    }

    private static bool IsPhotoFolderRequest(HttpRequestMessage request, string? libraryKey) =>
        !string.IsNullOrEmpty(libraryKey)
        && request.RequestUri!.AbsolutePath == $"/library/sections/{libraryKey}/folder";

    private Metadata? GetMediaItem(string key)
    {
        foreach (var movie in _movies)
        {
            var result = movie.Value.FirstOrDefault(x => x.RatingKey == key);
            if (result != null)
                return result;
        }

        foreach (var episode in _episodes)
        {
            var result = episode.Value.FirstOrDefault(x => x.RatingKey == key);
            if (result != null)
                return result;
        }

        foreach (var tvShow in _tvShows)
        {
            var result = tvShow.Value.FirstOrDefault(x => x.RatingKey == key);
            if (result != null)
                return result;
        }

        foreach (var season in _seasons)
        {
            var result = season.Value.FirstOrDefault(x => x.RatingKey == key);
            if (result != null)
                return result;
        }

        foreach (var artist in _artists.SelectMany(x => x.Value))
            if (artist.RatingKey == key)
                return artist;
        foreach (var album in _albums.SelectMany(x => x.Value))
            if (album.RatingKey == key)
                return album;
        foreach (var track in _tracks.SelectMany(x => x.Value))
            if (track.RatingKey == key)
                return track;
        foreach (var album in _photoAlbums.SelectMany(x => x.Value))
            if (album.RatingKey == key)
                return album;
        foreach (var photo in _photos.SelectMany(x => x.Value))
            if (photo.RatingKey == key)
                return photo;
        foreach (var video in _otherVideos.SelectMany(x => x.Value))
            if (video.RatingKey == key)
                return video;

        return null;
    }

    private ICollection<Metadata> GetMediaItems(string libraryUuid, PlexMediaType libraryType)
    {
        return libraryType switch
        {
            PlexMediaType.Movie => _movies.TryGetValue(libraryUuid, out var movies) ? movies : [],
            PlexMediaType.TvShow => _tvShows.TryGetValue(libraryUuid, out var shows) ? shows : [],
            PlexMediaType.Season => _seasons.TryGetValue(libraryUuid, out var seasons) ? seasons : [],
            PlexMediaType.Episode => _episodes.TryGetValue(libraryUuid, out var episodes) ? episodes : [],
            PlexMediaType.MusicArtist => _artists.TryGetValue(libraryUuid, out var artists) ? artists : [],
            PlexMediaType.MusicAlbum => _albums.TryGetValue(libraryUuid, out var albums) ? albums : [],
            PlexMediaType.MusicTrack => _tracks.TryGetValue(libraryUuid, out var tracks) ? tracks : [],
            PlexMediaType.PhotoAlbum => _photoAlbums.TryGetValue(libraryUuid, out var photoAlbums) ? photoAlbums : [],
            PlexMediaType.PhotoImage => _photos.TryGetValue(libraryUuid, out var photos) ? photos : [],
            PlexMediaType.OtherVideos => _otherVideos.TryGetValue(libraryUuid, out var videos) ? videos : [],
            _ => throw new ArgumentOutOfRangeException(nameof(libraryType), $"Unhandled library type: {libraryType}"),
        };
    }
}
