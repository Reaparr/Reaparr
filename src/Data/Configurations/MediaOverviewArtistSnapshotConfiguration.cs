namespace Reaparr.Data;

public class MediaOverviewArtistSnapshotConfiguration
    : MediaOverviewSnapshotConfigurationBase<MediaOverviewMusicArtistSnapshot>
{
    public override void Configure(EntityTypeBuilder<MediaOverviewMusicArtistSnapshot> builder)
    {
        base.Configure(builder);

        builder
            .HasOne(x => x.PlexArtist)
            .WithOne()
            .HasForeignKey<MediaOverviewMusicArtistSnapshot>(x => x.PlexArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
