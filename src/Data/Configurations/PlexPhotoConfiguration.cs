namespace Reaparr.Data;

public class PlexPhotoConfiguration : IEntityTypeConfiguration<PlexPhotoImage>
{
    public void Configure(EntityTypeBuilder<PlexPhotoImage> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexPhotoAlbumId, x.SortIndex });
        builder
            .HasOne(x => x.PlexPhotoAlbum)
            .WithMany(x => x.Photos)
            .HasForeignKey(x => x.PlexPhotoAlbumId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.PhotoImages)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
