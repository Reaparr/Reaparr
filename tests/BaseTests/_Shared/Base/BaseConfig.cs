namespace Reaparr.BaseTests;

public abstract class BaseConfig<T> where T : new()
{
    public static T FromOptions(Action<T>? action = null, T? defaultValue = default)
    {
        var config = defaultValue ?? new T();
        action?.Invoke(config);
        return config;
    }
}
