namespace Reaparr.Data;

public sealed class PlexPhotoAlbumConfiguration : IEntityTypeConfiguration<PlexPhotoAlbum>
{
    public void Configure(EntityTypeBuilder<PlexPhotoAlbum> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.Property(x => x.Title).UseCollation(OrderByNaturalExtensions.CollationName);
        builder.HasIndex(x => new
        {
            x.PlexLibraryId,
            x.Title,
            x.Id,
        });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.PhotoAlbums)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
