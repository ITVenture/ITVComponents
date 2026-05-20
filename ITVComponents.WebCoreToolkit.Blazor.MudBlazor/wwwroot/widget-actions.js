// Minimal click-delegation helper for WidgetRenderer (Scriban-rendered HTML templates).
// Listens for clicks on `[data-widget-action]` elements within the widget root,
// forwards action name + optional arg back to the .NET widget via DotNet reference.
// Each widget root carries its own DotNetObjectReference and its own listener so multiple
// widgets in a dashboard don't interfere with each other.

window.itvWidgets = window.itvWidgets || {
    attachActions(el, dotNet) {
        if (!el) return;
        const handler = (e) => {
            const btn = e.target.closest('[data-widget-action]');
            if (!btn || !el.contains(btn)) return;
            e.preventDefault();
            const action = btn.dataset.widgetAction;
            const arg = btn.dataset.widgetArg ?? null;
            dotNet.invokeMethodAsync('InvokeAction', action, arg).catch(() => { /* circuit may be gone */ });
        };
        el._itvWidgetHandler = handler;
        el.addEventListener('click', handler);
    },

    detachActions(el) {
        if (!el || !el._itvWidgetHandler) return;
        el.removeEventListener('click', el._itvWidgetHandler);
        delete el._itvWidgetHandler;
    }
};
