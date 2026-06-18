// Triggers a browser download for bytes the (authenticated) .NET client fetched.
// Used for game rules / expansion files served under /api, which a plain <a href>
// can't reach because it can't send the bearer token.
(function () {
    window.reviaDownload = function (fileName, base64, contentType) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName || "download";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };

    // Copies text to the clipboard; returns true on success.
    window.reviaCopy = async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    };
})();
