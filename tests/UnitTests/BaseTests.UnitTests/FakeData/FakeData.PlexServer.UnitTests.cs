namespace Reaparr.BaseTests.UnitTests;

public class FakeDataPlexServerUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldRetainConnectionOwnership_WhenAnotherGeneratorIsConfiguredBeforeGeneration()
    {
        // Arrange
        var customGenerator = FakeData.GetPlexServerConnections(new Seed(233324), isCustom: true, plexServerId: 42);
        var ordinaryGenerator = FakeData.GetPlexServerConnections(new Seed(233325), plexServerId: 73);

        // Act
        var customConnections = customGenerator.Generate(3);
        var ordinaryConnections = ordinaryGenerator.Generate(2);

        // Assert
        customConnections.Select(x => (x.PlexServerId, x.IsCustom)).ShouldBe([(42, true), (42, true), (42, true)]);
        ordinaryConnections.Select(x => (x.PlexServerId, x.IsCustom)).ShouldBe([(73, false), (73, false)]);
    }
}
