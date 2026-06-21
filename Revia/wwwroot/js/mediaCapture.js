// Live webcam capture. Opens a fullscreen overlay with a live camera preview
// and returns the captured frame as a JPEG data URL (or null if cancelled /
// the camera is unavailable). Works on desktop and mobile (getUserMedia).
window.mediaCapture = {
    openCamera: function () {
        return new Promise(async (resolve) => {
            let stream = null;
            let settled = false;

            const overlay = document.createElement('div');
            overlay.className = 'mc-overlay';
            overlay.innerHTML =
                '<div class="mc-dialog">' +
                '  <div class="mc-stage"><video class="mc-video" autoplay playsinline muted></video></div>' +
                '  <div class="mc-actions">' +
                '    <button type="button" class="mc-btn mc-cancel">Abbrechen</button>' +
                '    <button type="button" class="mc-btn mc-shoot" disabled>Foto aufnehmen</button>' +
                '  </div>' +
                '  <div class="mc-error" hidden></div>' +
                '</div>';
            document.body.appendChild(overlay);

            const video = overlay.querySelector('.mc-video');
            const shootBtn = overlay.querySelector('.mc-shoot');
            const cancelBtn = overlay.querySelector('.mc-cancel');
            const errorBox = overlay.querySelector('.mc-error');

            const cleanup = () => {
                if (stream) stream.getTracks().forEach(t => t.stop());
                overlay.remove();
                document.removeEventListener('keydown', onKey);
            };
            const finish = (value) => {
                if (settled) return;
                settled = true;
                cleanup();
                resolve(value);
            };
            const onKey = (e) => { if (e.key === 'Escape') finish(null); };
            document.addEventListener('keydown', onKey);

            cancelBtn.addEventListener('click', () => finish(null));
            overlay.addEventListener('click', (e) => { if (e.target === overlay) finish(null); });

            shootBtn.addEventListener('click', () => {
                if (!video.videoWidth) return;
                const canvas = document.createElement('canvas');
                canvas.width = video.videoWidth;
                canvas.height = video.videoHeight;
                canvas.getContext('2d').drawImage(video, 0, 0);
                finish(canvas.toDataURL('image/jpeg', 0.9));
            });

            try {
                if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                    throw new Error('Kamera wird von diesem Browser nicht unterstützt.');
                }
                stream = await navigator.mediaDevices.getUserMedia({
                    video: { facingMode: 'environment' },
                    audio: false
                });
                if (settled) { stream.getTracks().forEach(t => t.stop()); return; }
                video.srcObject = stream;
                video.onloadedmetadata = () => { shootBtn.disabled = false; };
            } catch (e) {
                errorBox.textContent = 'Kamera konnte nicht geöffnet werden: ' + (e.message || e);
                errorBox.hidden = false;
                shootBtn.style.display = 'none';
            }
        });
    }
};
