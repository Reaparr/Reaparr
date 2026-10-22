namespace Reaparr.Data;

public class MediaOverviewArtistSnapshotConfiguration
    : MediaOverviewSnapshotConfigurationBase<MediaOverviewMusicArtistSnapshot>
{
    public override void Configure(EntityTypeBuilder<MediaOverviewMusicArtistSnapshot> builder)
    {
        base.Configure(builder);
        builder.HasIndex(x => new { x.TitleRank, x.PlexLibraryId, x.PlexArtistId });

        builder
            .HasOne(x => x.PlexArtist)
            .WithOne()
            .HasForeignKey<MediaOverviewMusicArtistSnapshot>(x => x.PlexArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
