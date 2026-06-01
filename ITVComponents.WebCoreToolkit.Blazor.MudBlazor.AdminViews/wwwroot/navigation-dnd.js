// Drag-and-drop event delegation for NavigationTreeGrid.
// Document-level listeners (installed once) recognise drag sources / drop targets via
// data-nav-dnd-id attributes. The drop is dispatched to the .NET host whose
// [data-nav-dnd-host] root contains the target — registered via attach() from each grid
// instance, so cross-grid drops (parent <-> child rows) work transparently.

window.itvNavDnd = window.itvNavDnd || (function () {
    const hosts = new Map();
    let draggedId = null;
    let lastZoneEl = null;

    function clearZoneClasses(el) {
        if (!el) return;
        el.classList.remove('itv-nav-drop-above', 'itv-nav-drop-into', 'itv-nav-drop-below');
    }

    function pickZone(el, clientY) {
        const rect = el.getBoundingClientRect();
        const rel = rect.height > 0 ? (clientY - rect.top) / rect.height : 0.5;
        if (rel < 0.33) return 'above';
        if (rel < 0.67) return 'into';
        return 'below';
    }

    function injectStyle() {
        if (document.getElementById('itv-nav-dnd-style')) return;
        const style = document.createElement('style');
        style.id = 'itv-nav-dnd-style';
        style.textContent =
            '[data-nav-dnd-id] { cursor: grab; }' +
            '[data-nav-dnd-id]:active { cursor: grabbing; }' +
            // inset shadow with +y draws a band at the TOP edge, -y at the BOTTOM edge
            '.itv-nav-drop-above { box-shadow: inset 0 3px 0 0 var(--mud-palette-primary, #594ae2); }' +
            '.itv-nav-drop-into  { box-shadow: inset 0 0 0 2px var(--mud-palette-primary, #594ae2); background: rgba(89,74,226,0.06); }' +
            '.itv-nav-drop-below { box-shadow: inset 0 -3px 0 0 var(--mud-palette-primary, #594ae2); }';
        document.head.appendChild(style);
    }

    document.addEventListener('dragstart', (e) => {
        const src = e.target.closest('[data-nav-dnd-id]');
        if (!src) return;
        draggedId = parseInt(src.dataset.navDndId, 10);
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
        const tgt = e.target.closest('[data-nav-dnd-id]');
        if (!tgt) return;
        const tgtId = parseInt(tgt.dataset.navDndId, 10);
        if (tgtId === draggedId) return;
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
        const zone = pickZone(tgt, e.clientY);
        if (lastZoneEl && lastZoneEl !== tgt) clearZoneClasses(lastZoneEl);
        clearZoneClasses(tgt);
        tgt.classList.add('itv-nav-drop-' + zone);
        lastZoneEl = tgt;
    });

    document.addEventListener('dragleave', (e) => {
        const tgt = e.target.closest('[data-nav-dnd-id]');
        if (!tgt || tgt !== lastZoneEl) return;
        const related = e.relatedTarget;
        if (!related || !tgt.contains(related)) {
            clearZoneClasses(lastZoneEl);
            lastZoneEl = null;
        }
    });

    document.addEventListener('drop', (e) => {
        if (draggedId === null) return;
        const tgt = e.target.closest('[data-nav-dnd-id]');
        if (!tgt) return;
        const tgtId = parseInt(tgt.dataset.navDndId, 10);
        if (tgtId === draggedId) return;
        e.preventDefault();

        const zone = pickZone(tgt, e.clientY);
        const dragged = draggedId;
        draggedId = null;
        clearZoneClasses(lastZoneEl);
        lastZoneEl = null;

        const hostEl = tgt.closest('[data-nav-dnd-host]');
        const dotNet = hostEl ? hosts.get(hostEl.dataset.navDndHost) : null;
        if (!dotNet) return;
        dotNet.invokeMethodAsync('HandleDrop', dragged, tgtId, zone).catch(() => { /* circuit gone */ });
    });

    injectStyle();

    return {
        attach(hostEl, dotNet) {
            if (!hostEl || !hostEl.dataset.navDndHost) return;
            hosts.set(hostEl.dataset.navDndHost, dotNet);
        },
        detach(hostEl) {
            if (!hostEl || !hostEl.dataset.navDndHost) return;
            hosts.delete(hostEl.dataset.navDndHost);
        }
    };
})();
