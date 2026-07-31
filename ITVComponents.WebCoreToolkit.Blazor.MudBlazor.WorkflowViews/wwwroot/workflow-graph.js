// Klick-Interaktion fuer die READ-ONLY-Graphansicht (WorkflowGraph.razor).
// Blazor rendert das SVG als MarkupString; einzelne Knoten tragen <g data-wf-node="id">. Statt je Knoten
// einen Blazor-Handler zu haengen (die gaebe es im MarkupString gar nicht), haengt dieses Modul EINEN
// Listener an den Container und delegiert ueber closest() - das ueberlebt auch jedes Neu-Rendern des
// SVG-Koerpers, weil der Container derselbe bleibt.
//
// Bewusst getrennt von graph-editor.js: der Editor treibt Drag/Drop und Kantenbau, hier geht es nur um
// "welcher Knoten wurde angetippt". Gemeinsam ist nur die data-wf-node-Konvention.

window.itvWfGraph = window.itvWfGraph || (function () {
    const attached = new WeakMap();

    function injectStyle() {
        if (document.getElementById('itv-wf-graph-style')) return;
        const style = document.createElement('style');
        style.id = 'itv-wf-graph-style';
        // Bewusst ueber die Container-Klasse gescoped: der Editor setzt fuer dieselben data-wf-node ein
        // grab-Cursor-Styling: ohne Scope wuerden sich die beiden auf einer Seite widersprechen.
        style.textContent =
            '.itv-wf-clickable [data-wf-node] { cursor: pointer; }' +
            '.itv-wf-clickable [data-wf-node]:hover { filter: brightness(1.08); }';
        document.head.appendChild(style);
    }

    function nodeIdFrom(target) {
        const g = target && target.closest ? target.closest('[data-wf-node]') : null;
        return g ? g.getAttribute('data-wf-node') : null;
    }

    return {
        // host: der Container-DIV, dotNetRef: die Komponente mit [JSInvokable] OnNodeActivated/OnNodeSelected.
        attach: function (host, dotNetRef) {
            if (!host || attached.has(host)) return;
            attached.set(host, true);
            injectStyle();
            host.classList.add('itv-wf-clickable');

            host.addEventListener('dblclick', function (e) {
                const id = nodeIdFrom(e.target);
                if (!id) return;
                e.preventDefault();
                // Ein Doppelklick auf einen Knoten markiert im Browser sonst den Text darunter.
                if (window.getSelection) { window.getSelection().removeAllRanges(); }
                dotNetRef.invokeMethodAsync('OnNodeActivated', id);
            });

            host.addEventListener('click', function (e) {
                const id = nodeIdFrom(e.target);
                if (!id) return;
                dotNetRef.invokeMethodAsync('OnNodeSelected', id);
            });
        },

        detach: function (host) {
            if (!host) return;
            attached.delete(host);
            host.classList.remove('itv-wf-clickable');
        }
    };
})();
