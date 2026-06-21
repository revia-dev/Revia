using Revia.Services;

namespace Revia.Tests;

public class NotificationRoutingTests
{
    private static AppNotification Sample(bool allowDevice = true)
        => new() { Title = "Test", AllowDevice = allowDevice };

    [Fact]
    public void FocusedPage_AlwaysRoutesInApp()
    {
        var channel = NotificationRouter.Route(Sample(), pageFocused: true, permissionGranted: true);
        Assert.Equal(NotificationChannel.InApp, channel);
    }

    [Fact]
    public void BackgroundPage_WithPermission_RoutesToDevice()
    {
        var channel = NotificationRouter.Route(Sample(), pageFocused: false, permissionGranted: true);
        Assert.Equal(NotificationChannel.Device, channel);
    }

    [Fact]
    public void BackgroundPage_WithoutPermission_FallsBackToInApp()
    {
        var channel = NotificationRouter.Route(Sample(), pageFocused: false, permissionGranted: false);
        Assert.Equal(NotificationChannel.InApp, channel);
    }

    [Fact]
    public void BackgroundPage_DeviceDisallowed_StaysInApp()
    {
        var channel = NotificationRouter.Route(Sample(allowDevice: false), pageFocused: false, permissionGranted: true);
        Assert.Equal(NotificationChannel.InApp, channel);
    }

    [Theory]
    // pageFocused, permission, allowDevice -> expectDevice
    [InlineData(true,  true,  true,  false)]
    [InlineData(false, true,  true,  true)]
    [InlineData(false, false, true,  false)]
    [InlineData(false, true,  false, false)]
    [InlineData(true,  false, false, false)]
    public void RoutingMatrix(bool pageFocused, bool permissionGranted, bool allowDevice, bool expectDevice)
    {
        var channel = NotificationRouter.Route(Sample(allowDevice), pageFocused, permissionGranted);
        var expected = expectDevice ? NotificationChannel.Device : NotificationChannel.InApp;
        Assert.Equal(expected, channel);
    }
}

public class NotificationFactoryTests
{
    [Fact]
    public void LendingCreated_HasSuccessSeverity_AndNavigatesToEntry()
    {
        var n = AppNotification.LendingCreated("Bohrmaschine", "Anna", 42);

        Assert.Equal(NotificationSeverity.Success, n.Severity);
        Assert.Equal(NotificationKind.LendingCreated, n.Kind);
        Assert.Equal("/lending/42", n.NavigateUrl);
        Assert.Contains("Anna", n.Body);
        Assert.Contains("Bohrmaschine", n.Body);
    }

    [Fact]
    public void LendingDue_Overdue_IsErrorAndAllowsDevice()
    {
        var due = new DateTime(2026, 1, 1);
        var n = AppNotification.LendingDue("Leiter", due, 7, overdue: true);

        Assert.Equal(NotificationSeverity.Error, n.Severity);
        Assert.Equal(NotificationKind.LendingDue, n.Kind);
        Assert.True(n.AllowDevice);
        Assert.Equal("/lending/7", n.NavigateUrl);
        Assert.Contains("überfällig", n.Body);
    }

    [Fact]
    public void LendingDue_NotOverdue_IsWarning()
    {
        var n = AppNotification.LendingDue("Leiter", DateTime.Today, 7, overdue: false);
        Assert.Equal(NotificationSeverity.Warning, n.Severity);
    }

    [Fact]
    public void Error_SuppressesDeviceChannel()
    {
        // Errors are transient/contextual and must never escape to the OS.
        var n = AppNotification.Error("Fehlgeschlagen");
        Assert.False(n.AllowDevice);
        Assert.Equal(NotificationChannel.InApp,
            NotificationRouter.Route(n, pageFocused: false, permissionGranted: true));
    }

    [Fact]
    public async Task Service_RaisesEvent_WithSameNotification()
    {
        var service = new NotificationService();
        AppNotification? received = null;
        service.OnNotify += n => { received = n; return Task.CompletedTask; };

        var sent = AppNotification.Info("Hallo", "Welt");
        await service.Notify(sent);

        Assert.NotNull(received);
        Assert.Equal(sent.Id, received!.Id);
    }

    [Fact]
    public async Task Service_NoSubscribers_DoesNotThrow()
    {
        var service = new NotificationService();
        await service.Info("kein Abonnent");
    }
}
