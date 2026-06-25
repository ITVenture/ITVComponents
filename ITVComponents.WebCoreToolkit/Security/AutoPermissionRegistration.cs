using System;
using System.Collections.Concurrent;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Process-wide configuration and dedup-state for the optional "auto-register requested permissions"
    /// bootstrap feature. When <see cref="Enabled"/> is set (through the tenant-security WebPart's
    /// ActivationSettings), an authorization check for a permission that does not yet exist creates that
    /// permission (global, <c>TenantId == null</c>) and — if configured — grants it to the
    /// <see cref="GrantToGlobalRole"/> global role. This lets a fresh database self-populate its permission
    /// catalogue (and an admin role) simply by an administrator navigating the application, removing the need
    /// to keep a static permission seed in sync between the toolkit and its consumers.
    /// <para>
    /// The dedup set guarantees that each distinct permission name triggers at most one database ensure-attempt
    /// per process lifetime, keeping the authorization hot-path essentially free once a name has been handled.
    /// </para>
    /// </summary>
    public static class AutoPermissionRegistration
    {
        private static readonly ConcurrentDictionary<string, byte> handled = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets a value indicating whether requested-but-unknown permissions are auto-created on the fly.
        /// Off by default; set from the tenant-security WebPart configuration.
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the name of the global role that auto-created (and already-existing) permissions are
        /// granted to. When null/empty, permissions are created without a grant.
        /// </summary>
        public static string GrantToGlobalRole { get; set; }

        /// <summary>
        /// Claims the first attempt to ensure the given permission name. Returns <c>true</c> exactly once per
        /// name (until <see cref="ReleaseClaim"/> is called for it), so callers can skip the database round-trip
        /// for names that were already handled in this process.
        /// </summary>
        /// <param name="permissionName">the permission name to claim</param>
        /// <returns><c>true</c> if the caller now owns the (single) ensure-attempt for this name</returns>
        public static bool TryClaim(string permissionName) => handled.TryAdd(permissionName, 0);

        /// <summary>
        /// Releases a previously obtained claim so the name is retried on a later request. Used when the
        /// ensure-write failed, so a transient error does not permanently suppress the registration.
        /// </summary>
        /// <param name="permissionName">the permission name to release</param>
        public static void ReleaseClaim(string permissionName) => handled.TryRemove(permissionName, out _);
    }
}
