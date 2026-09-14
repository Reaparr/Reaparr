namespace Reaparr.Data.Configurations;

public class PlexGenreConfiguration : IEntityTypeConfiguration<PlexGenre>
{
    public void Configure(EntityTypeBuilder<PlexGenre> builder)
    {
        builder.HasIndex(x => x.Key).IsUnique();
        builder
            .Property(x => x.Type)
            .HasJsonConversion()
            .HasMaxLength(20)
            .HasDefaultValue(PlexGenreType.Unknown)
            .IsUnicode(false);
    }
}
