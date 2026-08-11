// Drag-and-drop event delegation for admin grids that let the user arrange rows.
// Document-level listeners (installed once) recognise drag sources / drop targets via
// data-itv-dnd-id attributes. The drop is dispatched to the .NET host whose
// [data-itv-dnd-host] root contains the target — registered via attach() from each grid
// instance, so cross-grid drops (parent <-> child rows of a tree) work transparently.
//
// Two shapes of grid share this file:
//   * hierarchical (navigation): the host root carries data-itv-dnd-into="true" and the middle
//     third of a row means "make it a child of this one".
//   * flat (dashboard widgets): no such attribute, the row splits in half into above / below.
// Keeping both in ONE helper is deliberate — the zone maths and the listener bookkeeping would
// otherwise exist twice and drift apart.

window.itvGridDnd = window.itvGridDnd || (function () {
    const hosts = new Map();
    let draggedId = null;
    let lastZoneEl = null;

    function clearZoneClasses(el) {
        if (!el) return;
        el.classList.remove('itv-dnd-above', 'itv-dnd-into', 'itv-dnd-below');
    }

    function allowsInto(el) {
        const hostEl = el.closest('[data-itv-dnd-host]');
        return !!hostEl && hostEl.dataset.itvDndInto === 'true';
    }

    function pickZone(el, clientY) {
        const rect = el.getBoundingClientRect();
        const rel = rect.height > 0 ? (clientY - rect.top) / rect.height : 0.5;
        if (!allowsInto(el)) return rel < 0.5 ? 'above' : 'below';
        if (rel < 0.33) return 'above';
        if (rel < 0.67) return 'into';
        return 'below';
    }

    function injectStyle() {
        if (document.getElementById('itv-grid-dnd-style')) return;
        const style = document.createElement('style');
        style.id = 'itv-grid-dnd-style';
        style.textContent =
            '[data-itv-dnd-id] { cursor: grab; }' +
            '[data-itv-dnd-id]:active { cursor: grabbing; }' +
            // inset shadow with +y draws a band at the TOP edge, -y at the BOTTOM edge
            '.itv-dnd-above { box-shadow: inset 0 3px 0 0 var(--mud-palette-primary, #594ae2); }' +
            '.itv-dnd-into  { box-shadow: inset 0 0 0 2px var(--mud-palette-primary, #594ae2); background: rgba(89,74,226,0.06); }' +
            '.itv-dnd-below { box-shadow: inset 0 -3px 0 0 var(--mud-palette-primary, #594ae2); }';
        document.head.appendChild(style);
    }

    document.addEventListener('dragstart', (e) => {
        const src = e.target.closest('[data-itv-dnd-id]');
        if (!src) return;
        draggedId = parseInt(src.dataset.itvDndId, 10);
        if (e.dataTransfer) {
            e.dataTransfer.effectAllowed = 'move';
            try { e.dataTransfer.setData('text/plain', String(draggedId)); } catch (_) { /* IE-compat */ }
        }
    });

    document.addEventListener('dragend', () => {
        draggedId = null;
        clearZoneClasses(lastZoneEl);
        lastZoneEl = null;
    });

    document.addEventListener('dragover', (e) => {
        if (draggedId === null) return;
        const tgt = e.target.closest('[data-itv-dnd-id]');
        if (!tgt) return;
        const tgtId = parseInt(tgt.dataset.itvDndId, 10);
        if (tgtId === draggedId) return;
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
        const zone = pickZone(tgt, e.clientY);
        if (lastZoneEl && lastZoneEl !== tgt) clearZoneClasses(lastZoneEl);
        clearZoneClasses(tgt);
        tgt.classList.add('itv-dnd-' + zone);
        lastZoneEl = tgt;
    });

    document.addEventListener('dragleave', (e) => {
        const tgt = e.target.closest('[data-itv-dnd-id]');
        if (!tgt || tgt !== lastZoneEl) return;
        const related = e.relatedTarget;
        if (!related || !tgt.contains(related)) {
            clearZoneClasses(lastZoneEl);
            lastZoneEl = null;
        }
    });

    document.addEventListener('drop', (e) => {
        if (draggedId === null) return;
        const tgt = e.target.closest('[data-itv-dnd-id]');
        if (!tgt) return;
        const tgtId = parseInt(tgt.dataset.itvDndId, 10);
        if (tgtId === draggedId) return;
        e.preventDefault();

        const zone = pickZone(tgt, e.clientY);
        const dragged = draggedId;
        draggedId = null;
        clearZoneClasses(lastZoneEl);
        lastZoneEl = null;

        const hostEl = tgt.closest('[data-itv-dnd-host]');
        const dotNet = hostEl ? hosts.get(hostEl.dataset.itvDndHost) : null;
        if (!dotNet) return;
        dotNet.invokeMethodAsync('HandleDrop', dragged, tgtId, zone).catch(() => { /* circuit gone */ });
    });

    injectStyle();

    return {
        attach(hostEl, dotNet) {
            if (!hostEl || !hostEl.dataset.itvDndHost) return;
            hosts.set(hostEl.dataset.itvDndHost, dotNet);
        },
        detach(hostEl) {
            if (!hostEl || !hostEl.dataset.itvDndHost) return;
            hosts.delete(hostEl.dataset.itvDndHost);
        }
    };
})();
