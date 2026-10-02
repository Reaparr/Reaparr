namespace Reaparr.Data;

public class MediaOverviewArtistSnapshotConfiguration
    : MediaOverviewSnapshotConfigurationBase<MediaOverviewArtistSnapshot>
{
    public override void Configure(EntityTypeBuilder<MediaOverviewArtistSnapshot> builder)
    {
        base.Configure(builder);

        builder
            .HasOne(x => x.PlexArtist)
            .WithOne()
            .HasForeignKey<MediaOverviewArtistSnapshot>(x => x.PlexArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
