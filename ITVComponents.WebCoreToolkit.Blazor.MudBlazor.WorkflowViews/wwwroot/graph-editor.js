// Pointer interactions for the Workflow editor canvas (WorkflowEditor.razor).
// Blazor renders the SVG (nodes as <g data-wf-node transform="translate(x,y)"> with a
// <circle data-wf-port> output handle; edges as <line data-wf-edge data-source data-target>).
// This module drives the *live* interactions client-side (Blazor Server would lag if every
// drag frame went over the wire): dragging a node moves its group + redraws connected edges
// locally, and on release reports the final coordinates / new edge / selection back to .NET.
// The model stays authoritative in .NET; the next render reconciles the DOM with it.

window.itvWfEditor = window.itvWfEditor || (function () {
    const SVGNS = 'http://www.w3.org/2000/svg';
    const instances = new Map();
    const MOVE_THRESHOLD = 3;

    function injectStyle() {
        if (document.getElementById('itv-wf-editor-style')) return;
        const style = document.createElement('style');
        style.id = 'itv-wf-editor-style';
        style.textContent =
            '[data-wf-node] { cursor: grab; }' +
            '[data-wf-node]:active { cursor: grabbing; }' +
            '[data-wf-port] { cursor: crosshair; }' +
            '.itv-wf-temp-edge { stroke: var(--mud-palette-primary, #594ae2); stroke-width: 2; stroke-dasharray: 4 3; pointer-events: none; }';
        document.head.appendChild(style);
    }

    function toSvgPoint(svg, clientX, clientY) {
        try {
            const pt = svg.createSVGPoint();
            pt.x = clientX;
            pt.y = clientY;
            const ctm = svg.getScreenCTM();
            if (!ctm) return { x: clientX, y: clientY };
            const p = pt.matrixTransform(ctm.inverse());
            return { x: p.x, y: p.y };
        } catch (_) {
            return { x: clientX, y: clientY };
        }
    }

    function parseTranslate(g) {
        const t = g.getAttribute('transform') || '';
        const m = /translate\(\s*([-\d.]+)[ ,]+([-\d.]+)\s*\)/.exec(t);
        return m ? { x: parseFloat(m[1]), y: parseFloat(m[2]) } : { x: 0, y: 0 };
    }

    function nodeBox(svg, id) {
        if (!id) return null;
        const g = svg.querySelector('[data-node-id="' + (window.CSS && CSS.escape ? CSS.escape(id) : id) + '"]');
        if (!g) return null;
        const p = parseTranslate(g);
        const w = parseFloat(g.getAttribute('data-w')) || 0;
        const h = parseFloat(g.getAttribute('data-h')) || 0;
        return { x: p.x, y: p.y, w: w, h: h, cx: p.x + w / 2, cy: p.y + h / 2 };
    }

    // Rectangle-ray clip: point on the box border on the line from the box centre toward (tx,ty).
    // Mirrors GraphLayout.ClipToBorder on the .NET side so live redraw matches the server render.
    function clip(box, tx, ty) {
        const dx = tx - box.cx;
        const dy = ty - box.cy;
        if (dx === 0 && dy === 0) return { x: box.cx, y: box.cy };
        const sx = dx !== 0 ? (box.w / 2) / Math.abs(dx) : Infinity;
        const sy = dy !== 0 ? (box.h / 2) / Math.abs(dy) : Infinity;
        const t = Math.min(sx, sy);
        return { x: box.cx + dx * t, y: box.cy + dy * t };
    }

    function redrawEdges(svg, nodeId) {
        svg.querySelectorAll('[data-wf-edge]').forEach(function (line) {
            const s = line.getAttribute('data-source');
            const t = line.getAttribute('data-target');
            if (s !== nodeId && t !== nodeId) return;
            const sb = nodeBox(svg, s);
            const tb = nodeBox(svg, t);
            if (!sb || !tb) return;
            const p1 = clip(sb, tb.cx, tb.cy);
            const p2 = clip(tb, sb.cx, sb.cy);
            line.setAttribute('x1', p1.x);
            line.setAttribute('y1', p1.y);
            line.setAttribute('x2', p2.x);
            line.setAttribute('y2', p2.y);
        });
    }

    return {
        attach(hostEl, svgEl, dotNet) {
            if (!hostEl || !svgEl) return;
            injectStyle();
            let drag = null;

            function onDown(e) {
                if (e.button !== undefined && e.button !== 0) return;
                const portEl = e.target.closest ? e.target.closest('[data-wf-port]') : null;
                const nodeEl = e.target.closest ? e.target.closest('[data-wf-node]') : null;
                const edgeEl = e.target.closest ? e.target.closest('[data-wf-edge]') : null;

                if (portEl && nodeEl) {
                    const sourceId = nodeEl.getAttribute('data-node-id');
                    const box = nodeBox(svgEl, sourceId);
                    const line = document.createElementNS(SVGNS, 'line');
                    line.setAttribute('class', 'itv-wf-temp-edge');
                    const start = box ? { x: box.x + box.w, y: box.cy } : toSvgPoint(svgEl, e.clientX, e.clientY);
                    line.setAttribute('x1', start.x);
                    line.setAttribute('y1', start.y);
                    line.setAttribute('x2', start.x);
                    line.setAttribute('y2', start.y);
                    svgEl.appendChild(line);
                    drag = { mode: 'edge', sourceId: sourceId, tempLine: line, moved: false };
                    try { svgEl.setPointerCapture(e.pointerId); } catch (_) { /* no capture */ }
                    e.preventDefault();
                    return;
                }

                if (nodeEl) {
                    const p = parseTranslate(nodeEl);
                    const pt = toSvgPoint(svgEl, e.clientX, e.clientY);
                    drag = {
                        mode: 'node',
                        nodeId: nodeEl.getAttribute('data-node-id'),
                        g: nodeEl,
                        offsetX: pt.x - p.x,
                        offsetY: pt.y - p.y,
                        startClientX: e.clientX,
                        startClientY: e.clientY,
                        lastX: p.x,
                        lastY: p.y,
                        moved: false
                    };
                    try { svgEl.setPointerCapture(e.pointerId); } catch (_) { /* no capture */ }
                    e.preventDefault();
                    return;
                }

                if (edgeEl) {
                    drag = { mode: 'selectEdge', edgeId: edgeEl.getAttribute('data-edge-id') };
                    return;
                }

                drag = { mode: 'empty' };
            }

            function onMove(e) {
                if (!drag) return;
                const pt = toSvgPoint(svgEl, e.clientX, e.clientY);
                if (drag.mode === 'node') {
                    if (Math.abs(e.clientX - drag.startClientX) + Math.abs(e.clientY - drag.startClientY) > MOVE_THRESHOLD) {
                        drag.moved = true;
                    }
                    drag.lastX = pt.x - drag.offsetX;
                    drag.lastY = pt.y - drag.offsetY;
                    drag.g.setAttribute('transform', 'translate(' + drag.lastX + ',' + drag.lastY + ')');
                    redrawEdges(svgEl, drag.nodeId);
                } else if (drag.mode === 'edge') {
                    drag.moved = true;
                    drag.tempLine.setAttribute('x2', pt.x);
                    drag.tempLine.setAttribute('y2', pt.y);
                }
            }

            function onUp(e) {
                if (!drag) return;
                try { svgEl.releasePointerCapture(e.pointerId); } catch (_) { /* ignore */ }
                const mode = drag.mode;
                const d = drag;
                drag = null;

                if (mode === 'node') {
                    if (d.moved) {
                        dotNet.invokeMethodAsync('OnNodeMoved', d.nodeId, d.lastX, d.lastY).catch(function () { });
                    } else {
                        dotNet.invokeMethodAsync('OnSelectNode', d.nodeId).catch(function () { });
                    }
                } else if (mode === 'edge') {
                    let targetId = null;
                    const under = document.elementFromPoint(e.clientX, e.clientY);
                    const targetNode = under && under.closest ? under.closest('[data-wf-node]') : null;
                    if (targetNode) targetId = targetNode.getAttribute('data-node-id');
                    if (d.tempLine) d.tempLine.remove();
                    if (d.moved && targetId && targetId !== d.sourceId) {
                        dotNet.invokeMethodAsync('OnEdgeCreated', d.sourceId, targetId).catch(function () { });
                    } else {
                        dotNet.invokeMethodAsync('OnSelectNode', d.sourceId).catch(function () { });
                    }
                } else if (mode === 'selectEdge') {
                    dotNet.invokeMethodAsync('OnSelectEdge', d.edgeId).catch(function () { });
                } else if (mode === 'empty') {
                    dotNet.invokeMethodAsync('OnSelectNone').catch(function () { });
                }
            }

            svgEl.addEventListener('pointerdown', onDown);
            svgEl.addEventListener('pointermove', onMove);
            svgEl.addEventListener('pointerup', onUp);
            svgEl.addEventListener('pointercancel', onUp);
            instances.set(hostEl, { svgEl: svgEl, onDown: onDown, onMove: onMove, onUp: onUp });
        },

        // Loest einen Datei-Download eines Text-Strings aus (Export der Workflow-Definition als JSON).
        downloadText(filename, text) {
            const blob = new Blob([text], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename || 'workflow.json';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        },

        detach(hostEl) {
            const it = instances.get(hostEl);
            if (!it) return;
            it.svgEl.removeEventListener('pointerdown', it.onDown);
            it.svgEl.removeEventListener('pointermove', it.onMove);
            it.svgEl.removeEventListener('pointerup', it.onUp);
            it.svgEl.removeEventListener('pointercancel', it.onUp);
            instances.delete(hostEl);
        }
    };
})();
