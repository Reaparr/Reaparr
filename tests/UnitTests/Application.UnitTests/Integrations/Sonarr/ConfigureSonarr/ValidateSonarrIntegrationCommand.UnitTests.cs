using System.Collections.Concurrent;
using System.Net;

namespace Reaparr.Application.UnitTests;

public class ValidateSonarrIntegrationCommandUnitTests : BaseCommandUnitTest<ValidateSonarrIntegrationCommand>
{
    [Test]
    public async Task ShouldMergeBothScopedResourceValidationFailures()
    {
        // Arrange
        var integrationId = Guid.Parse("00000000-0000-0000-0000-000000062642");
        var downloadClient = new SonarrDownloadContractDTO
        {
            Fields = [new SonarrDownloadContractCreateFieldDTO { Name = "apiKey", Value = "qbt-key" }],
        };
        var indexer = new SonarrIndexerContractDTO
        {
            Fields = [new SonarrIndexerContractFieldDTO { Name = "apiKey", Value = "torznab-key" }],
        };
        var handler = new FailingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://sonarr.test") };

        Mock.Mock<ISonarrHttpClientFactory>()
            .Setup(x => x.CreateAsync(integrationId))
            .ReturnsAsync(Result.Ok(client))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(
            new ValidateSonarrIntegrationCommand(integrationId, downloadClient, indexer)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(4);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.ShouldContain(x => x.Path == "/api/v3/downloadclient/test" && x.Body.Contains("qbt-key"));
        handler.Requests.ShouldContain(x => x.Path == "/api/v3/indexer/test" && x.Body.Contains("torznab-key"));
        Mock.Mock<ISonarrHttpClientFactory>().Verify();
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        public ConcurrentBag<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(
                (request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken))
            );
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                RequestMessage = request,
                Content = new StringContent("validation failed"),
            };
        }
    }
}
