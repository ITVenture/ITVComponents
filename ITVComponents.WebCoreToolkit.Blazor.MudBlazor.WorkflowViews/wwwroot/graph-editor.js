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
            '.itv-wf-temp-edge { stroke: var(--mud-palette-primary, #594ae2); stroke-width: 2; stroke-dasharray: 4 3; pointer-events: none; }' +
            '.itv-wf-temp-err { stroke: var(--mud-palette-error, #f44336); }';
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

    // --- Sichtfenster (viewBox) --------------------------------------------------------------
    // Zoom/Pan aendern nur die viewBox des SVG. Sie wird bei jeder Geste frisch vom Attribut gelesen
    // (nicht zwischengespeichert), damit von .NET gesetzte Werte - Fit-Knopf, +/- - sofort mitgehen.
    // Am Ende einer Geste wird der Ausschnitt an .NET gemeldet (OnViewChanged), damit der naechste
    // Server-Render denselben Ausschnitt behaelt.
    const MIN_VIEW_W = 200;
    const MAX_VIEW_W = 20000;

    function getViewBox(svg) {
        const vb = svg.getAttribute('viewBox');
        if (vb) {
            const m = vb.trim().split(/[ ,]+/).map(parseFloat);
            if (m.length === 4 && m.every(function (n) { return !isNaN(n); })) {
                return { x: m[0], y: m[1], w: m[2], h: m[3] };
            }
        }
        return { x: 0, y: 0, w: 1200, h: 800 };
    }

    function setViewBox(svg, v) {
        svg.setAttribute('viewBox', v.x + ' ' + v.y + ' ' + v.w + ' ' + v.h);
    }

    // ---------------------------------------------------------------------------------------------
    // Orthogonales Kanten-Routing. Strukturgleich zu Graph/EdgeRouter.cs auf der .NET-Seite: nur
    // waagrechte und senkrechte Teilstuecke, kein Teilstueck durch einen Knoten, wenige Ecken. Die
    // Doppelung ist gewollt - der Server rechnet beim Rendern, diese Fassung waehrend des Ziehens
    // (ein Roundtrip pro Mausbewegung waere ueber Blazor Server nicht fluessig). Wer hier etwas
    // aendert, aendert es dort mit, sonst springt die Linie beim Loslassen.
    // Bewusste Abweichung: beim Ziehen werden nur die Kanten des bewegten Knotens neu geroutet.
    // Andere Kanten, die durch die neue Position blockiert werden, richtet der naechste Server-
    // Render (beim Loslassen).
    // ---------------------------------------------------------------------------------------------

    const CLEARANCE = 14;
    const TURN_PENALTY = 30;
    const ESCAPE_MARGIN = 40;
    const LOCAL_MARGIN = 160;
    const MAX_GRID_CELLS = 4000;
    const EPS = 0.5;
    const DIRS = [[1, 0], [-1, 0], [0, 1], [0, -1]];

    function allObstacles(svg) {
        const list = [];
        svg.querySelectorAll('[data-wf-node]').forEach(function (g) {
            const p = parseTranslate(g);
            const w = parseFloat(g.getAttribute('data-w')) || 0;
            const h = parseFloat(g.getAttribute('data-h')) || 0;
            list.push({ left: p.x - CLEARANCE, top: p.y - CLEARANCE, right: p.x + w + CLEARANCE, bottom: p.y + h + CLEARANCE });
        });
        return list;
    }

    function chooseSides(s, t) {
        const dx = t.cx - s.cx;
        const dy = t.cy - s.cy;
        if (Math.abs(dx) >= Math.abs(dy)) return dx >= 0 ? ['right', 'left'] : ['left', 'right'];
        return dy >= 0 ? ['bottom', 'top'] : ['top', 'bottom'];
    }

    function along(v, lo, hi) {
        const inset = Math.min(6, (hi - lo) / 3);
        const min = lo + inset;
        const max = hi - inset;
        return v < min ? min : (v > max ? max : v);
    }

    function anchorOf(box, side, off) {
        switch (side) {
            case 'left': return { x: box.x, y: along(box.cy + off, box.y, box.y + box.h) };
            case 'right': return { x: box.x + box.w, y: along(box.cy + off, box.y, box.y + box.h) };
            case 'top': return { x: along(box.cx + off, box.x, box.x + box.w), y: box.y };
            default: return { x: along(box.cx + off, box.x, box.x + box.w), y: box.y + box.h };
        }
    }

    function advance(p, side, d) {
        switch (side) {
            case 'left': return { x: p.x - d, y: p.y };
            case 'right': return { x: p.x + d, y: p.y };
            case 'top': return { x: p.x, y: p.y - d };
            default: return { x: p.x, y: p.y + d };
        }
    }

    function outwardDir(side) {
        return side === 'right' ? 0 : side === 'left' ? 1 : side === 'bottom' ? 2 : 3;
    }

    function leavesVertically(side) { return side === 'top' || side === 'bottom'; }

    function isClear(ax, ay, bx, by, blocked) {
        const minX = Math.min(ax, bx), maxX = Math.max(ax, bx);
        const minY = Math.min(ay, by), maxY = Math.max(ay, by);
        for (let i = 0; i < blocked.length; i++) {
            const r = blocked[i];
            if (minX < r.right - EPS && maxX > r.left + EPS && minY < r.bottom - EPS && maxY > r.top + EPS) return false;
        }
        return true;
    }

    function ensureValue(sorted, value) {
        for (let i = 0; i < sorted.length; i++) {
            if (Math.abs(sorted[i] - value) <= EPS) { sorted[i] = value; return; }
            if (sorted[i] > value) { sorted.splice(i, 0, value); return; }
        }
        sorted.push(value);
    }

    function axisValues(blocked, horizontal, a, b, bounds) {
        const values = [];
        for (let i = 0; i < blocked.length; i++) {
            values.push(horizontal ? blocked[i].left : blocked[i].top);
            values.push(horizontal ? blocked[i].right : blocked[i].bottom);
        }
        values.push((horizontal ? bounds.left : bounds.top) - ESCAPE_MARGIN);
        values.push((horizontal ? bounds.right : bounds.bottom) + ESCAPE_MARGIN);
        values.sort(function (x, y) { return x - y; });
        const out = [];
        for (let i = 0; i < values.length; i++) {
            if (out.length === 0 || values[i] - out[out.length - 1] > EPS) out.push(values[i]);
        }
        ensureValue(out, a);
        ensureValue(out, b);
        return out;
    }

    function indexOfValue(sorted, v) {
        for (let i = 0; i < sorted.length; i++) if (Math.abs(sorted[i] - v) <= EPS) return i;
        return -1;
    }

    function Heap() { this.a = []; }
    Heap.prototype.push = function (item, pri) {
        this.a.push({ i: item, p: pri });
        let c = this.a.length - 1;
        while (c > 0) {
            const par = (c - 1) >> 1;
            if (this.a[par].p <= this.a[c].p) break;
            const t = this.a[par]; this.a[par] = this.a[c]; this.a[c] = t; c = par;
        }
    };
    Heap.prototype.pop = function () {
        const top = this.a[0];
        const last = this.a.pop();
        if (this.a.length) {
            this.a[0] = last;
            let i = 0;
            for (;;) {
                const l = 2 * i + 1, r = l + 1;
                let m = i;
                if (l < this.a.length && this.a[l].p < this.a[m].p) m = l;
                if (r < this.a.length && this.a[r].p < this.a[m].p) m = r;
                if (m === i) break;
                const t = this.a[m]; this.a[m] = this.a[i]; this.a[i] = t; i = m;
            }
        }
        return top;
    };

    function findPath(s, sSide, t, tSide, obstacles) {
        const bounds = {
            left: Math.min(s.x, t.x) - LOCAL_MARGIN, top: Math.min(s.y, t.y) - LOCAL_MARGIN,
            right: Math.max(s.x, t.x) + LOCAL_MARGIN, bottom: Math.max(s.y, t.y) + LOCAL_MARGIN
        };
        const blocked = [];
        for (let i = 0; i < obstacles.length; i++) {
            const o = obstacles[i];
            if (o.left < bounds.right && o.right > bounds.left && o.top < bounds.bottom && o.bottom > bounds.top) blocked.push(o);
        }

        if ((Math.abs(s.x - t.x) <= EPS || Math.abs(s.y - t.y) <= EPS) && isClear(s.x, s.y, t.x, t.y, blocked)) {
            return [s, t];
        }

        const xs = axisValues(blocked, true, s.x, t.x, bounds);
        const ys = axisValues(blocked, false, s.y, t.y, bounds);
        if (xs.length * ys.length > MAX_GRID_CELLS) return null;

        const nx = xs.length, ny = ys.length;
        const si = indexOfValue(xs, s.x), sj = indexOfValue(ys, s.y);
        const ti = indexOfValue(xs, t.x), tj = indexOfValue(ys, t.y);
        if (si < 0 || sj < 0 || ti < 0 || tj < 0) return null;

        const states = nx * ny * 4;
        const cost = new Float64Array(states).fill(Infinity);
        const from = new Int32Array(states).fill(-1);
        const done = new Uint8Array(states);
        const state = function (ix, iy, dir) { return ((ix * ny + iy) * 4) + dir; };

        const start = state(si, sj, outwardDir(sSide));
        cost[start] = 0;
        const forbidden = outwardDir(tSide);
        const tx = xs[ti], ty = ys[tj];
        const queue = new Heap();
        queue.push(start, Math.abs(s.x - tx) + Math.abs(s.y - ty));

        let goal = -1;
        while (queue.a.length) {
            const cur = queue.pop().i;
            if (done[cur]) continue;
            done[cur] = 1;
            const ix = Math.floor((cur / 4) / ny);
            const iy = Math.floor(cur / 4) % ny;
            const dir = cur % 4;
            if (ix === ti && iy === tj && dir !== forbidden) { goal = cur; break; }

            const ax = xs[ix], ay = ys[iy];
            for (let d = 0; d < 4; d++) {
                const jx = ix + DIRS[d][0], jy = iy + DIRS[d][1];
                if (jx < 0 || jx >= nx || jy < 0 || jy >= ny) continue;
                const bx = xs[jx], by = ys[jy];
                if (!isClear(ax, ay, bx, by, blocked)) continue;
                const next = state(jx, jy, d);
                const g = cost[cur] + Math.abs(bx - ax) + Math.abs(by - ay) + (d === dir ? 0 : TURN_PENALTY);
                if (g + 1e-9 < cost[next]) {
                    cost[next] = g;
                    from[next] = cur;
                    queue.push(next, g + Math.abs(bx - tx) + Math.abs(by - ty));
                }
            }
        }

        if (goal < 0) return null;
        const path = [];
        for (let st = goal; st >= 0; st = from[st]) {
            path.push({ x: xs[Math.floor((st / 4) / ny)], y: ys[Math.floor(st / 4) % ny] });
        }
        path.reverse();
        return path;
    }

    function fallbackPath(s, sSide, t, tSide) {
        const sv = leavesVertically(sSide), tv = leavesVertically(tSide);
        if (!sv && !tv) { const m = (s.x + t.x) / 2; return [s, { x: m, y: s.y }, { x: m, y: t.y }, t]; }
        if (sv && tv) { const m = (s.y + t.y) / 2; return [s, { x: s.x, y: m }, { x: t.x, y: m }, t]; }
        return sv ? [s, { x: s.x, y: t.y }, t] : [s, { x: t.x, y: s.y }, t];
    }

    function simplify(points) {
        const out = [];
        for (let i = 0; i < points.length; i++) {
            const p = points[i];
            if (out.length && Math.abs(out[out.length - 1].x - p.x) <= 1e-6 && Math.abs(out[out.length - 1].y - p.y) <= 1e-6) continue;
            out.push(p);
        }
        for (let i = out.length - 2; i >= 1; i--) {
            const a = out[i - 1], b = out[i], c = out[i + 1];
            if ((Math.abs(a.x - b.x) <= 1e-6 && Math.abs(b.x - c.x) <= 1e-6)
                || (Math.abs(a.y - b.y) <= 1e-6 && Math.abs(b.y - c.y) <= 1e-6)) out.splice(i, 1);
        }
        return out;
    }

    // Fast fluchtende Enden auf eine Linie ziehen - sonst ein Z mit Mini-Versatz. Siehe EdgeRouter.Align.
    function alignEnds(sourceBox, sSide, p, targetBox, tSide, q) {
        const snap = 10;
        if (leavesVertically(sSide) !== leavesVertically(tSide)) return q;
        if (leavesVertically(sSide)) {
            if (Math.abs(p.x - q.x) <= snap && Math.abs(along(p.x, targetBox.x, targetBox.x + targetBox.w) - p.x) <= 1e-6) {
                return { x: p.x, y: q.y };
            }
            return q;
        }
        if (Math.abs(p.y - q.y) <= snap && Math.abs(along(p.y, targetBox.y, targetBox.y + targetBox.h) - p.y) <= 1e-6) {
            return { x: q.x, y: p.y };
        }
        return q;
    }

    function routeEdge(sourceBox, sSide, sOff, targetBox, tSide, tOff, obstacles) {
        const p = anchorOf(sourceBox, sSide, sOff);
        const q = alignEnds(sourceBox, sSide, p, targetBox, tSide, anchorOf(targetBox, tSide, tOff));
        const s = advance(p, sSide, CLEARANCE);
        const t = advance(q, tSide, CLEARANCE);
        const middle = findPath(s, sSide, t, tSide, obstacles) || fallbackPath(s, sSide, t, tSide);
        return simplify([p].concat(middle).concat([q]));
    }

    function selfLoop(box) {
        const outX = box.x + box.w + 2 * CLEARANCE;
        const topY = box.y - 2 * CLEARANCE;
        const y = box.cy - box.h / 4;
        return [
            { x: box.x + box.w, y: y }, { x: outX, y: y }, { x: outX, y: topY },
            { x: box.cx, y: topY }, { x: box.cx, y: box.y }
        ];
    }

    function pathData(points) {
        let d = '';
        for (let i = 0; i < points.length; i++) {
            d += (i === 0 ? 'M' : 'L') + points[i].x + ' ' + points[i].y + ' ';
        }
        return d.trim();
    }

    function redrawEdges(svg, nodeId) {
        const obstacles = allObstacles(svg);
        svg.querySelectorAll('[data-wf-edge]').forEach(function (el) {
            const s = el.getAttribute('data-source');
            const t = el.getAttribute('data-target');
            if (s !== nodeId && t !== nodeId) return;
            const sb = nodeBox(svg, s);
            const tb = nodeBox(svg, t);
            if (!sb || !tb) return;

            let points;
            if (s === t) {
                points = selfLoop(sb);
            } else {
                const sides = chooseSides(sb, tb);
                // Der Fehlerausgang haengt am festen roten Port, nicht an der geometrisch naechsten
                // Seite - siehe GraphLayout.RouteEdges.
                const sSide = el.getAttribute('data-err') === '1' ? 'right' : sides[0];
                const sOff = parseFloat(el.getAttribute('data-src-off')) || 0;
                const tOff = parseFloat(el.getAttribute('data-tgt-off')) || 0;
                points = routeEdge(sb, sSide, sOff, tb, sides[1], tOff, obstacles);
            }

            el.setAttribute('d', pathData(points));
        });
    }

    function detachHost(hostEl) {
        const it = instances.get(hostEl);
        if (!it) return;
        it.svgEl.removeEventListener('pointerdown', it.onDown);
        it.svgEl.removeEventListener('pointermove', it.onMove);
        it.svgEl.removeEventListener('pointerup', it.onUp);
        it.svgEl.removeEventListener('pointercancel', it.onUp);
        it.svgEl.removeEventListener('wheel', it.onWheel);
        document.removeEventListener('keydown', it.onKey);
        if (it.viewTimer) clearTimeout(it.viewTimer);
        instances.delete(hostEl);
    }

    return {
        attach(hostEl, svgEl, dotNet) {
            if (!hostEl || !svgEl) return;
            // Zweiter Aufruf fuer denselben Host: erst die alten Listener loesen. Sonst haengen zwei
            // Closures mit je eigenem drag am selben SVG und ein einziger Zug meldet zwei Kanten.
            // Die .NET-Seite schuetzt sich ebenfalls (jsAttached) - hier steht der zweite Riegel.
            detachHost(hostEl);
            injectStyle();
            let drag = null;
            let lastClick = null;                 // {id, t} fuer die Doppelklick-Erkennung
            const inst = { svgEl: svgEl, viewTimer: 0 };

            // Ausschnitt nach einer Geste an .NET melden (leicht entprellt): die viewBox steht schon am
            // DOM; die Meldung sorgt nur dafuer, dass der naechste Server-Render denselben Ausschnitt haelt.
            function reportView() {
                if (inst.viewTimer) clearTimeout(inst.viewTimer);
                inst.viewTimer = setTimeout(function () {
                    inst.viewTimer = 0;
                    const v = getViewBox(svgEl);
                    dotNet.invokeMethodAsync('OnViewChanged', v.x, v.y, v.w, v.h).catch(function () { });
                }, 150);
            }

            // Einen noch ausstehenden (entprellten) Zoom-Report sofort abschicken. Wird zu Beginn jeder
            // neuen Geste gerufen: sonst koennte ein anschliessender Server-Render (z.B. nach dem
            // Verschieben eines Knotens) die viewBox auf den noch nicht gemeldeten Stand zuruecksetzen.
            function flushView() {
                if (!inst.viewTimer) return;
                clearTimeout(inst.viewTimer);
                inst.viewTimer = 0;
                const v = getViewBox(svgEl);
                dotNet.invokeMethodAsync('OnViewChanged', v.x, v.y, v.w, v.h).catch(function () { });
            }

            // Doppelklick selbst erkannt statt ueber native dblclick: die Pointer-Handler fangen die
            // Events (setPointerCapture/preventDefault) sonst teils weg. Zweiter Klick auf DASSELBE
            // Element binnen 400ms -> Bearbeiten (Popup), sonst nur Auswaehlen.
            function clickOrDouble(id, selectMethod, editMethod) {
                const now = Date.now();
                const dbl = lastClick && lastClick.id === id && (now - lastClick.t) < 400;
                lastClick = dbl ? null : { id: id, t: now };
                dotNet.invokeMethodAsync(dbl ? editMethod : selectMethod, id).catch(function () { });
            }

            function onDown(e) {
                if (e.button !== undefined && e.button !== 0) return;
                // Vor jeder neuen Geste einen offenen Zoom-Report abschliessen (siehe flushView).
                flushView();
                const portEl = e.target.closest ? e.target.closest('[data-wf-port]') : null;
                const nodeEl = e.target.closest ? e.target.closest('[data-wf-node]') : null;
                const edgeEl = e.target.closest ? e.target.closest('[data-wf-edge]') : null;

                if (portEl && nodeEl) {
                    const sourceId = nodeEl.getAttribute('data-node-id');
                    // Port-Art: 'out' = normaler Ausgang, 'err' = Fehler-Ausgang. Die .NET-Seite setzt
                    // daraus ErrorFlowId, statt den Nutzer die Kante hinterher in einem Dropdown
                    // auswaehlen zu lassen.
                    const portKind = portEl.getAttribute('data-wf-port') || 'out';
                    const box = nodeBox(svgEl, sourceId);
                    const line = document.createElementNS(SVGNS, 'line');
                    line.setAttribute('class', portKind === 'err' ? 'itv-wf-temp-edge itv-wf-temp-err' : 'itv-wf-temp-edge');
                    // Startpunkt am tatsaechlich gezogenen Port (nicht immer die rechte Mitte).
                    const pcx = parseFloat(portEl.getAttribute('cx'));
                    const pcy = parseFloat(portEl.getAttribute('cy'));
                    const start = box
                        ? {
                            x: box.x + (isNaN(pcx) ? box.w : pcx),
                            y: box.y + (isNaN(pcy) ? box.h / 2 : pcy)
                        }
                        : toSvgPoint(svgEl, e.clientX, e.clientY);
                    line.setAttribute('x1', start.x);
                    line.setAttribute('y1', start.y);
                    line.setAttribute('x2', start.x);
                    line.setAttribute('y2', start.y);
                    svgEl.appendChild(line);
                    drag = { mode: 'edge', sourceId: sourceId, portKind: portKind, tempLine: line, moved: false };
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

                // Leere Flaeche: Pan vorbereiten. Ein Klick ohne Bewegung hebt die Auswahl auf.
                drag = {
                    mode: 'pan',
                    grab: toSvgPoint(svgEl, e.clientX, e.clientY),
                    startClientX: e.clientX,
                    startClientY: e.clientY,
                    moved: false
                };
                svgEl.style.cursor = 'grabbing';
                try { svgEl.setPointerCapture(e.pointerId); } catch (_) { /* no capture */ }
            }

            function onMove(e) {
                if (!drag) return;
                const pt = toSvgPoint(svgEl, e.clientX, e.clientY);
                if (drag.mode === 'node') {
                    if (Math.abs(e.clientX - drag.startClientX) + Math.abs(e.clientY - drag.startClientY) > MOVE_THRESHOLD) {
                        drag.moved = true;
                        lastClick = null;
                    }
                    drag.lastX = pt.x - drag.offsetX;
                    drag.lastY = pt.y - drag.offsetY;
                    drag.g.setAttribute('transform', 'translate(' + drag.lastX + ',' + drag.lastY + ')');
                    redrawEdges(svgEl, drag.nodeId);
                } else if (drag.mode === 'edge') {
                    drag.moved = true;
                    drag.tempLine.setAttribute('x2', pt.x);
                    drag.tempLine.setAttribute('y2', pt.y);
                } else if (drag.mode === 'pan') {
                    if (!drag.moved
                        && Math.abs(e.clientX - drag.startClientX) + Math.abs(e.clientY - drag.startClientY) <= MOVE_THRESHOLD) {
                        return;
                    }
                    drag.moved = true;
                    // Den beim Druck gegriffenen Weltpunkt unter dem Cursor halten - exakt trotz Letterbox,
                    // weil toSvgPoint die tatsaechliche viewBox-Abbildung nutzt.
                    const v = getViewBox(svgEl);
                    v.x += drag.grab.x - pt.x;
                    v.y += drag.grab.y - pt.y;
                    setViewBox(svgEl, v);
                }
            }

            function onUp(e) {
                if (!drag) return;
                try { svgEl.releasePointerCapture(e.pointerId); } catch (_) { /* ignore */ }
                svgEl.style.cursor = '';
                const mode = drag.mode;
                const d = drag;
                drag = null;

                if (mode === 'node') {
                    if (d.moved) {
                        dotNet.invokeMethodAsync('OnNodeMoved', d.nodeId, d.lastX, d.lastY).catch(function () { });
                    } else {
                        clickOrDouble(d.nodeId, 'OnSelectNode', 'OnEditNode');
                    }
                } else if (mode === 'edge') {
                    let targetId = null;
                    const under = document.elementFromPoint(e.clientX, e.clientY);
                    const targetNode = under && under.closest ? under.closest('[data-wf-node]') : null;
                    if (targetNode) targetId = targetNode.getAttribute('data-node-id');
                    if (d.tempLine) d.tempLine.remove();
                    if (d.moved && targetId && targetId !== d.sourceId) {
                        dotNet.invokeMethodAsync('OnEdgeCreated', d.sourceId, targetId, d.portKind || 'out').catch(function () { });
                    } else {
                        dotNet.invokeMethodAsync('OnSelectNode', d.sourceId).catch(function () { });
                    }
                } else if (mode === 'selectEdge') {
                    clickOrDouble(d.edgeId, 'OnSelectEdge', 'OnEditEdge');
                } else if (mode === 'pan') {
                    if (d.moved) {
                        reportView();
                    } else {
                        lastClick = null;
                        dotNet.invokeMethodAsync('OnSelectNone').catch(function () { });
                    }
                }
            }

            // Mausrad zoomt um den Cursor (der Weltpunkt unter dem Cursor bleibt fest).
            function onWheel(e) {
                e.preventDefault();
                const p = toSvgPoint(svgEl, e.clientX, e.clientY);
                const v = getViewBox(svgEl);
                const f = e.deltaY < 0 ? 0.85 : (1 / 0.85);
                let newW = v.w * f;
                if (newW < MIN_VIEW_W) newW = MIN_VIEW_W;
                if (newW > MAX_VIEW_W) newW = MAX_VIEW_W;
                const ratio = v.w === 0 ? 1 : newW / v.w;
                v.x = p.x - (p.x - v.x) * ratio;
                v.y = p.y - (p.y - v.y) * ratio;
                v.w = newW;
                v.h = v.h * ratio;
                setViewBox(svgEl, v);
                reportView();
            }

            // Entf-Taste loescht die Auswahl - aber nicht, waehrend in einem Feld/Editor getippt wird.
            function onKey(e) {
                if (e.key !== 'Delete') return;
                const a = document.activeElement;
                if (a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.isContentEditable
                    || (a.closest && a.closest('.monaco-editor')))) {
                    return;
                }
                dotNet.invokeMethodAsync('OnDeleteKey').catch(function () { });
            }

            svgEl.addEventListener('pointerdown', onDown);
            svgEl.addEventListener('pointermove', onMove);
            svgEl.addEventListener('pointerup', onUp);
            svgEl.addEventListener('pointercancel', onUp);
            svgEl.addEventListener('wheel', onWheel, { passive: false });
            document.addEventListener('keydown', onKey);
            inst.onDown = onDown;
            inst.onMove = onMove;
            inst.onUp = onUp;
            inst.onWheel = onWheel;
            inst.onKey = onKey;
            instances.set(hostEl, inst);
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
            detachHost(hostEl);
        }
    };
})();
