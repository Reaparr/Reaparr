namespace Reaparr.Data;

public class MediaOverviewOtherVideoSnapshotConfiguration
    : MediaOverviewSnapshotConfigurationBase<MediaOverviewOtherVideoSnapshot>
{
    public override void Configure(EntityTypeBuilder<MediaOverviewOtherVideoSnapshot> builder)
    {
        base.Configure(builder);

        builder
            .HasOne(x => x.PlexOtherVideo)
            .WithOne()
            .HasForeignKey<MediaOverviewOtherVideoSnapshot>(x => x.PlexOtherVideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
