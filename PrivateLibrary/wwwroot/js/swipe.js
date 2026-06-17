// Swipe-to-reveal interop for Private Library.
//
// Attaches horizontal-swipe tracking to a record element so the user can drag it
// to the left to reveal a delete action underneath. Works with touch and pointer
// (mouse) input. The element is translated live while dragging; on release it
// snaps either to "open" (delete revealed) or back to "closed", and reports the
// resulting state to .NET.
//
// Pure presentation lives in CSS; this module only manages the transform and the
// open/closed decision, then calls back so Blazor can keep its own state in sync
// (e.g. to close other open rows).

const registrations = new Map();
let nextId = 0;

// How far (px) the row must be dragged left to count as "open".
const OPEN_THRESHOLD = 56;
// Resting offset (px) when open — matches the action width in CSS.
const OPEN_OFFSET = 88;
// Horizontal movement (px) before we treat the gesture as a horizontal swipe and
// start suppressing vertical scroll. Keeps normal taps/scrolls feeling natural.
const DIRECTION_LOCK = 10;

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

export function register(element, dotNetRef) {
    if (!element) {
        return 0;
    }

    const id = ++nextId;
    const state = {
        element,
        dotNetRef,
        startX: 0,
        startY: 0,
        currentX: 0,
        dragging: false,
        locked: null, // null | 'h' | 'v'
        open: false,
        pointerId: null,
    };

    const surface = element.querySelector('[data-swipe-surface]') || element;

    function setTranslate(x, animate) {
        surface.style.transition = animate ? 'transform 0.2s ease' : 'none';
        surface.style.transform = `translateX(${x}px)`;
    }

    function open(animate = true) {
        state.open = true;
        state.currentX = -OPEN_OFFSET;
        setTranslate(-OPEN_OFFSET, animate);
        element.classList.add('swipe-open');
        state.dotNetRef?.invokeMethodAsync('OnSwipeStateChanged', true);
    }

    function close(animate = true) {
        state.open = false;
        state.currentX = 0;
        setTranslate(0, animate);
        element.classList.remove('swipe-open');
        state.dotNetRef?.invokeMethodAsync('OnSwipeStateChanged', false);
    }

    function onDown(e) {
        // Only primary button / single touch.
        if (e.button != null && e.button !== 0) {
            return;
        }
        state.dragging = true;
        state.locked = null;
        state.pointerId = e.pointerId ?? null;
        state.startX = e.clientX ?? (e.touches && e.touches[0].clientX) ?? 0;
        state.startY = e.clientY ?? (e.touches && e.touches[0].clientY) ?? 0;
        state.baseX = state.open ? -OPEN_OFFSET : 0;
    }

    function onMove(e) {
        if (!state.dragging) {
            return;
        }
        const x = e.clientX ?? (e.touches && e.touches[0].clientX) ?? 0;
        const y = e.clientY ?? (e.touches && e.touches[0].clientY) ?? 0;
        const dx = x - state.startX;
        const dy = y - state.startY;

        if (state.locked === null) {
            if (Math.abs(dx) > DIRECTION_LOCK || Math.abs(dy) > DIRECTION_LOCK) {
                state.locked = Math.abs(dx) > Math.abs(dy) ? 'h' : 'v';
            }
        }

        if (state.locked !== 'h') {
            return; // let the page scroll vertically
        }

        if (e.cancelable) {
            e.preventDefault();
        }

        // Allow dragging left to OPEN_OFFSET (a touch beyond for elasticity),
        // and right back to 0.
        const next = clamp(state.baseX + dx, -OPEN_OFFSET - 16, 0);
        state.currentX = next;
        setTranslate(next, false);
    }

    function onUp() {
        if (!state.dragging) {
            return;
        }
        state.dragging = false;
        if (state.locked !== 'h') {
            return;
        }
        if (state.currentX <= -OPEN_THRESHOLD) {
            open();
        } else {
            close();
        }
    }

    // Listen on the sliding surface only — not the whole row — so the delete
    // button (a sibling of the surface) is outside the gesture subtree and its
    // click is never intercepted by the swipe handlers.
    surface.addEventListener('pointerdown', onDown);
    surface.addEventListener('pointermove', onMove, { passive: false });
    surface.addEventListener('pointerup', onUp);
    surface.addEventListener('pointercancel', onUp);
    surface.addEventListener('pointerleave', onUp);

    registrations.set(id, { state, surface, close, open });
    return id;
}

// Programmatically close a row (used when another row opens, or after delete).
export function close(id) {
    const reg = registrations.get(id);
    reg?.close(true);
}

export function unregister(id) {
    const reg = registrations.get(id);
    if (!reg) {
        return;
    }
    registrations.delete(id);
}
