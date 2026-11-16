using System.Globalization;

namespace Reaparr.IntegrationTests;

[NotInParallel]
public class CultureInitializationIntegrationTests
{
    [Test]
    public void ShouldInitializeIntegrationTestCultureInvariant_WhenAmbientCultureIsNonInvariant()
    {
        // Arrange
        var originalCurrentCulture = CultureInfo.CurrentCulture;
        var originalCurrentUiCulture = CultureInfo.CurrentUICulture;
        var ambientCulture = CultureInfo.GetCultureInfo("fr-FR");

        try
        {
            CultureInfo.CurrentCulture = ambientCulture;

            // Act
            var probe = new IntegrationTestCultureProbe();
            var initializedCulture = probe.CultureAfterBase;

            // Assert
            initializedCulture.ShouldBe(CultureInfo.InvariantCulture);
            CultureInfo.CurrentUICulture.ShouldBe(originalCurrentUiCulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCurrentCulture;
        }

        CultureInfo.CurrentCulture.ShouldBe(originalCurrentCulture);
        CultureInfo.CurrentUICulture.ShouldBe(originalCurrentUiCulture);
    }

    private sealed class IntegrationTestCultureProbe : BaseIntegrationTests
    {
        public IntegrationTestCultureProbe()
        {
            CultureAfterBase = CultureInfo.CurrentCulture;
        }

        public CultureInfo CultureAfterBase { get; }
    }
}
