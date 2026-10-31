namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexMusicArtist> _plexMusicArtist = new Faker<PlexMusicArtist>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Title, f => f.Name.FullName())
        .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
        .RuleFor(x => x.PlexApiIdentityId, f => f.Random.Guid().ToString("N"))
        .RuleFor(x => x.MusicBrainzArtistId, f => f.Random.Guid().ToString("D"))
        .Ignore(x => x.Albums)
        .Ignore(x => x.Qualities)
        .Ignore(x => x.Actors)
        .Ignore(x => x.Genres)
        .Ignore(x => x.Countries)
        .FinishWith(
            (_, artist) =>
            {
                foreach (var album in artist.Albums)
                {
                    album.PlexArtistId = artist.Id;
                    album.PlexArtist = artist;
                    album.ParentKey = artist.PlexApiRatingKey;
                }

                artist.ChildCount = artist.Albums.Count;
                artist.Duration = artist.Albums.Sum(x => x.Duration);
                artist.MediaSize = artist.Albums.Sum(x => x.MediaSize);
                artist.VideoQuality = VideoQuality.None;
            }
        );

    public static Faker<PlexMusicArtist> GetPlexMusicArtists(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);
        var albumFaker = GetPlexMusicAlbums(seed, options);

        return _plexMusicArtist
            .Clone() // Clone to isolate per-call child rules and seed state.
            .RuleFor(x => x.Albums, _ => albumFaker.Generate(config.MusicAlbumCount))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexMusicAlbum> _plexMusicAlbum = new Faker<PlexMusicAlbum>()
        .ApplyBasePlexMedia()
        .Ignore(x => x.PlexArtistId)
        .Ignore(x => x.PlexArtist)
        .Ignore(x => x.ParentKey)
        .RuleFor(x => x.Title, f => f.Commerce.ProductName())
        .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
        .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
        .RuleFor(x => x.Rating, f => f.Random.Double(0.1))
        .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Past(40))
        .RuleFor(x => x.PlexApiIdentityId, f => f.Random.Guid().ToString("N"))
        .RuleFor(x => x.MusicBrainzReleaseId, f => f.Random.Guid().ToString("D"))
        .RuleFor(x => x.RecordLabel, f => f.Company.CompanyName())
        .Ignore(x => x.Countries)
        .Ignore(x => x.Tracks)
        .Ignore(x => x.Qualities)
        .FinishWith(
            (_, album) =>
            {
                foreach (var (track, index) in album.Tracks.Select((track, index) => (track, index)))
                {
                    track.PlexAlbumId = album.Id;
                    track.PlexAlbum = album;
                    track.ParentKey = album.PlexApiRatingKey;
                    track.DiscNumber = 1;
                    track.TrackNumber = index + 1;
                }
                album.ChildCount = album.Tracks.Count;
                album.Duration = album.Tracks.Sum(x => x.Duration);
                album.MediaSize = album.Tracks.Sum(x => x.MediaSize);
                album.VideoQuality = VideoQuality.None;
            }
        );

    public static Faker<PlexMusicAlbum> GetPlexMusicAlbums(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);
        var trackFaker = GetPlexMusicTracks(seed, options);

        return _plexMusicAlbum
            .Clone() // Clone to isolate per-call child rules and seed state.
            .RuleFor(x => x.Tracks, _ => trackFaker.Generate(config.MusicTrackCount))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexMusicTrack> _plexMusicTrack = new Faker<PlexMusicTrack>()
        .ApplyBasePlexMedia()
        .Ignore(x => x.PlexAlbumId)
        .Ignore(x => x.PlexAlbum)
        .Ignore(x => x.ParentKey)
        .RuleFor(x => x.Title, f => string.Join(" ", f.Lorem.Words(f.Random.Int(1, 4))))
        .RuleFor(x => x.PlexApiIdentityId, f => f.Random.Guid().ToString("N"))
        .RuleFor(x => x.MusicBrainzReleaseTrackId, f => f.Random.Guid().ToString("D"))
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
                track.VideoQuality = VideoQuality.None;
            }
        );

    public static Faker<PlexMusicTrack> GetPlexMusicTracks(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var mediaDataFaker = GetPlexMusicTrackMediaData(seed, options);

        return _plexMusicTrack
            .Clone() // Clone to isolate per-call media-data rules and seed state.
            .RuleFor(x => x.MediaDataList, _ => mediaDataFaker.Generate(1))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexMusicTrackMediaData> _plexMusicTrackMediaData =
        new Faker<PlexMusicTrackMediaData>()
            .ApplyBasePlexMediaData(new FakeDataConfig())
            .Ignore(x => x.PlexTrackId)
            .Ignore(x => x.PlexTrack)
            .Ignore(x => x.Streams)
            .RuleFor(x => x.PartIndex, _ => 0)
            .RuleFor(x => x.VideoCodec, _ => string.Empty)
            .RuleFor(x => x.AudioCodec, _ => "flac")
            .RuleFor(x => x.AudioBitrateKbps, _ => 737)
            .RuleFor(x => x.AudioBitDepth, _ => 16)
            .RuleFor(x => x.AudioSampleRateHz, _ => 44_100)
            .RuleFor(x => x.AudioChannels, _ => 2)
            .RuleFor(x => x.AudioChannelLayout, _ => "stereo")
            .RuleFor(x => x.AudioProfile, _ => null)
            .RuleFor(x => x.AudioQuality, _ => AudioQuality.FLAC_16Bit_44_1kHz)
            .RuleFor(x => x.VideoQuality, _ => VideoQuality.None)
            .RuleFor(x => x.Duration, f => f.Random.Int(90_000, 600_000))
            .RuleFor(x => x.Container, _ => "flac")
            .RuleFor(x => x.Source, _ => ReleaseSource.None)
            .RuleFor(x => x.OriginalFilename, f => $"{f.Random.Guid():N}.flac")
            .RuleFor(x => x.Key, (_, x) => $"/library/parts/{x.PlexApiPartId}/file.flac");

    public static Faker<PlexMusicTrackMediaData> GetPlexMusicTrackMediaData(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        return _plexMusicTrackMediaData
            .Clone() // Clone to isolate per-call size rules and caller overrides.
            .RuleFor(
                x => x.Size,
                _ =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : (long)ByteSize.FromMebiBytes(10).Bytes
            )
            .UseSeed(seed.Next());
    }
}
