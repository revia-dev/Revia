// Camera QR scanner built on the vendored jsQR. Opens the rear camera into a <video>
// inside the given container, scans each frame, and invokes the .NET callback
// OnScanned(text) on the first successful decode, then stops. reviaScanner.stop()
// cancels. Requires a secure context (HTTPS or localhost) for camera access.
(function () {
    let stream = null;
    let raf = null;
    let video = null;
    let canvas = null;

    function stop() {
        if (raf) { cancelAnimationFrame(raf); raf = null; }
        if (stream) { stream.getTracks().forEach(t => t.stop()); stream = null; }
        if (video && video.parentNode) { video.parentNode.removeChild(video); }
        video = null; canvas = null;
    }

    window.reviaScanner = {
        // Returns "" on success (the callback fires), or an error message string.
        start: async function (containerId, dotNetRef) {
            stop();
            const container = document.getElementById(containerId);
            if (!container) { return "Kein Container."; }
            if (!navigator.mediaDevices || !window.jsQR) { return "Kamera/Scanner nicht verfügbar."; }

            try {
                stream = await navigator.mediaDevices.getUserMedia({
                    video: { facingMode: "environment" }, audio: false
                });
            } catch (e) {
                return "Kamerazugriff verweigert oder nicht verfügbar.";
            }

            video = document.createElement("video");
            video.setAttribute("playsinline", "true");
            video.style.width = "100%";
            video.style.borderRadius = "12px";
            container.appendChild(video);
            video.srcObject = stream;
            await video.play();

            canvas = document.createElement("canvas");
            const ctx = canvas.getContext("2d", { willReadFrequently: true });

            const tick = () => {
                if (!video || video.readyState !== video.HAVE_ENOUGH_DATA) {
                    raf = requestAnimationFrame(tick);
                    return;
                }
                canvas.width = video.videoWidth;
                canvas.height = video.videoHeight;
                ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
                const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
                const code = window.jsQR(img.data, img.width, img.height, { inversionAttempts: "dontInvert" });
                if (code && code.data) {
                    const text = code.data;
                    stop();
                    dotNetRef.invokeMethodAsync("OnScanned", text);
                    return;
                }
                raf = requestAnimationFrame(tick);
            };
            raf = requestAnimationFrame(tick);
            return "";
        },
        stop: stop
    };
})();
