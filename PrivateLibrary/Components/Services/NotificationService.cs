namespace PrivateLibrary.Services;

/// <summary>
/// Visual severity of a notification. Drives icon and colour in the in-app
/// channel and is otherwise informational for the OS channel.
/// </summary>
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Semantic kind of a notification. Lets callers raise a meaningful event
/// without hand-crafting title/body/severity each time, and gives the UI a
/// stable category to group or filter by later.
/// </summary>
public enum NotificationKind
{
    /// <summary>Generic message — uses the title/body as given.</summary>
    General,
    /// <summary>An item was lent out.</summary>
    LendingCreated,
    /// <summary>A lending was returned.</summary>
    LendingReturned,
    /// <summary>A lending is due soon or overdue.</summary>
    LendingDue,
    /// <summary>A reminder the user scheduled or the system raised.</summary>
    Reminder,
    /// <summary>Something was created (game, wish, person, measurement, …).</summary>
    ItemAdded,
    /// <summary>An operation failed.</summary>
    Failure
}

/// <summary>
/// A single notification request. Use the static factory helpers for the common
/// cases, or construct directly for full control.
/// </summary>
public sealed record AppNotification
{
    public required string Title { get; init; }
    public string? Body { get; init; }
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
    public NotificationKind Kind { get; init; } = NotificationKind.General;

    /// <summary>Optional in-app navigation target. Clicking the notification
    /// (in-app or OS) routes here.</summary>
    public string? NavigateUrl { get; init; }

    /// <summary>Optional icon URL for the OS notification. Falls back to the app icon.</summary>
    public string? IconUrl { get; init; }

    /// <summary>How long the in-app toast stays visible, in milliseconds.</summary>
    public int DurationMs { get; init; } = 5000;

    /// <summary>
    /// When true the notification is allowed to surface as an OS/device
    /// notification while the page is in the background. Transient confirmations
    /// (e.g. "saved") usually set this to false so they never escape the tab.
    /// </summary>
    public bool AllowDevice { get; init; } = true;

    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

    public static AppNotification Info(string title, string? body = null, string? navigateUrl = null)
        => new() { Title = title, Body = body, Severity = NotificationSeverity.Info, NavigateUrl = navigateUrl };

    public static AppNotification Success(string title, string? body = null, string? navigateUrl = null)
        => new() { Title = title, Body = body, Severity = NotificationSeverity.Success, NavigateUrl = navigateUrl, Kind = NotificationKind.ItemAdded };

    public static AppNotification Warning(string title, string? body = null, string? navigateUrl = null)
        => new() { Title = title, Body = body, Severity = NotificationSeverity.Warning, NavigateUrl = navigateUrl };

    public static AppNotification Error(string title, string? body = null)
        => new() { Title = title, Body = body, Severity = NotificationSeverity.Error, Kind = NotificationKind.Failure, AllowDevice = false };

    // --- Domain-specific helpers (the "several kinds of notifications") ---

    public static AppNotification LendingCreated(string itemTitle, string borrowerName, int lendingId)
        => new()
        {
            Title = "Ausleihe angelegt",
            Body = $"„{itemTitle}“ wurde an {borrowerName} verliehen.",
            Severity = NotificationSeverity.Success,
            Kind = NotificationKind.LendingCreated,
            NavigateUrl = $"/lending/{lendingId}"
        };

    public static AppNotification LendingReturned(string itemTitle, int lendingId)
        => new()
        {
            Title = "Rückgabe verbucht",
            Body = $"„{itemTitle}“ wurde als zurückgegeben markiert.",
            Severity = NotificationSeverity.Success,
            Kind = NotificationKind.LendingReturned,
            NavigateUrl = $"/lending/{lendingId}"
        };

    public static AppNotification LendingDue(string itemTitle, DateTime dueDate, int lendingId, bool overdue)
        => new()
        {
            Title = overdue ? "Ausleihe überfällig" : "Rückgabe steht an",
            Body = overdue
                ? $"„{itemTitle}“ ist seit {dueDate:dd.MM.yyyy} überfällig."
                : $"„{itemTitle}“ ist am {dueDate:dd.MM.yyyy} fällig.",
            Severity = overdue ? NotificationSeverity.Error : NotificationSeverity.Warning,
            Kind = NotificationKind.LendingDue,
            NavigateUrl = $"/lending/{lendingId}",
            AllowDevice = true,
            DurationMs = 8000
        };

    public static AppNotification ItemAdded(string title, string? body = null, string? navigateUrl = null)
        => new()
        {
            Title = title,
            Body = body,
            Severity = NotificationSeverity.Success,
            Kind = NotificationKind.ItemAdded,
            NavigateUrl = navigateUrl
        };
}

/// <summary>The channel a notification should be delivered through.</summary>
public enum NotificationChannel
{
    /// <summary>Render an in-app toast (page is focused, or device unavailable).</summary>
    InApp,
    /// <summary>Raise an OS/device notification (page is in the background).</summary>
    Device
}

/// <summary>
/// Pure routing rules shared by the UI and tests. Keeping the decision here (not
/// inline in the component) makes the focus/permission behaviour verifiable.
/// </summary>
public static class NotificationRouter
{
    /// <summary>
    /// Decides where a notification goes. Device only when the page is NOT focused,
    /// the notification permits device delivery, and the browser permission was
    /// granted. Everything else falls back to the in-app channel.
    /// </summary>
    public static NotificationChannel Route(
        AppNotification notification,
        bool pageFocused,
        bool permissionGranted)
    {
        if (!pageFocused && notification.AllowDevice && permissionGranted)
        {
            return NotificationChannel.Device;
        }

        return NotificationChannel.InApp;
    }
}

/// <summary>
/// Central, app-wide entry point for raising notifications. Inject this anywhere
/// (services or components) and call <see cref="Notify"/> (or a helper). A single
/// <c>NotificationCenter</c> component subscribes and routes each notification to
/// either the in-app or the device channel depending on page focus.
/// </summary>
public sealed class NotificationService
{
    /// <summary>Raised when a notification should be presented.</summary>
    public event Func<AppNotification, Task>? OnNotify;

    public Task Notify(AppNotification notification)
        => OnNotify?.Invoke(notification) ?? Task.CompletedTask;

    public Task Info(string title, string? body = null, string? navigateUrl = null)
        => Notify(AppNotification.Info(title, body, navigateUrl));

    public Task Success(string title, string? body = null, string? navigateUrl = null)
        => Notify(AppNotification.Success(title, body, navigateUrl));

    public Task Warning(string title, string? body = null, string? navigateUrl = null)
        => Notify(AppNotification.Warning(title, body, navigateUrl));

    public Task Error(string title, string? body = null)
        => Notify(AppNotification.Error(title, body));
}
