namespace Reaparr.Settings.Contracts;

/// <summary>
/// Daily half-hour change points. Each day starts Unlimited; null resets to Unlimited.
/// </summary>
public sealed record DownloadSchedule
{
    public bool Enabled { get; init; }

    public Dictionary<string, Dictionary<string, int?>> Days { get; init; } = new();

    public static bool IsValidDays(Dictionary<string, Dictionary<string, int?>>? days)
    {
        if (days is null)
            return false;

        foreach (var (day, points) in days)
        {
            if (
                day
                    is not (
                        nameof(DayOfWeek.Monday)
                        or nameof(DayOfWeek.Tuesday)
                        or nameof(DayOfWeek.Wednesday)
                        or nameof(DayOfWeek.Thursday)
                        or nameof(DayOfWeek.Friday)
                        or nameof(DayOfWeek.Saturday)
                        or nameof(DayOfWeek.Sunday)
                    )
                || points is null
            )
                return false;

            foreach (var (time, limit) in points)
            {
                if (
                    time.Length != 5
                    || time[0] is < '0' or > '2'
                    || time[1] is < '0' or > '9'
                    || (time[0] == '2' && time[1] > '3')
                    || time[2] != ':'
                    || time[3] is not ('0' or '3')
                    || time[4] != '0'
                    || limit is <= 0
                )
                    return false;
            }
        }

        return true;
    }
}
