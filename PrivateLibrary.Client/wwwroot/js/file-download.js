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

    // Renders text as a QR code and returns an SVG data: URL (crisp at any size).
    // Uses the vendored qrcode-generator (window.qrcode). Auto-sizes the QR version
    // to the payload; falls back to an empty string if generation fails.
    window.reviaQr = function (text) {
        try {
            // typeNumber 0 = auto-detect smallest version that fits; "M" = ~15% ECC.
            const qr = window.qrcode(0, "M");
            qr.addData(text || "");
            qr.make();
            // 4px module, 4-module quiet zone, scalable SVG.
            return "data:image/svg+xml;utf8," + encodeURIComponent(qr.createSvgTag({ cellSize: 4, margin: 16, scalable: true }));
        } catch {
            return "";
        }
    };

    // Invokes the Web Share API (system share sheet incl. AirDrop where available).
    // Returns true if shared, false if unavailable/cancelled.
    window.reviaShare = async function (title, text) {
        if (!navigator.share) { return false; }
        try {
            await navigator.share({ title: title, text: text });
            return true;
        } catch {
            return false;
        }
    };

    // Makes a list element drag-sortable (touch + mouse) via the vendored SortableJS,
    // and reports the new order back to .NET. The .NET ref must expose
    // [JSInvokable] OnReorder(oldIndex, newIndex). Returns a disposer handle id.
    const _sortables = {};
    window.reviaSortable = {
        attach: function (element, dotNetRef, callbackName, handleClass) {
            if (!element || !window.Sortable) { return; }
            const s = window.Sortable.create(element, {
                animation: 150,
                handle: handleClass ? ("." + handleClass) : undefined,
                // Long-press to start on touch so normal scrolling still works.
                delay: 150,
                delayOnTouchOnly: true,
                onEnd: function (evt) {
                    if (evt.oldIndex !== evt.newIndex) {
                        dotNetRef.invokeMethodAsync(callbackName || "OnReorder", evt.oldIndex, evt.newIndex);
                    }
                }
            });
            const id = "sortable-" + Math.random().toString(36).slice(2);
            _sortables[id] = s;
            element.dataset.sortableId = id;
            return id;
        },
        detach: function (id) {
            if (id && _sortables[id]) { _sortables[id].destroy(); delete _sortables[id]; }
        }
    };
})();
