using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Off-hot-path sink for the auto-permission-registration bootstrap. The authorization gate hands the
    /// requested permission names to <see cref="Enqueue"/> (cheap, no database access) instead of writing inline;
    /// a background worker coalesces the enqueued names across all requests/circuits and materializes them in a
    /// single batched write. This removes the per-permission write-and-invalidate storm that made a fresh-database
    /// bootstrap sluggish (each inline write raised the security change-signal, forcing a full permission-scope
    /// re-resolution and a SecureView re-render fan-out on every circuit).
    /// </summary>
    public interface IAutoPermissionRegistrar
    {
        /// <summary>
        /// Registers permission names that a real authorization gate requested but that may not exist yet. Names
        /// already handled in this process are ignored; the rest are batched and created (and granted to the
        /// configured global role) by the background worker.
        /// </summary>
        /// <param name="permissionNames">the permission names requested by the current authorization check</param>
        void Enqueue(IEnumerable<string> permissionNames);
    }
}
