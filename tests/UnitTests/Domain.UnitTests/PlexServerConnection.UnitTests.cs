namespace Reaparr.Domain.UnitTests;

public class PlexServerConnectionUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(true, true)]
    [Arguments(false, false)]
    public void ShouldIncludeIpv4_WhenComparingConnectionValues(bool ipv4, bool expectedEqual)
    {
        // Arrange
        var connection = new PlexServerConnection
        {
            Id = 1,
            PlexServerId = 1,
            Protocol = "https",
            Address = "plex.example.test",
            Port = 32400,
            Url = "https://plex.example.test:32400",
            Local = false,
            Relay = false,
            Ipv4 = true,
            Ipv6 = false,
            IsCustom = false,
        };
        var other = new PlexServerConnection
        {
            Id = 2,
            PlexServerId = 1,
            Protocol = "https",
            Address = "plex.example.test",
            Port = 32400,
            Url = "https://plex.example.test:32400",
            Local = false,
            Relay = false,
            Ipv4 = ipv4,
            Ipv6 = false,
            IsCustom = false,
        };

        // Act
        var equal = connection.Equals(other);

        // Assert
        equal.ShouldBe(expectedEqual);
        (connection == other).ShouldBe(expectedEqual);
        (connection != other).ShouldBe(!expectedEqual);
        if (expectedEqual)
            connection.GetHashCode().ShouldBe(other.GetHashCode());
    }
}
