namespace Reaparr.Domain;

public static partial class EnumMapperExtensions
{
    private static readonly Dictionary<string, JobTypes> _jobTypesMap = new(StringComparer.Ordinal)
    {
        ["Unknown"] = JobTypes.Unknown,
        ["None"] = JobTypes.None,
        ["CheckAllConnectionsStatusByPlexServerJob"] = JobTypes.CheckAllConnectionsStatusByPlexServerJob,
        ["DownloadJob"] = JobTypes.DownloadJob,
        ["MoveDownloadFileJob"] = JobTypes.MoveDownloadFileJob,
        ["InspectPlexServerJob"] = JobTypes.InspectPlexServerJob,
        ["LibrarySyncJob"] = JobTypes.LibrarySyncJob,
        ["MetadataSyncJob"] = JobTypes.MetadataSyncJob,
        ["CheckForUpdateJob"] = JobTypes.CheckForUpdateJob,
        ["CheckPlexLibrariesForUpdatesJob"] = JobTypes.CheckPlexLibrariesForUpdatesJob,
        ["RefreshPlexAccountAccessJob"] = JobTypes.RefreshPlexAccountAccessJob,
        ["LibraryComparisonJob"] = JobTypes.LibraryComparisonJob,
        ["MediaOverviewSnapshotJob"] = JobTypes.MediaOverviewSnapshotJob,
        ["OptimizeDatabaseJob"] = JobTypes.OptimizeDatabaseJob,
    };

    /// <summary>
    /// Converts a string to <see cref="JobTypes"/> using a fast lookup.
    /// </summary>
    /// <param name="value">The string representation of <see cref="JobTypes"/>.</param>
    /// <returns>The converted <see cref="JobTypes"/> value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="value"/> does not map to a <see cref="JobTypes"/> value.
    /// </exception>
    public static JobTypes ToJobTypes(this string value)
    {
        if (_jobTypesMap.TryGetValue(value, out var jobType))
            return jobType;

        _log.Here().Error("Failed to convert string {Value} to type {NameOfJobTypes}", value, nameof(JobTypes));

        throw new ArgumentOutOfRangeException(nameof(value), value, null);
    }

    /// <summary>
    /// Converts <see cref="JobTypes"/> to its mapped string representation.
    /// </summary>
    /// <param name="value">The <see cref="JobTypes"/> value.</param>
    /// <returns>The mapped string representation of <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="value"/> does not have a string mapping.
    /// </exception>
    public static string ToJobTypesString(this JobTypes value)
    {
        foreach (var (key, jobType) in _jobTypesMap)
        {
            if (jobType == value)
                return key;
        }

        _log.Here().Error("Failed to convert {Value} to string for type {NameOfJobTypes}", value, nameof(JobTypes));

        throw new ArgumentOutOfRangeException(nameof(value), value, null);
    }
}
