// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Reaparr.Domain;

public class FolderPath : BaseEntity
{
    #region Constructor

    public FolderPath() { }

    public FolderPath(FolderType folderType, string displayName, string directoryPath)
    {
        FolderType = folderType;
        DisplayName = displayName;
        DirectoryPath = directoryPath;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the display name of this <see cref="FolderPath"/>.
    /// </summary>
    [Column(Order = 1)]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the <see cref="FolderType"/> of this <see cref="FolderPath"/>.
    /// </summary>
    [Column(Order = 2)]
    public FolderType FolderType { get; init; } = FolderType.Unknown;

    /// <summary>
    /// Gets or sets the <see cref="PlexMediaType"/> associated with this <see cref="FolderPath"/>.
    /// </summary>
    [Column(Order = 3)]
    public PlexMediaType MediaType { get; init; } = PlexMediaType.Unknown;

    /// <summary>
    ///  Gets or sets the filesystem directory path of this <see cref="FolderPath"/>.
    /// </summary>
    [Column(Order = 4)]
    public string DirectoryPath { get; set; } = string.Empty;

    #endregion

    #region Relationships

    /// <summary>
    /// Gets or sets the <see cref="PlexLibraries"/> that use this <see cref="FolderPath"/> as a download destination folder.
    /// </summary>
    public ICollection<PlexLibrary> PlexLibraries { get; init; } = [];

    #endregion

    #region Helpers

    /// <summary>
    /// Checks if the <see cref="FolderPath"/> is valid by checking if the directory exists.
    /// </summary>
    /// <returns>If the check is successful.</returns>
    public bool IsValid => Directory.Exists(DirectoryPath);

    /// <summary>
    /// Checks whether another folder serves the same role.
    /// </summary>
    public bool HasSameRole(FolderPath other) => FolderType == other.FolderType && MediaType == other.MediaType;

    #endregion

    #region Equality

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(DisplayName, DirectoryPath, FolderType, MediaType);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        return obj.GetType() == GetType() && Equals((FolderPath)obj);
    }

    /// <summary>
    /// Compares folder configuration without identity or navigation properties.
    /// </summary>
    protected bool Equals(FolderPath other) =>
        DisplayName == other.DisplayName && DirectoryPath == other.DirectoryPath && HasSameRole(other);

    #endregion
}
