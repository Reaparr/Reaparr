namespace Reaparr.Data;

public class PlexMusicAlbumConfiguration : IEntityTypeConfiguration<PlexMusicAlbum>
{
    public void Configure(EntityTypeBuilder<PlexMusicAlbum> builder)
    {
        builder.Ignore(x => x.VideoQuality);
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.Property(x => x.MusicBrainzReleaseId).UseCollation("BINARY");
        builder.Property(x => x.PlexApiIdentityId).UseCollation("BINARY");
        builder.HasIndex(x => new { x.PlexLibraryId, PlexIdentityId = x.PlexApiIdentityId });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexArtistId, x.SortIndex, x.Id });
        builder.HasAlternateKey(x => new { x.PlexLibraryId, x.Id });

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
            .HasForeignKey(x => new { x.PlexLibraryId, x.PlexArtistId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Countries)
            .WithMany(x => x.PlexMusicAlbumCountries)
            .UsingEntity<PlexMusicAlbumCountries>(
                l => l.HasOne<PlexCountry>().WithMany().HasForeignKey(e => e.CountryId),
                r => r.HasOne<PlexMusicAlbum>().WithMany().HasForeignKey(e => e.PlexMusicAlbumId)
            );
    }
}
