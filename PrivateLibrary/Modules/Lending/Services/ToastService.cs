namespace PrivateLibrary.Modules.Lending.Services;

public enum ToastType { Success, Error, Info }

public record ToastMessage(string Text, ToastType Type = ToastType.Success)
{
    /// <summary>Optional action button label (e.g. "Rückgängig").</summary>
    public string? ActionLabel { get; init; }

    /// <summary>Invoked when the action button is tapped.</summary>
    public Func<Task>? OnAction { get; init; }

    /// <summary>How long the toast stays visible, in milliseconds.</summary>
    public int DurationMs { get; init; } = 3500;
}

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Show(string text, ToastType type = ToastType.Success)
        => OnShow?.Invoke(new ToastMessage(text, type));

    public void Show(ToastMessage message)
        => OnShow?.Invoke(message);

    public void Success(string text) => Show(text, ToastType.Success);
    public void Error(string text)   => Show(text, ToastType.Error);
    public void Info(string text)    => Show(text, ToastType.Info);

    /// <summary>
    /// Shows a toast with an action button (typically "Rückgängig"). Used by the
    /// swipe-to-delete flow so a delete can be undone within the toast window.
    /// </summary>
    public void ShowWithAction(string text, string actionLabel, Func<Task> onAction,
        ToastType type = ToastType.Info, int durationMs = 6000)
        => Show(new ToastMessage(text, type)
        {
            ActionLabel = actionLabel,
            OnAction = onAction,
            DurationMs = durationMs
        });
}
