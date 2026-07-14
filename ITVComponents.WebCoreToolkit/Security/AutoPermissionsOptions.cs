namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Configuration for the optional "auto-register requested permissions" bootstrap feature. Resolved through
    /// the DI options pipeline (<c>IOptions&lt;AutoPermissionsOptions&gt;</c>); configure it with
    /// <c>services.Configure&lt;AutoPermissionsOptions&gt;(o =&gt; { o.Enabled = true; ... })</c> — the
    /// tenant-security WebPart wires this from its ActivationSettings.
    /// <para>
    /// When <see cref="Enabled"/> is set, an authorization check for a permission that does not yet exist creates
    /// that permission (global, <c>TenantId == null</c>) and — if <see cref="GrantToGlobalRole"/> is set — grants
    /// it to that global role. This lets a fresh database self-populate its permission catalogue (and an admin
    /// role) simply by an administrator navigating the application, removing the need to keep a static permission
    /// seed in sync between the toolkit and its consumers.
    /// </para>
    /// </summary>
    public class AutoPermissionsOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether requested-but-unknown permissions are auto-created on the fly.
        /// Off by default.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the name of the global role that auto-created (and already-existing requested) permissions
        /// are granted to. When null/empty, permissions are created without a grant.
        /// </summary>
        public string GrantToGlobalRole { get; set; }

        /// <summary>
        /// Debounce window (milliseconds) the background registrar waits after the first enqueued name before it
        /// writes the batch. It coalesces the burst of permissions a page requests in one render into a single
        /// write/notification; a nested-gate reveal on a first visit then costs one such window per reveal-wave.
        /// Lower = snappier first visit, but too low risks splitting one render's requests into several batches
        /// (each with its own notification). Default 100.
        /// </summary>
        public int DebounceMilliseconds { get; set; } = 100;

        /// <summary>
        /// Command timeout (seconds) applied to the background registration write. Bounds how long the write may
        /// wait on lock/schema contention before it aborts and is retried on a later request — instead of blocking
        /// for the connection's default command timeout (typically 60s), which would otherwise coincide with the
        /// permission-read on the render hot-path. Must be &gt; 0 to take effect; default 15.
        /// </summary>
        public int WriteCommandTimeoutSeconds { get; set; } = 15;
    }
}
