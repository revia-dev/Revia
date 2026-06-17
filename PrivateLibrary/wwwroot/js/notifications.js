// Notifications interop for Private Library.
//
// Bridges the browser Notifications API and document focus/visibility state to
// .NET. The C# NotificationCenter component decides — based on whether the page
// is currently focused — whether to render an in-app toast (page focused) or to
// raise an operating-system notification via this module (page in background).
//
// Everything degrades gracefully: if the Notifications API is unavailable or
// permission is denied, the methods resolve without throwing so the caller can
// always fall back to the in-app notification.

(() => {
    const api = {};
    let clickHandler = null;
    const openNotifications = new Map();
    let nextTag = 0;

    function notificationsSupported() {
        return typeof window !== 'undefined' && 'Notification' in window;
    }

    // True when the page is actually visible AND focused. We use this to route
    // a notification to the in-app channel instead of the OS channel.
    api.isPageFocused = function () {
        try {
            return document.visibilityState === 'visible' && document.hasFocus();
        } catch {
            return true;
        }
    };

    api.permissionStatus = function () {
        if (!notificationsSupported()) {
            return 'unsupported';
        }
        return Notification.permission; // 'default' | 'granted' | 'denied'
    };

    api.requestPermission = async function () {
        if (!notificationsSupported()) {
            return 'unsupported';
        }
        if (Notification.permission === 'granted' || Notification.permission === 'denied') {
            return Notification.permission;
        }
        try {
            const result = await Notification.requestPermission();
            return result;
        } catch {
            return Notification.permission;
        }
    };

    // Registers the .NET object that receives notification-click callbacks.
    api.registerClickHandler = function (dotNetRef) {
        clickHandler = dotNetRef;
    };

    api.disposeClickHandler = function () {
        clickHandler = null;
        openNotifications.forEach(n => {
            try { n.close(); } catch { /* ignore */ }
        });
        openNotifications.clear();
    };

    // Shows an OS notification. Returns true if one was actually shown.
    // navigateUrl (optional) is passed back to .NET on click so the app can route.
    api.showDevice = function (title, body, icon, navigateUrl) {
        if (!notificationsSupported() || Notification.permission !== 'granted') {
            return false;
        }

        try {
            const tag = 'pl-' + (nextTag++);
            const notification = new Notification(title, {
                body: body || '',
                icon: icon || '/images/icon-192.png',
                badge: '/images/icon-192.png',
                tag,
                renotify: false,
            });

            openNotifications.set(tag, notification);

            notification.onclick = function (event) {
                event.preventDefault();
                try { window.focus(); } catch { /* ignore */ }
                if (clickHandler && navigateUrl) {
                    clickHandler.invokeMethodAsync('OnNotificationClicked', navigateUrl);
                }
                notification.close();
            };

            notification.onclose = function () {
                openNotifications.delete(tag);
            };

            return true;
        } catch {
            return false;
        }
    };

    window.privateLibraryNotifications = api;
})();
