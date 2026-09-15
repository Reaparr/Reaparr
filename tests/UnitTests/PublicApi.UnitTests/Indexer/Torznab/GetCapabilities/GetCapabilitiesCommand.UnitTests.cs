using System.Xml.Serialization;
using Reaparr.PublicAPI.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class GetCapabilitiesCommandUnitTests : BaseCommandUnitTest<GetCapabilitiesCommand>
{
    [Test]
    public async Task ShouldSerializeOnlyStandardCapabilitySections()
    {
        // Arrange
        var command = new GetCapabilitiesCommand();
        // Act
        var result = await TestHandlerExecuteAsync<TorznabCapsResponseDTO>(command);
        var serializer = new XmlSerializer(typeof(TorznabCapsResponseDTO));
        await using var stream = new MemoryStream();
        serializer.Serialize(stream, result.Value);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var xml = await reader.ReadToEndAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        xml.ShouldContain("<server");
        xml.ShouldContain("<limits");
        xml.ShouldContain("<searching>");
        xml.ShouldContain("<categories>");
        result.Value.Categories.ShouldBe(
            IntegrationDefinitions
                .SupportedTorznabCategories.Select(x => new TorznabCategory((int)x.Id, x.Name))
                .ToList()
        );
        result.Value.Limits.Max.ShouldBe(10_000);
        xml.ShouldNotContain("<torznab:attributes");
        xml.ShouldNotContain("<attributes");
    }
}
