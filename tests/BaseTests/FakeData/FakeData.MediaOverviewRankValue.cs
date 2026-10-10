namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<MediaOverviewRankValue> _mediaOverviewRankValue = new Faker<MediaOverviewRankValue>()
        .CustomInstantiator(f =>
            new MediaOverviewRankValue(
                f.Lorem.Word(),
                f.Random.Bool() ? f.Random.Int(1900, 2030) : null,
                f.Date.Past(4),
                f.Random.Bool() ? f.Date.Recent(30) : null,
                f.Random.Bool() ? f.Random.Int(1, 3_000_000) : null,
                f.Random.Long(1_000, 30_000_000)
            )
        );

    public static Faker<MediaOverviewRankValue> GetMediaOverviewRankValues(Seed seed) =>
        _mediaOverviewRankValue.Clone().UseSeed(seed.Next());
}
