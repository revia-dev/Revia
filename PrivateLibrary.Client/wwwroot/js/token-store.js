// Persistent key/value store for auth tokens, backed by localStorage.
// Exposed on window (not an ES module) so C# JSInterop can call it by name
// without depending on the fingerprinted importmap.
window.reviaTokenStore = {
    get: function (key) {
        try { return localStorage.getItem(key); } catch { return null; }
    },
    set: function (key, value) {
        try { localStorage.setItem(key, value); } catch { /* ignore */ }
    },
    remove: function (key) {
        try { localStorage.removeItem(key); } catch { /* ignore */ }
    }
};
