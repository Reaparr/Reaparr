using Autofac.Extensions.DependencyInjection;
using Reaparr.Data.Contracts;

namespace Reaparr.AppHost;

public static partial class Startup
{
    /// <summary>
    /// Set up the database.
    /// </summary>
    public static async Task<Result> SetupDatabase(this WebApplication app)
    {
        await using var scope = app.Services.GetAutofacRoot().BeginLifetimeScope();

        var dbContextManager = scope.Resolve<IReaparrDbContextManager>();

        return await dbContextManager.SetupAsync();
    }
}
