namespace Reaparr.BaseTests;

public abstract class BaseCommandUnitTest<TCommand> : BaseUnitTest
    where TCommand : class
{
    // Each closed TCommand type requires its own validator and handler cache entries.
    // ReSharper disable StaticMemberInGenericType
    private static readonly Lazy<Type> _validatorType = new(() => ResolveRelatedType("CommandValidator"));
    private static readonly Lazy<Type> _handlerType = new(() => ResolveRelatedType("CommandHandler"));

    private static Type ResolveRelatedType(string replacement)
    {
        var commandType = typeof(TCommand);
        var fullName = commandType.FullName!.Replace("Command", replacement);
        var shortName = commandType.Name.Replace("Command", replacement);
        return commandType.Assembly.GetType(fullName)
            ?? AppDomain
                .CurrentDomain.GetAssemblies()
                .Where(x => !x.IsDynamic)
                .SelectMany(x => x.GetTypes())
                .SingleOrDefault(t => t.Name == shortName)
            ?? throw new InvalidOperationException(
                $"Related type '{fullName}' not found for command: {commandType.FullName}."
            );
    }

    private static IValidator<TCommand> GetValidator() =>
        (IValidator<TCommand>)Activator.CreateInstance(_validatorType.Value)!;

    /// <summary>
    /// Use this method to test the execution of a command handler, including the corresponding validator.
    /// </summary>
    /// <param name="command"> The ICommand to execute inside the handler.</param>
    protected async Task<Result> TestHandlerExecuteAsync(TCommand command)
    {
        if ((object?)command is null)
            return Result.Fail("Command cannot be null");

        var validator = GetValidator();
        var validationResult = await validator.ValidateAsync(command, CancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToResult();

        var handler = Mock.Create(_handlerType.Value);

        // dynamically cast the handler to ICommandHandler<TCommand, TResponse>
        // to avoid needing to know the exact response type at compile time
        dynamic dynHandler = handler;

        // we still strongly type the command and CancellationToken
        Result result = await dynHandler.ExecuteAsync(command, CancellationToken);
        return result;
    }

    /// <summary>
    /// Use this method to test the execution of a command handler, including the corresponding validator.
    /// </summary>
    /// <param name="command"> The ICommand to execute inside the handler.</param>
    protected async Task<Result<TResponse>> TestHandlerExecuteAsync<TResponse>(TCommand command)
    {
        var validator = GetValidator();
        var validationResult = await validator.ValidateAsync(command, CancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToResult();

        var handler = Mock.Create(_handlerType.Value);

        // dynamically cast the handler to ICommandHandler<TCommand, TResponse>
        // to avoid needing to know the exact response type at compile time
        dynamic dynHandler = handler;

        // we still strongly type the command and CancellationToken
        Result<TResponse> result = await dynHandler.ExecuteAsync(command, CancellationToken);
        return result;
    }

    public override void Dispose()
    {
        base.Dispose();
        Mock.Dispose();
    }
}
