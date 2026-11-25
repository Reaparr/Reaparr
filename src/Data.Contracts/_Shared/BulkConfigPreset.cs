using EFCore.BulkExtensions;

namespace Reaparr.Data.Contracts;

public static class BulkConfigPreset
{
    // Always return a fresh instance. BulkExtensions mutates the operation type on this object; sharing it allowed a concurrent insert to turn a pending SQLite update into an insert.
    public static BulkConfig Default =>
        new()
        {
            BatchSize = 500,
            SetOutputIdentity = true,
            PreserveInsertOrder = true,
            CalculateStats = true,
        };
}
