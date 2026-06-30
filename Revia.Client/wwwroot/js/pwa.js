// Native-app capabilities for the Revia PWA, exposed to .NET via IJSRuntime.
// Every function degrades gracefully: on a browser that lacks the API it returns a
// neutral value instead of throwing, so the C# side never has to feature-detect.
(function () {
    "use strict";

    // --- Persistent storage -------------------------------------------------
    // The single most important line for a client-only app: without it the OS may
    // evict our IndexedDB (offline cache + outbox) under storage pressure.
    window.reviaPersistStorage = async function () {
        try {
            if (!navigator.storage || !navigator.storage.persist) { return false; }
            if (await navigator.storage.persisted()) { return true; }
            return await navigator.storage.persist();
        } catch {
            return false;
        }
    };

    // Returns { usage, quota, persisted } so the UI can show how much is stored.
    window.reviaStorageEstimate = async function () {
        try {
            const est = navigator.storage && navigator.storage.estimate
                ? await navigator.storage.estimate() : { usage: 0, quota: 0 };
            const persisted = navigator.storage && navigator.storage.persisted
                ? await navigator.storage.persisted() : false;
            return { usage: est.usage || 0, quota: est.quota || 0, persisted: persisted };
        } catch {
            return { usage: 0, quota: 0, persisted: false };
        }
    };

    // --- Badging API --------------------------------------------------------
    // Shows the number of pending (un-synced) mutations on the installed app icon.
    window.reviaSetBadge = function (count) {
        try {
            if (count > 0 && navigator.setAppBadge) { navigator.setAppBadge(count); }
            else if (navigator.clearAppBadge) { navigator.clearAppBadge(); }
        } catch { /* unsupported — ignore */ }
    };

    // --- File System Access -------------------------------------------------
    // Saves text to a real, user-chosen file (e.g. a CSV backup). Falls back to a
    // classic download when the File System Access API is unavailable (Firefox/iOS).
    window.reviaSaveTextFile = async function (suggestedName, text, mimeType) {
        const type = mimeType || "text/csv";
        try {
            if (window.showSaveFilePicker) {
                const handle = await window.showSaveFilePicker({
                    suggestedName: suggestedName,
                    types: [{ description: "Backup", accept: { [type]: ["." + (suggestedName.split(".").pop() || "csv")] } }]
                });
                const writable = await handle.createWritable();
                await writable.write(new Blob([text], { type: type }));
                await writable.close();
                return "saved";
            }
        } catch (e) {
            // AbortError = the user cancelled the picker; report it as such.
            if (e && e.name === "AbortError") { return "cancelled"; }
            // Any other failure falls through to the download path below.
        }

        try {
            const blob = new Blob([text], { type: type });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url; a.download = suggestedName || "backup.csv";
            document.body.appendChild(a); a.click(); document.body.removeChild(a);
            URL.revokeObjectURL(url);
            return "downloaded";
        } catch {
            return "failed";
        }
    };

    // Opens a text file the user picks and returns its contents (or null if cancelled
    // / unsupported). Used to restore a backup.
    window.reviaOpenTextFile = async function () {
        try {
            if (!window.showOpenFilePicker) { return null; }
            const [handle] = await window.showOpenFilePicker({ multiple: false });
            const file = await handle.getFile();
            return await file.text();
        } catch {
            return null;
        }
    };

    // --- OPFS (Origin Private File System) ----------------------------------
    // A fast, private local file store. We use it to keep a rolling local backup of
    // the exported CSV that survives even if IndexedDB is cleared.
    async function opfsHandle(name, create) {
        if (!navigator.storage || !navigator.storage.getDirectory) { return null; }
        const root = await navigator.storage.getDirectory();
        return await root.getFileHandle(name, { create: !!create });
    }

    window.reviaOpfsWrite = async function (name, text) {
        try {
            const handle = await opfsHandle(name, true);
            if (!handle) { return false; }
            const writable = await handle.createWritable();
            await writable.write(text);
            await writable.close();
            return true;
        } catch {
            return false;
        }
    };

    window.reviaOpfsRead = async function (name) {
        try {
            const handle = await opfsHandle(name, false);
            if (!handle) { return null; }
            const file = await handle.getFile();
            return await file.text();
        } catch {
            return null;
        }
    };

    // --- Window Controls Overlay (desktop installed app) --------------------
    // When the title bar is replaced by the app (display_override: window-controls-
    // overlay), expose the reserved drag/inset area to CSS via a class on <html>.
    function updateWco() {
        try {
            const wco = navigator.windowControlsOverlay;
            document.documentElement.classList.toggle(
                "wco-active", !!(wco && wco.visible));
        } catch { /* ignore */ }
    }
    if (navigator.windowControlsOverlay) {
        navigator.windowControlsOverlay.addEventListener("geometrychange", updateWco);
        updateWco();
    }
})();
