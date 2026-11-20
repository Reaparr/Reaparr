using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

namespace Reaparr.Settings.Contracts;

public abstract record BaseSettingsModule<TModel> : INotifyPropertyChanged
    where TModel : class
{
    protected BaseSettingsModule()
    {
        _moduleUpdatedSubject = new BehaviorSubject<TModel>((this as TModel)!);
    }

    private readonly BehaviorSubject<TModel> _moduleUpdatedSubject;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        _moduleUpdatedSubject.OnNext((this as TModel)!);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// This has to be a public method otherwise it will be marked as a JSON serializable property or used to compare with in assertions.
    /// </summary>
    /// <returns></returns>
    [JsonIgnore]
    public IObservable<TModel> HasChanged => _moduleUpdatedSubject.AsObservable();

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null!)
    {
        if (Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    public virtual void Update(TModel sourceSettings)
    {
        var hasChanged = false;

        foreach (var prop in typeof(TModel).GetProperties())
        {
            var sourceProp = sourceSettings.GetType().GetProperty(prop.Name);
            var sourceValue = sourceProp?.GetValue(sourceSettings, null);
            var targetProp = GetType().GetProperty(prop.Name);
            var targetValue = targetProp?.GetValue(this, null);

            if (prop.Name == nameof(HasChanged))
                continue;

            if (sourceValue != null && !sourceValue.Equals(targetValue) && targetProp is not null)
            {
                targetProp.SetValue(this, sourceValue);
                hasChanged = true;
            }
        }

        if (hasChanged)
            OnPropertyChanged();
    }
}
