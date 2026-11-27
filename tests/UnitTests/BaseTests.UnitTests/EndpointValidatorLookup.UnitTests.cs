namespace Reaparr.BaseTests.UnitTests;

public sealed class EndpointValidatorLookupUnitTests
{
    [Test]
    public async Task ShouldValidateRequest_WhenOneValidatorExists()
    {
        // Arrange
        var request = new ValidRequest { Value = "valid" };

        // Act
        var result = await Fixture.ValidateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnInvalidResult_WhenOneValidatorRejectsRequest()
    {
        // Arrange
        var request = new ValidRequest { Value = "" };

        // Act
        var result = await Fixture.ValidateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName).ShouldBe([nameof(ValidRequest.Value)]);
    }

    [Test]
    public async Task ShouldReturnNull_WhenNoValidatorExists()
    {
        // Arrange
        var request = new RequestWithoutValidator();

        // Act
        var result = await Fixture.ValidateAsync(request);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ShouldThrowInvalidOperationException_WhenDuplicateValidatorsExist()
    {
        // Arrange
        var request = new RequestWithDuplicates();

        // Act
        var first = await Should.ThrowAsync<InvalidOperationException>(() => Fixture.ValidateAsync(request));
        var second = await Should.ThrowAsync<InvalidOperationException>(() => Fixture.ValidateAsync(request));

        // Assert
        first.Message.ShouldBe(second.Message);
        first.Message.ShouldContain(typeof(RequestWithDuplicates).FullName!);
    }

    public sealed class ValidRequest
    {
        public string Value { get; set; } = "";
    }

    public sealed class ValidRequestValidator : AbstractValidator<ValidRequest>
    {
        public ValidRequestValidator() => RuleFor(x => x.Value).NotEmpty();
    }

    public sealed class RequestWithoutValidator { }

    public sealed class RequestWithDuplicates { }

    public sealed class FirstDuplicateValidator : AbstractValidator<RequestWithDuplicates> { }

    public sealed class SecondDuplicateValidator : AbstractValidator<RequestWithDuplicates> { }

    private sealed class Fixture : BaseEndpointUnitTestBase<Fixture.Endpoint, object>
    {
        public static Task<FluentValidation.Results.ValidationResult?> ValidateAsync<TRequest>(TRequest request)
            where TRequest : class => ValidateEndpointRequestAsync(request, CancellationToken.None);

        public sealed class Endpoint : Endpoint<ValidRequest>
        {
            public override void Configure() { }

            public override Task HandleAsync(ValidRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
