namespace PrivateLibrary.Modules.Lending.Services;

public enum ToastType { Success, Error, Info }

public record ToastMessage(string Text, ToastType Type = ToastType.Success);

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Show(string text, ToastType type = ToastType.Success)
        => OnShow?.Invoke(new ToastMessage(text, type));

    public void Success(string text) => Show(text, ToastType.Success);
    public void Error(string text)   => Show(text, ToastType.Error);
    public void Info(string text)    => Show(text, ToastType.Info);
}
