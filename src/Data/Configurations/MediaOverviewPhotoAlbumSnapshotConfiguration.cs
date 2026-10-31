namespace Reaparr.Data;

public class MediaOverviewPhotoAlbumSnapshotConfiguration
    : MediaOverviewSnapshotConfigurationBase<MediaOverviewPhotoAlbumSnapshot>
{
    public override void Configure(EntityTypeBuilder<MediaOverviewPhotoAlbumSnapshot> builder)
    {
        base.Configure(builder);
        builder.HasIndex(x => new
        {
            x.TitleRank,
            x.PlexLibraryId,
            x.PlexPhotoAlbumId,
        });

        builder
            .HasOne(x => x.PlexPhotoAlbum)
            .WithOne()
            .HasForeignKey<MediaOverviewPhotoAlbumSnapshot>(x => x.PlexPhotoAlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
