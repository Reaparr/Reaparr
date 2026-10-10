namespace Reaparr.Data;

public class MediaOverviewOtherVideoSnapshotConfiguration
    : MediaOverviewSnapshotConfigurationBase<MediaOverviewOtherVideoSnapshot>
{
    public override void Configure(EntityTypeBuilder<MediaOverviewOtherVideoSnapshot> builder)
    {
        base.Configure(builder);
        builder.HasIndex(x => new { x.PlexLibraryId, x.QualityRank });

        builder
            .HasOne(x => x.PlexOtherVideo)
            .WithOne()
            .HasForeignKey<MediaOverviewOtherVideoSnapshot>(x => x.PlexOtherVideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
