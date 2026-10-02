namespace Reaparr.Data;

public class PlexPhotoMediaDataConfiguration : IEntityTypeConfiguration<PlexPhotoMediaData>
{
    public void Configure(EntityTypeBuilder<PlexPhotoMediaData> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiPartId }).IsUnique();

        builder.HasIndex(x => x.PlexApiRatingKey);
        builder
            .HasOne(x => x.PlexPhoto)
            .WithMany(x => x.MediaDataList)
            .HasForeignKey(x => x.PlexPhotoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany()
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
