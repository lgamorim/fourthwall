// The canvas's shim: the thin layer between the browser's resize, wheel, and pointer events and
// StoryCanvas's [JSInvokable] methods. It measures, captures, and forwards; every decision about
// what a gesture means is made in C# (CanvasViewport, CanvasInteraction). The host it is given
// is the sheet the svg fills exactly, so the sizes and wheel points it reports are the svg's own.
//
// Why it exists at all: browsers register wheel listeners as passive by default, so only a
// non-passive listener here can stop a wheel from scrolling the page; pointer capture must be set
// during the press to catch the moves that follow, and a round trip to the server would miss them;
// and the window's size comes from a ResizeObserver.
//
// Every forwarded event is coalesced to one invoke per animation frame with latest-wins
// backpressure: while an invoke is in flight over SignalR, later moves replace the pending one
// and wheel travel accumulates, so a slow round trip never queues up stale frames. A release is
// sent at once with its final coordinates, ahead of any pending move.

const LINE_HEIGHT_PIXELS = 16;

const sessions = new WeakMap();

export function attach(host, dotnet) {
    if (sessions.has(host)) {
        detach(host);
    }

    const listeners = new AbortController();
    const session = {
        host,
        dotnet,
        listeners,
        detached: false,
        inFlight: null,
        pending: new Map(),
        frame: 0,
        pointerId: null,
        lastPoint: null,
        observer: new ResizeObserver((entries) => {
            const { width, height } = entries[entries.length - 1].contentRect;
            queue(session, "ResizeAsync", [width, height]);
        }),
    };
    sessions.set(host, session);

    const options = { signal: listeners.signal };
    host.addEventListener("wheel", (event) => onWheel(session, event), { passive: false, signal: listeners.signal });
    host.addEventListener("pointerdown", (event) => onPointerDown(session, event), options);
    // A press starts on the sheet; the rest of the gesture is heard at the document. While the
    // capture holds, every move and the release reach the document anyway. If the pressed element
    // leaves the DOM mid-gesture (its scene deleted from another tab, a child of it dropped by a
    // re-render), the browser drops the capture, fires lostpointercapture at the document, and a
    // release outside the sheet no longer reaches the sheet; the document still hears it, so the
    // gesture always ends. The pointer id filter keeps other pointers, and the ordinary release
    // (pointerup, then the loss of its capture), from ending it twice.
    document.addEventListener("pointermove", (event) => onPointerMove(session, event), options);
    document.addEventListener("pointerup", (event) => onPointerUp(session, event), options);
    document.addEventListener("pointercancel", (event) => onPointerUp(session, event), options);
    document.addEventListener("lostpointercapture", (event) => onPointerUp(session, event), options);
    session.observer.observe(host);
}

export function detach(host) {
    const session = sessions.get(host);
    if (!session) {
        return;
    }

    session.detached = true;
    session.observer.disconnect();
    session.listeners.abort();
    if (session.frame) {
        cancelAnimationFrame(session.frame);
        session.frame = 0;
    }
    sessions.delete(host);
}

function onWheel(session, event) {
    // Stop the page from scrolling, and the browser from zooming on Ctrl+wheel (a trackpad pinch).
    event.preventDefault();

    const rect = session.host.getBoundingClientRect();
    const scale = event.deltaMode === 1 ? LINE_HEIGHT_PIXELS : event.deltaMode === 2 ? rect.height : 1;
    const pending = session.pending.get("ZoomAsync");
    const travel = (pending ? pending[2] : 0) + (event.deltaY * scale);
    queue(session, "ZoomAsync", [event.clientX - rect.left, event.clientY - rect.top, travel]);
}

function onPointerDown(session, event) {
    if (event.button !== 0 || session.pointerId !== null || !(event.target instanceof Element)
        || !event.target.closest("svg.canvas-svg")) {
        return;
    }

    // Capture on the pressed element itself. The click the browser fires on release goes to the
    // common ancestor of the press and release targets; capturing on the host would move that
    // click off the node, and a click would never select. Blazor's own @onpointerdown handlers
    // tell C# what was pressed and where.
    session.pointerId = event.pointerId;
    session.lastPoint = [event.clientX, event.clientY];
    event.target.setPointerCapture(event.pointerId);
}

function onPointerMove(session, event) {
    if (event.pointerId !== session.pointerId) {
        return;
    }

    session.lastPoint = [event.clientX, event.clientY];
    queue(session, "MoveAsync", session.lastPoint);
}

function onPointerUp(session, event) {
    if (event.pointerId !== session.pointerId) {
        return;
    }

    // A cancel or a lost capture carries no useful coordinates; the release ends where the pointer
    // was last seen.
    const point = event.type === "pointerup" ? [event.clientX, event.clientY] : session.lastPoint;
    session.pointerId = null;
    session.lastPoint = null;
    session.pending.delete("MoveAsync");
    invoke(session, "UpAsync", point);
}

function queue(session, method, args) {
    session.pending.set(method, args);
    if (!session.frame) {
        session.frame = requestAnimationFrame(() => flush(session));
    }
}

function flush(session) {
    session.frame = 0;
    if (session.detached || session.inFlight || session.pending.size === 0) {
        return;
    }

    const calls = [...session.pending].map(([method, args]) => invoke(session, method, args));
    session.pending.clear();
    session.inFlight = Promise.all(calls).finally(() => {
        session.inFlight = null;
        if (session.pending.size > 0) {
            flush(session);
        }
    });
}

function invoke(session, method, args) {
    if (session.detached) {
        return Promise.resolve();
    }

    return session.dotnet.invokeMethodAsync(method, ...args).catch((error) => {
        // After detach the .NET side is gone and a rejection is expected; before it, it is a bug.
        if (!session.detached) {
            console.error(error);
        }
    });
}
