// IndexedDB-backed key/value store for the offline data layer.
//
// Two logical uses, both as object stores in one database:
//   - "cache"  : last-known server data per collection (read-cache), keyed by
//                "<collection>" -> JSON array (or "<collection>:<id>" for single).
//   - "outbox" : queued offline mutations, keyed by client mutation id.
//
// Exposed on window (plain global, not an ES module) so C# JSInterop can call it
// without depending on the fingerprinted import map. Kept deliberately small;
// all domain logic lives in C#.

(function () {
    const DB_NAME = "revia-offline";
    const DB_VERSION = 1;
    const STORE_CACHE = "cache";
    const STORE_OUTBOX = "outbox";

    let dbPromise = null;

    function openDb() {
        if (dbPromise) return dbPromise;
        dbPromise = new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, DB_VERSION);
            req.onupgradeneeded = () => {
                const db = req.result;
                if (!db.objectStoreNames.contains(STORE_CACHE)) {
                    db.createObjectStore(STORE_CACHE);
                }
                if (!db.objectStoreNames.contains(STORE_OUTBOX)) {
                    db.createObjectStore(STORE_OUTBOX, { keyPath: "id" });
                }
            };
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
        return dbPromise;
    }

    function tx(storeName, mode) {
        return openDb().then(db => db.transaction(storeName, mode).objectStore(storeName));
    }

    function reqToPromise(request) {
        return new Promise((resolve, reject) => {
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    window.reviaOffline = {
        // ---- read cache (string values; C# stores JSON) ----
        cacheGet: async function (key) {
            const store = await tx(STORE_CACHE, "readonly");
            const v = await reqToPromise(store.get(key));
            return v === undefined ? null : v;
        },
        cacheSet: async function (key, json) {
            const store = await tx(STORE_CACHE, "readwrite");
            await reqToPromise(store.put(json, key));
        },
        cacheRemove: async function (key) {
            const store = await tx(STORE_CACHE, "readwrite");
            await reqToPromise(store.delete(key));
        },

        // ---- outbox (objects with an "id" key) ----
        outboxAdd: async function (entryJson) {
            const entry = JSON.parse(entryJson);
            const store = await tx(STORE_OUTBOX, "readwrite");
            await reqToPromise(store.put(entry));
        },
        outboxAll: async function () {
            const store = await tx(STORE_OUTBOX, "readonly");
            const all = await reqToPromise(store.getAll());
            // Oldest first (FIFO) by createdAt.
            all.sort((a, b) => (a.createdAt || "").localeCompare(b.createdAt || ""));
            return JSON.stringify(all);
        },
        outboxRemove: async function (id) {
            const store = await tx(STORE_OUTBOX, "readwrite");
            await reqToPromise(store.delete(id));
        },
        outboxCount: async function () {
            const store = await tx(STORE_OUTBOX, "readonly");
            return await reqToPromise(store.count());
        },

        // ---- connectivity ----
        // Probes the server with a short timeout. navigator.onLine only tells us
        // whether the device has *any* network — it returns true when on WiFi even if
        // the home server is unreachable. A real HTTP probe avoids the multi-second
        // TCP timeout that would otherwise stall every API call on startup.
        isOnline: async function () {
            if (!navigator.onLine) return false;
            try {
                const ctrl = new AbortController();
                const id = setTimeout(() => ctrl.abort(), 2500);
                const r = await fetch('/api/ping', { method: 'HEAD', cache: 'no-store', signal: ctrl.signal });
                clearTimeout(id);
                return r.ok;
            } catch {
                return false;
            }
        },
        // Registers a .NET callback fired on online/offline transitions.
        // We also re-probe the server whenever the browser's online event fires,
        // because that event only means the NIC is up, not that our server is back.
        registerConnectivity: function (dotNetRef) {
            const probe = async () => {
                const online = await window.reviaOffline.isOnline();
                dotNetRef.invokeMethodAsync("OnConnectivityChanged", online);
            };
            window.addEventListener("online", probe);
            window.addEventListener("offline", () => dotNetRef.invokeMethodAsync("OnConnectivityChanged", false));
        }
    };
})();
