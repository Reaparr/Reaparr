using System.Text;
using System.Text.Json;

namespace Reaparr.PlexApi.UnitTests;

public class StringToBool : BaseUnitTest
{
    [Test]
    [Arguments("{\"x\": \"1\"}", true)]
    [Arguments("{\"x\": \"0\"}", false)]
    [Arguments("{\"x\": \"rubbish\"}", false)]
    public void ShouldConvertToTrue_WhenStringOfOne(string json, bool expected)
    {
        // Arrange
        var sut = new PlexApi.StringToBool();
        var utf8JsonReader = new Utf8JsonReader(
            Encoding.UTF8.GetBytes(json),
            false,
            new JsonReaderState(new JsonReaderOptions())
        );
        while (utf8JsonReader.Read())
        {
            if (utf8JsonReader.TokenType == JsonTokenType.String)
                break;
        }

        // Act
        var deserializedBoolean = sut.Read(ref utf8JsonReader, typeof(string), new JsonSerializerOptions());

        // Assert
        deserializedBoolean.ShouldBe(expected);
    }

    [Test]
    [Arguments(true, "\"1\"")]
    [Arguments(false, "\"0\"")]
    public void ShouldSerializeBooleanAsPlexString(bool value, string expectedJson)
    {
        // Arrange
        var sut = new PlexApi.StringToBool();

        // Act
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Converters = { sut } });

        // Assert
        json.ShouldBe(expectedJson);
    }
}
