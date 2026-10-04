namespace Reaparr.Data;

public class PlexMusicArtistConfiguration : IEntityTypeConfiguration<PlexMusicArtist>
{
    public void Configure(EntityTypeBuilder<PlexMusicArtist> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.Music)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Actors)
            .WithMany(x => x.PlexMusicArtistActors)
            .UsingEntity<PlexMusicArtistActors>(
                l => l.HasOne<PlexActor>().WithMany().HasForeignKey(e => e.PlexActorId),
                r => r.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(e => e.PlexMusicArtistId)
            );

        builder
            .HasMany(x => x.Genres)
            .WithMany(x => x.PlexMusicArtistGenres)
            .UsingEntity<PlexMusicArtistGenres>(
                l => l.HasOne<PlexGenre>().WithMany().HasForeignKey(e => e.GenresId),
                r => r.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(e => e.PlexMusicArtistId)
            );

        builder
            .HasMany(x => x.Countries)
            .WithMany(x => x.PlexMusicArtistCountries)
            .UsingEntity<PlexMusicArtistCountries>(
                l => l.HasOne<PlexCountry>().WithMany().HasForeignKey(e => e.CountryId),
                r => r.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(e => e.PlexMusicArtistId)
            );
    }
}
