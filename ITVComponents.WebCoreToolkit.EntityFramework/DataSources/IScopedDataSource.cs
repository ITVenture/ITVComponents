using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources
{
    /// <summary>
    /// Optional return-value for a diagnostics-/foreign-key data-source factory delegate
    /// (<c>ContextResolveOptions.RegisterService</c>) when the actual data-source was produced inside a
    /// per-operation scope that must be torn down once the query is finished — e.g. a plugin loaded through
    /// <c>IWebPluginHelper.CreateOperationScope()</c> together with its per-operation DbContext.
    ///
    /// <para>When a factory delegate returns an <see cref="IScopedDataSource"/>, the resolving wrapper
    /// (<c>ContextForDiagnosticsQuery</c>/<c>ContextForFkQuery</c>) wraps <see cref="Source"/> and takes ownership
    /// of <see cref="Scope"/>: disposing the returned wrapped source disposes the scope (which in turn disposes the
    /// scoped plugins + their per-operation contexts). Returning a plain source (not scoped) keeps the historic
    /// behaviour: nothing is disposed by the wrapper (the source is host-/DI-owned).</para>
    /// </summary>
    public interface IScopedDataSource
    {
        /// <summary>The actual data-source (a <c>DbContext</c>, <c>DynamicDataAdapter</c> or <c>IForeignKeyProvider</c>).</summary>
        object Source { get; }

        /// <summary>The scope owning the source's lifetime; disposed once the wrapped source is disposed.</summary>
        IDisposable Scope { get; }
    }

    /// <summary>Default <see cref="IScopedDataSource"/> implementation.</summary>
    public sealed class ScopedDataSource : IScopedDataSource
    {
        /// <summary>Initializes a new instance pairing a data-source with the scope owning its lifetime.</summary>
        public ScopedDataSource(object source, IDisposable scope)
        {
            Source = source;
            Scope = scope;
        }

        /// <inheritdoc/>
        public object Source { get; }

        /// <inheritdoc/>
        public IDisposable Scope { get; }
    }
}
