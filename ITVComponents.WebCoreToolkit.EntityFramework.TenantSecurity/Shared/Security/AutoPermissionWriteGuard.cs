using System;
using ITVComponents.EFRepo.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security
{
    /// <summary>
    /// Hardening for the auto-permission-registration write. Registration is a background bootstrap step whose
    /// rows (a new global permission and its grant to the configured bootstrap role) no active session is waiting
    /// on, so its <c>SaveChanges</c> must not behave like an interactive security change:
    /// <list type="bullet">
    /// <item>it runs with write-tracking suppressed, so it does NOT bump the security change-signal — otherwise
    /// every circuit drops its memoized permission scope and re-resolves INLINE on the render thread, turning a
    /// single background seed into a synchronous permission-read storm that collides with the registration write
    /// itself; and</item>
    /// <item>it runs fail-fast (bounded command timeout) and treats a unique-constraint violation as an idempotent
    /// success, so a race with a concurrent/external writer (a second instance, a static seeder) neither waits out
    /// the full default command timeout (typically 60s) nor surfaces as an error.</item>
    /// </list>
    /// </summary>
    internal static class AutoPermissionWriteGuard
    {
        /// <summary>
        /// Persists the pending auto-registration changes on <paramref name="context"/> fail-fast, tracking-
        /// suppressed and idempotent against unique-constraint races. A genuine (non-duplicate) failure is
        /// rethrown so the caller's batch is released and retried on a later request.
        /// </summary>
        public static void SaveIdempotent(DbContext context, ILogger logger, int commandTimeoutSeconds)
        {
            if (context == null)
            {
                return;
            }

            // Fail-fast: a contended write aborts after a bounded wait instead of blocking for the connection's
            // default command timeout; the batching registrar re-enqueues on the next request, so a transient
            // contention window costs a bounded delay rather than a stalled render.
            if (commandTimeoutSeconds > 0)
            {
                context.Database.SetCommandTimeout(TimeSpan.FromSeconds(commandTimeoutSeconds));
            }

            // Suppress write-tracking for exactly this save: the new permission/grant is not something any live
            // session is blocked on, so raising the security change-signal here only forces redundant re-resolves.
            using (EntityWriteTrackerInterceptor.SuppressTracking())
            {
                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateException due) when (IsUniqueConstraintViolation(due))
                {
                    // A concurrent/external writer already created the same global permission (or grant). The
                    // desired end-state exists, so this is an idempotent success — do not rethrow (which would
                    // release the batch for a pointless retry).
                    logger?.LogDebug(due,
                        "Auto-permission-registration hit a unique-constraint race; the permission/grant already exists (treated as idempotent success).");
                }
            }
        }

        /// <summary>
        /// Provider-neutral detection of a unique/primary-key violation anywhere in the exception chain, without a
        /// hard dependency on a specific ADO.NET provider assembly.
        /// </summary>
        private static bool IsUniqueConstraintViolation(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                var type = e.GetType();

                // SqlServer: 2627 (PK/unique constraint violation), 2601 (duplicate key in a unique index).
                if (type.GetProperty("Number")?.GetValue(e) is int number && (number == 2627 || number == 2601))
                {
                    return true;
                }

                // ANSI SQLSTATE 23505 = unique_violation (Npgsql and other standard-conforming providers).
                if (type.GetProperty("SqlState")?.GetValue(e) is string sqlState && sqlState == "23505")
                {
                    return true;
                }

                // SQLite: primary result code 19 = SQLITE_CONSTRAINT.
                if (type.GetProperty("SqliteErrorCode")?.GetValue(e) is int sqliteError && sqliteError == 19)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
