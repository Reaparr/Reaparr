namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexPhotoAlbum> _plexPhotoAlbum = new Faker<PlexPhotoAlbum>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.PlexServerId, _ => GetUniqueNumber())
        .RuleFor(x => x.PlexLibraryId, _ => GetUniqueNumber())
        .RuleFor(x => x.PlexApiRatingKey, _ => GetUniqueNumber())
        .RuleFor(x => x.Title, f => f.Lorem.Sentence(3))
        .RuleFor(x => x.SearchTitle, (_, x) => x.Title.ToSearchTitle())
        .RuleFor(x => x.Summary, f => f.Lorem.Sentence())
        .RuleFor(x => x.HasThumb, _ => true)
        .RuleFor(x => x.AddedAt, f => f.Date.Past(4).ToUniversalTime())
        .RuleFor(x => x.UpdatedAt, f => f.Date.Recent().ToUniversalTime())
        .Ignore(x => x.PlexServer)
        .Ignore(x => x.PlexLibrary)
        .Ignore(x => x.Memberships);

    public static Faker<PlexPhotoAlbum> GetPlexPhotoAlbums(Seed seed, Action<FakeDataConfig>? options = null) =>
        _plexPhotoAlbum.Clone().UseSeed(seed.Next());

    private static readonly Faker<PlexPhotoMediaData> _plexPhotoMediaData = CreatePlexPhotoMediaDataFaker();

    private static readonly Faker<PlexPhotoMediaData> _plexPhotoClipMediaData = CreatePlexPhotoMediaDataFaker()
        .RuleFor(x => x.AssetKind, _ => PhotoAssetKind.VideoClip)
        .RuleFor(x => x.Width, _ => 1920)
        .RuleFor(x => x.Height, _ => 1080)
        .RuleFor(x => x.Duration, f => f.Random.Int(1_000, 120_000))
        .RuleFor(x => x.VideoCodec, _ => "h264")
        .RuleFor(x => x.AudioCodec, _ => "aac")
        .RuleFor(x => x.Container, _ => "mp4")
        .RuleFor(x => x.OriginalFilename, (_, x) => $"photo-{x.PlexApiPartId}.mp4")
        .RuleFor(x => x.Key, (_, x) => $"/library/parts/{x.PlexApiPartId}/file.mp4")
        .RuleFor(x => x.SourcePath, (_, x) => $"/fixture/photos/photo-{x.PlexApiPartId}.mp4");

    private static Faker<PlexPhotoMediaData> CreatePlexPhotoMediaDataFaker() =>
        new Faker<PlexPhotoMediaData>()
            .StrictMode(true)
            .Ignore(x => x.Id)
            .RuleFor(x => x.PlexServerId, _ => GetUniqueNumber())
            .RuleFor(x => x.PlexLibraryId, _ => GetUniqueNumber())
            .RuleFor(x => x.PlexApiRatingKey, _ => GetUniqueNumber())
            .RuleFor(x => x.PlexApiMediaId, _ => GetUniqueNumber())
            .RuleFor(x => x.PlexApiPartId, _ => GetUniqueNumber())
            .RuleFor(x => x.AssetKind, _ => PhotoAssetKind.Image)
            .RuleFor(x => x.Title, f => f.Lorem.Sentence(3))
            .RuleFor(x => x.SearchTitle, (_, x) => x.Title.ToSearchTitle())
            .RuleFor(x => x.Summary, f => f.Lorem.Sentence())
            .RuleFor(x => x.HasThumb, _ => true)
            .RuleFor(x => x.AddedAt, f => f.Date.Past(4).ToUniversalTime())
            .RuleFor(x => x.UpdatedAt, f => f.Date.Recent().ToUniversalTime())
            .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Past(2).ToUniversalTime())
            .RuleFor(x => x.SourcePath, (_, x) => $"/fixture/photos/photo-{x.PlexApiPartId}.jpg")
            .RuleFor(x => x.OriginalFilename, (_, x) => $"photo-{x.PlexApiPartId}.jpg")
            .RuleFor(x => x.Key, (_, x) => $"/library/parts/{x.PlexApiPartId}/file.jpg")
            .RuleFor(x => x.Size, _ => 1_048_576L)
            .RuleFor(x => x.Container, _ => "jpg")
            .RuleFor(x => x.Width, _ => 4000)
            .RuleFor(x => x.Height, _ => 3000)
            .RuleFor(x => x.Duration, _ => (int?)null)
            .RuleFor(x => x.VideoCodec, _ => (string?)null)
            .RuleFor(x => x.AudioCodec, _ => (string?)null)
            .Ignore(x => x.PlexPhotoFolderId)
            .Ignore(x => x.PlexPhotoFolder)
            .Ignore(x => x.AlbumMemberships)
            .Ignore(x => x.PlexServer)
            .Ignore(x => x.PlexLibrary);

    public static Faker<PlexPhotoMediaData> GetPlexPhotoMediaData(
        Seed seed,
        Action<FakeDataConfig>? options = null,
        bool isClip = false
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        return (isClip ? _plexPhotoClipMediaData : _plexPhotoMediaData)
            .Clone()
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
