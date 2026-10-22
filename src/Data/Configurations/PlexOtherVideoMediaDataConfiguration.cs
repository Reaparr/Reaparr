namespace Reaparr.Data;

public class PlexOtherVideoMediaDataConfiguration : IEntityTypeConfiguration<PlexOtherVideoMediaData>
{
    public void Configure(EntityTypeBuilder<PlexOtherVideoMediaData> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiPartId }).IsUnique();
        builder
            .HasIndex(x => new
            {
                x.PlexOtherVideoId,
                x.PlexApiMediaId,
                x.PartIndex,
            })
            .IsUnique();
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexOtherVideoId });
        builder
            .HasOne(x => x.PlexOtherVideo)
            .WithMany(x => x.MediaDataList)
            .HasForeignKey(x => new { x.PlexLibraryId, x.PlexOtherVideoId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
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
