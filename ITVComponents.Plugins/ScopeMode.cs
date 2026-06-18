namespace ITVComponents.Plugins
{
    /// <summary>
    /// Controls how a <see cref="PluginFactory"/> tracks its currently-active <see cref="PluginScope"/>.
    /// </summary>
    public enum ScopeMode
    {
        /// <summary>
        /// The active scope is bound to the current thread (<see cref="System.Threading.ThreadLocal{T}"/>).
        /// Does NOT flow across <c>await</c>-boundaries. Default — preserves the historic behaviour.
        /// </summary>
        PerThread,

        /// <summary>
        /// The active scope flows with the logical (async) call-context
        /// (<see cref="System.Threading.AsyncLocal{T}"/>), so it survives <c>await</c>-boundaries. Use this for
        /// async unit-of-work scopes (e.g. Blazor per-operation plugin scopes).
        /// </summary>
        PerAsyncContext
    }
}
