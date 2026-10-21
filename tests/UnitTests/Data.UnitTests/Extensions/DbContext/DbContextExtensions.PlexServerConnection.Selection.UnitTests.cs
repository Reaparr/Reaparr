namespace Reaparr.Data.UnitTests;

public class PlexServerConnectionSelectionUnitTests
{
    public static IEnumerable<Func<(PlexServer, int)>> SelectionCases()
    {
        // An online preference outranks HTTPS and local connections.
        yield return SelectionCase(101, 101,
        [
            (102, "https://secure.example.com:32400", false, true),
            (103, "http://127.0.0.1:32400", true, true),
            (101, "http://preferred.example.com:32400", false, true),
        ]);
        // An offline preference must not be selected.
        yield return SelectionCase(202, 201,
        [
            (201, "https://offline.example.com:32400", false, false),
            (202, "https://online.example.com:32400", false, true),
        ]);
        // A missing preference falls through to the normal priority order.
        yield return SelectionCase(302, 999,
        [
            (301, "http://127.0.0.1:32400", true, true),
            (302, "https://online.example.com:32400", false, true),
        ]);
        // HTTPS outranks local and other direct connections; offline HTTPS is ignored.
        yield return SelectionCase(402, 0,
        [
            (400, "https://offline.example.com:32400", false, false),
            (401, "http://127.0.0.1:32400", true, true),
            (402, "https://online.example.com:32400", false, true),
            (403, "http://direct.example.com:32400", false, true),
        ]);
        // Local outranks the public-address match and other direct connections.
        yield return SelectionCase(503, 0,
        [
            (500, "http://127.0.0.2:32400", true, false),
            (501, "http://other.example.com:32400", false, true),
            (502, "http://public.example.com:32400", false, true),
            (503, "http://127.0.0.1:32400", true, true),
        ]);
        // A public-address match outranks other direct connections.
        yield return SelectionCase(602, 0,
        [
            (600, "http://public.example.com:32400", false, false),
            (601, "http://other.example.com:32400", false, true),
            (602, "http://public.example.com:32400", false, true),
            (603, "http://relay.plex.direct:32400", false, true),
        ]);
        // An online direct connection outranks an earlier PlexTv connection.
        yield return SelectionCase(703, 0,
        [
            (701, "http://first.plex.direct:32400", false, true),
            (702, "http://offline.example.com:32400", false, false),
            (703, "http://direct.example.com:32400", false, true),
            (704, "http://second.plex.direct:32400", false, true),
        ]);
        // With no direct connection, preserve the first-online fallback.
        yield return SelectionCase(801, 0,
        [
            (800, "http://offline.plex.direct:32400", false, false),
            (801, "http://first.plex.direct:32400", false, true),
            (802, "http://second.plex.direct:32400", false, true),
        ]);
    }

    [Test]
    [MethodDataSource(nameof(SelectionCases))]
    public void ShouldChooseByExistingPriorityWithoutChangingPreloadedState(PlexServer server, int expectedConnectionId)
    {
        // Arrange
        var beforeOrder = server.PlexServerConnections.Select(x => x.Id).ToArray();
        var beforeStatuses = server.PlexServerConnections.Select(x => x.LatestConnectionStatus).ToArray();
        var beforeStatusState = beforeStatuses
            .Select(x => (x?.IsSuccessful, x?.StatusCode, x?.StatusMessage, x?.LastChecked))
            .ToArray();
        var expectedStatus = server.PlexServerConnections.Single(x => x.Id == expectedConnectionId).LatestConnectionStatus;
        var beforePreferredId = server.PreferredConnectionId;

        // Act
        var result = server.ChoosePlexServerConnection();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Id.ShouldBe(expectedConnectionId);
        server.PlexServerConnections.Select(x => x.Id).ShouldBe(beforeOrder);
        server.PlexServerConnections.Select(x => x.LatestConnectionStatus).ShouldBe(beforeStatuses);
        server.PlexServerConnections
            .Select(x => x.LatestConnectionStatus)
            .Select(x => (x?.IsSuccessful, x?.StatusCode, x?.StatusMessage, x?.LastChecked))
            .ShouldBe(beforeStatusState);
        result.Value.LatestConnectionStatus.ShouldBeSameAs(expectedStatus);
        server.PreferredConnectionId.ShouldBe(beforePreferredId);
    }

    [Test]
    public void ShouldReturn504WhenAllConnectionsAreOfflineOrUnchecked()
    {
        // Arrange
        var offlineServer = CreateServer(72109);
        offlineServer.PlexServerConnections =
        [
            Connection(901, "http://offline-one.example.com:32400", false, false),
            Connection(902, "http://offline-two.example.com:32400", false, false),
        ];
        var uncheckedServer = CreateServer(72110);
        uncheckedServer.PlexServerConnections = [Connection(903, "http://unchecked.example.com:32400", false, null)];

        // Act
        var offlineResult = offlineServer.ChoosePlexServerConnection();
        var uncheckedResult = uncheckedServer.ChoosePlexServerConnection();

        // Assert
        offlineResult.IsFailed.ShouldBeTrue();
        offlineResult.Errors.Count.ShouldBe(1);
        offlineResult.ToResult().Has504GatewayTimeoutError().ShouldBeTrue();
        uncheckedResult.IsFailed.ShouldBeTrue();
        uncheckedResult.Errors.Count.ShouldBe(1);
        uncheckedResult.ToResult().Has504GatewayTimeoutError().ShouldBeTrue();
    }

    [Test]
    public void ShouldKeepDisabledAndEmptyServerFailuresDistinct()
    {
        // Arrange
        var disabledServer = CreateServer(72111);
        disabledServer.IsEnabled = false;
        disabledServer.PlexServerConnections = [Connection(1001, "http://online.example.com:32400", false, true)];
        var emptyServer = CreateServer(72112);

        // Act
        var disabledResult = disabledServer.ChoosePlexServerConnection();
        var emptyResult = emptyServer.ChoosePlexServerConnection();

        // Assert
        disabledResult.IsFailed.ShouldBeTrue();
        disabledResult.Errors.Count.ShouldBe(1);
        disabledResult.ToResult().Has504GatewayTimeoutError().ShouldBeFalse();
        emptyResult.IsFailed.ShouldBeTrue();
        emptyResult.Errors.Count.ShouldBe(1);
        emptyResult.ToResult().Has504GatewayTimeoutError().ShouldBeFalse();
    }

    private static Func<(PlexServer, int)> SelectionCase(
        int expectedConnectionId,
        int preferredId,
        (int Id, string Url, bool Local, bool? Online)[] connections
    )
    {
        return () =>
        {
            var server = CreateServer(expectedConnectionId);
            server.PreferredConnectionId = preferredId;
            server.PlexServerConnections = connections.Select(x => Connection(x.Id, x.Url, x.Local, x.Online)).ToList();
            return (server, expectedConnectionId);
        };
    }

    private static PlexServer CreateServer(int seed)
    {
        var server = FakeData.GetPlexServer(new Seed(seed)).Generate();
        // Override this instance without changing the shared Faker's rule.
        server.UpdateInitProperty(nameof(PlexServer.PublicAddress), "public.example.com");
        return server;
    }

    private static PlexServerConnection Connection(int id, string url, bool local, bool? online)
    {
        var uri = new Uri(url);
        return new PlexServerConnection
        {
            Id = id,
            Protocol = uri.Scheme,
            Address = uri.Host,
            Port = uri.Port,
            Url = url,
            Local = local,
            Relay = false,
            IPv4 = uri.HostNameType == UriHostNameType.IPv4,
            IPv6 = uri.HostNameType == UriHostNameType.IPv6,
            IsCustom = false,
            PlexServerId = 0,
            LatestConnectionStatus = online is bool isSuccessful
                ? new PlexServerStatus
                {
                    Id = id + 10000,
                    IsSuccessful = isSuccessful,
                    StatusCode = isSuccessful ? 200 : 503,
                    StatusMessage = isSuccessful ? "OK" : "Offline",
                    LastChecked = DateTime.UnixEpoch,
                    PlexServerId = 0,
                    PlexServerConnectionId = id,
                }
                : null,
        };
    }
}
