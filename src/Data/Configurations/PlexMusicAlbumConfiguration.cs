namespace Reaparr.Data;

public class PlexMusicAlbumConfiguration : IEntityTypeConfiguration<PlexMusicAlbum>
{
    public void Configure(EntityTypeBuilder<PlexMusicAlbum> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.Albums)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.PlexArtist)
            .WithMany(x => x.Albums)
            .HasForeignKey(x => x.PlexArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
