namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Options
{
    /// <summary>
    /// WebPart-level switch for the HelpViews Blazor library. When <see cref="ConfigureContext"/> is set,
    /// <c>WebPartInit</c> wires the help handlers + the anonymous resource endpoint over the host DbContext.
    /// The context type is taken from the shared <c>SecurityContextOptions.ContextType</c> — a context that hosts
    /// the help tables (<c>IHelpSystemContext</c>) is always at least an <c>ICoreSystemContext</c> — so it is not
    /// repeated here. Bound from the WebPart config (not the scoped/global settings infrastructure), hence no
    /// <c>[SettingName]</c>.
    /// </summary>
    public class HelpViewOptions
    {
        /// <summary>When true, the help handlers and the resource endpoint are auto-wired over the host context.</summary>
        public bool ConfigureContext { get; set; }
    }
}
