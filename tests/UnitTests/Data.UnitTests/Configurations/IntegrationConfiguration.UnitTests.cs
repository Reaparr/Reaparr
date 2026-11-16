using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Reaparr.Data.UnitTests;

public class IntegrationConfigurationUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldConfigureSeparateIntegrationPersistenceContracts()
    {
        await SetupDatabase(626553);
        await using var context = (ReaparrDbContext)IDbContext;
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertIntegration<SonarrIntegration>(model);
        AssertIntegration<RadarrIntegration>(model);
    }

    [Test]
    public async Task ShouldConfigureTypedTaskOwnership()
    {
        await SetupDatabase(626554);
        await using var context = (ReaparrDbContext)IDbContext;
        var entityType = context.Model.FindEntityType(typeof(DownloadTaskBase)).ShouldNotBeNull();
        var foreignKeys = entityType.GetForeignKeys().ToList();

        foreignKeys.ShouldContain(x =>
            x.PrincipalEntityType.ClrType == typeof(SonarrIntegration)
            && x.Properties.Single().Name == nameof(DownloadTaskBase.SonarrIntegrationId)
            && x.DeleteBehavior == DeleteBehavior.SetNull
        );
        foreignKeys.ShouldContain(x =>
            x.PrincipalEntityType.ClrType == typeof(RadarrIntegration)
            && x.Properties.Single().Name == nameof(DownloadTaskBase.RadarrIntegrationId)
            && x.DeleteBehavior == DeleteBehavior.SetNull
        );
    }

    private static void AssertIntegration<T>(IModel model)
    {
        var entityType = model.FindEntityType(typeof(T)).ShouldNotBeNull();
        var indexes = entityType.GetIndexes().ToList();
        entityType.FindProperty("QBittorrentApiKey")!.GetMaxLength().ShouldBe(32);
        entityType.FindProperty("TorznabApiKey")!.GetMaxLength().ShouldBe(32);
        indexes.ShouldContain(x => x.IsUnique && HasProperties(x, "DisplayName"));
        indexes.ShouldContain(x => x.IsUnique && HasProperties(x, "BaseUrl"));
        indexes.ShouldContain(x => x.IsUnique && HasProperties(x, "Category"));
        indexes.ShouldNotContain(x => HasProperties(x, "QBittorrentApiKey"));
        indexes.ShouldNotContain(x => HasProperties(x, "TorznabApiKey"));
    }

    private static bool HasProperties(IIndex index, params string[] propertyNames) =>
        index.Properties.Select(x => x.Name).SequenceEqual(propertyNames);
}
