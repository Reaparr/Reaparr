namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexMusicArtist> _plexMusicArtist = new Faker<PlexMusicArtist>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Type, _ => PlexMediaType.Artist)
        .RuleFor(x => x.Title, f => f.Name.FullName())
        .RuleFor(x => x.Guid, f => $"plex://artist/{f.Random.Guid():N}")
        .RuleFor(x => x.MusicBrainzArtistId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.Guid_IMDB, _ => null)
        .RuleFor(x => x.Guid_TMDB, _ => null)
        .RuleFor(x => x.Guid_TVDB, _ => null)
        .Ignore(x => x.Albums)
        .FinishWith(
            (_, artist) =>
            {
                foreach (var album in artist.Albums)
                {
                    album.PlexArtistId = artist.Id;
                    album.PlexArtist = artist;
                    album.FullTitle = $"{artist.Title}/{album.Title}";

                    foreach (var track in album.Tracks)
                        track.FullTitle = $"{album.FullTitle}/{track.Title}";
                }

                artist.ChildCount = artist.Albums.Count;
                artist.Duration = artist.Albums.Sum(x => x.Duration);
                artist.MediaSize = artist.Albums.Sum(x => x.MediaSize);
                artist.Quality = VideoQuality.Unknown;
                artist.FullTitle = artist.Title;
            }
        );

    public static Faker<PlexMusicArtist> GetPlexMusicArtists(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);
        var albumFaker = GetPlexMusicAlbums(seed, options);

        return _plexMusicArtist
            .Clone()
            .RuleFor(x => x.Albums, _ => albumFaker.Generate(config.MusicAlbumCount))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexMusicAlbum> _plexMusicAlbum = new Faker<PlexMusicAlbum>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Type, _ => PlexMediaType.Album)
        .Ignore(x => x.PlexArtistId)
        .Ignore(x => x.PlexArtist)
        .RuleFor(x => x.Title, f => f.Commerce.ProductName())
        .RuleFor(x => x.Guid, f => $"plex://album/{f.Random.Guid():N}")
        .RuleFor(x => x.MusicBrainzReleaseId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.MusicBrainzReleaseGroupId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.Guid_IMDB, _ => null)
        .RuleFor(x => x.Guid_TMDB, _ => null)
        .RuleFor(x => x.Guid_TVDB, _ => null)
        .RuleFor(x => x.ReleaseDate, f => f.Date.Past(40))
        .RuleFor(x => x.RecordLabel, f => f.Company.CompanyName())
        .RuleFor(x => x.Country, f => f.Address.CountryCode())
        .Ignore(x => x.DiscCount)
        .Ignore(x => x.TrackCount)
        .Ignore(x => x.Tracks)
        .FinishWith(
            (_, album) =>
            {
                foreach (var (track, index) in album.Tracks.Select((track, index) => (track, index)))
                {
                    track.PlexAlbumId = album.Id;
                    track.PlexAlbum = album;
                    track.DiscNumber = 1;
                    track.TrackNumber = index + 1;
                    track.FullTitle = $"{album.Title}/{track.Title}";
                }

                album.ChildCount = album.Tracks.Count;
                album.TrackCount = album.Tracks.Count;
                album.DiscCount = album.Tracks.Count == 0 ? 0 : 1;
                album.Duration = album.Tracks.Sum(x => x.Duration);
                album.MediaSize = album.Tracks.Sum(x => x.MediaSize);
                album.Quality = VideoQuality.Unknown;
                album.FullTitle = album.Title;
            }
        );

    public static Faker<PlexMusicAlbum> GetPlexMusicAlbums(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);
        var trackFaker = GetPlexMusicTracks(seed, options);

        return _plexMusicAlbum
            .Clone()
            .RuleFor(x => x.Tracks, _ => trackFaker.Generate(config.MusicTrackCount))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexMusicTrack> _plexMusicTrack = new Faker<PlexMusicTrack>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Type, _ => PlexMediaType.Song)
        .Ignore(x => x.PlexAlbumId)
        .Ignore(x => x.PlexAlbum)
        .RuleFor(x => x.Title, f => string.Join(" ", f.Lorem.Words(f.Random.Int(1, 4))))
        .RuleFor(x => x.Guid, f => $"plex://track/{f.Random.Guid():N}")
        .RuleFor(x => x.MusicBrainzRecordingId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.MusicBrainzReleaseTrackId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.Guid_IMDB, _ => null)
        .RuleFor(x => x.Guid_TMDB, _ => null)
        .RuleFor(x => x.Guid_TVDB, _ => null)
        .RuleFor(x => x.DiscNumber, _ => 1)
        .RuleFor(x => x.TrackNumber, f => f.IndexFaker + 1)
        .Ignore(x => x.MediaDataList)
        .FinishWith(
            (_, track) =>
            {
                var original = track.MediaDataList.Single();
                original.PlexTrackId = track.Id;
                original.PlexTrack = track;
                original.UpdateInitProperty(nameof(original.PlexApiRatingKey), track.PlexApiRatingKey);

                track.ChildCount = 0;
                track.Duration = original.Duration / 1000;
                track.MediaSize = original.Size;
                track.Quality = VideoQuality.Unknown;
                track.FullTitle = track.Title;
            }
        );

    public static Faker<PlexMusicTrack> GetPlexMusicTracks(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var mediaDataFaker = GetPlexMusicTrackMediaData(seed, options);

        return _plexMusicTrack
            .Clone()
            .RuleFor(x => x.MediaDataList, _ => mediaDataFaker.Generate(1))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexMusicTrackMediaData> _plexMusicTrackMediaData =
        new Faker<PlexMusicTrackMediaData>()
            .ApplyBasePlexMediaData(new FakeDataConfig())
            .Ignore(x => x.PlexTrackId)
            .Ignore(x => x.PlexTrack)
            .RuleFor(x => x.PartIndex, _ => 0)
            .RuleFor(x => x.VideoCodec, _ => string.Empty)
            .RuleFor(x => x.AudioCodec, _ => "flac")
            .RuleFor(x => x.VideoResolution, _ => VideoQuality.Unknown)
            .RuleFor(x => x.Quality, _ => VideoQuality.Unknown)
            .RuleFor(x => x.Duration, f => f.Random.Int(90_000, 600_000))
            .RuleFor(x => x.Container, _ => "flac")
            .RuleFor(x => x.Source, _ => ReleaseSource.None)
            .RuleFor(x => x.OriginalFilename, f => $"{f.Random.Guid():N}.flac")
            .RuleFor(x => x.Key, (_, x) => $"/library/parts/{x.PlexApiPartId}/file.flac")
            .RuleFor(x => x.OriginalFilePath, (_, x) => $"/music/{x.OriginalFilename}")
            .RuleFor(x => x.SourceRelativePath, (_, x) => x.OriginalFilename)
            .RuleFor(x => x.Width, _ => null)
            .RuleFor(x => x.Height, _ => null)
            .RuleFor(x => x.VideoProfile, _ => null)
            .RuleFor(x => x.VideoFrameRate, _ => null)
            .RuleFor(x => x.GeneratedNameSyncedAt, _ => null);

    public static Faker<PlexMusicTrackMediaData> GetPlexMusicTrackMediaData(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var faker = _plexMusicTrackMediaData.Clone();
        if (config.DownloadFileSizeInMb > 0)
            faker.RuleFor(x => x.Size, (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes);
        return faker.UseSeed(seed.Next());
    }
}
