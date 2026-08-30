// wwwroot/js/photoCapture.js
// The live camera behind the Photos tool, and the crop-and-downscale both capture routes share.
//
// Every function here treats camera failure as a normal state with a real path through it, because
// the fallback is part of the build rather than a contingency: the tool has to degrade to the
// native camera app rather than break if a phone refuses getUserMedia. See the plan's "build for it
// to fail" section.

let stream = null;

/**
 * Opens the camera into a <video> and waits until it is genuinely producing frames.
 *
 * Resolves { ok: true } or { ok: false, reason } - it never throws, because every caller's job is
 * the same either way: fall back to the file input. The reasons are distinguished only so the
 * leader can be told something true on screen.
 */
export async function start(video, facingMode) {
    stop();

    // An old or locked-down browser, or a page that is not on a secure origin. Nothing to try.
    if (!navigator.mediaDevices?.getUserMedia)
        return { ok: false, reason: 'unsupported' };

    try {
        stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode, width: { ideal: 1280 }, height: { ideal: 1280 } },
            audio: false
        });
    } catch (e) {
        // NotAllowedError - the leader said no, or the origin is not permitted.
        // NotFoundError   - there is no camera.
        // Anything else is a surprise, and is treated exactly the same way.
        return { ok: false, reason: e?.name === 'NotAllowedError' ? 'denied'
                             : e?.name === 'NotFoundError'   ? 'nocamera'
                             : 'failed' };
    }

    video.srcObject = stream;
    // Required on iOS or the video is taken fullscreen by the OS player.
    video.setAttribute('playsinline', '');
    video.muted = true;

    try {
        await video.play();
    } catch {
        // Autoplay refusal. The stream may still deliver frames, so fall through to the size wait
        // rather than giving up here.
    }

    // The stream can open and still never produce a frame - the shape a standalone-mode failure is
    // most likely to take. A spinner that waits forever is the failure mode to avoid, so this is
    // bounded: five seconds without a non-zero videoWidth is a failure like any other.
    const ready = await waitForFrames(video, 5000);
    if (!ready) {
        stop();
        return { ok: false, reason: 'noframes' };
    }

    return { ok: true };
}

function waitForFrames(video, timeoutMs) {
    return new Promise(resolve => {
        const deadline = performance.now() + timeoutMs;

        const check = () => {
            if (video.videoWidth > 0 && video.videoHeight > 0) return resolve(true);
            if (performance.now() > deadline) return resolve(false);
            requestAnimationFrame(check);
        };

        check();
    });
}

export function stop() {
    if (!stream) return;
    for (const track of stream.getTracks()) track.stop();
    stream = null;
}

/** Which cameras exist, so the front/back toggle can be hidden when there is only one. */
export async function hasMultipleCameras() {
    if (!navigator.mediaDevices?.enumerateDevices) return false;
    try {
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices.filter(d => d.kind === 'videoinput').length > 1;
    } catch {
        return false;
    }
}

const SIZE = 500;
const QUALITY = 0.85;

/**
 * Grabs the current frame, centre-cropped to a square and downscaled to 500x500 JPEG.
 *
 * 500x500 at q0.85 is what Elvanto's own thumbnails are and puts a photo at roughly 20-50 KB, so
 * the whole roll stays well under 100 MB. Returns a base64 string - not a data URL - because .NET
 * wants the bytes and slicing the prefix off in two places invites getting it wrong in one.
 */
export function capture(video) {
    return toSquareJpeg(video, video.videoWidth, video.videoHeight);
}

/**
 * The same crop-and-downscale for a file the leader picked from their library or took with the
 * native camera app, so both routes produce byte-identical objects for the same image - and
 * therefore the same content hash, and one object in the store rather than two.
 *
 * Takes the bytes rather than a File: .NET reads the file through IBrowserFile, and handing the
 * File object across interop instead would mean two different ways of getting at the same image.
 */
export async function captureFromFileBytes(bytes, contentType) {
    const blob = new Blob([new Uint8Array(bytes)], { type: contentType || 'image/jpeg' });
    const bitmap = await createImageBitmap(blob);
    try {
        return toSquareJpeg(bitmap, bitmap.width, bitmap.height);
    } finally {
        bitmap.close?.();
    }
}

// Draws the source frame as it actually is. There is deliberately no mirroring step here.
//
// The front-camera PREVIEW is mirrored, but only in CSS, so that aiming feels like a mirror. The
// frames the <video> element holds are the raw, unmirrored ones, so drawing them straight gives the
// face the right way round - which is what anyone matching the photo against the child in front of
// them needs. Flipping here as well re-mirrored it, and the bug was invisible because the mirrored
// preview and the mirrored capture agreed with each other.
function toSquareJpeg(source, width, height) {
    const side = Math.min(width, height);
    const sx = (width - side) / 2;
    const sy = (height - side) / 2;

    const canvas = document.createElement('canvas');
    canvas.width = SIZE;
    canvas.height = SIZE;

    canvas.getContext('2d').drawImage(source, sx, sy, side, side, 0, 0, SIZE, SIZE);

    return canvas.toDataURL('image/jpeg', QUALITY).split(',')[1];
}
