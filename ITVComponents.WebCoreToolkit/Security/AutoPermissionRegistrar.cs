using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Singleton background implementation of <see cref="IAutoPermissionRegistrar"/>. Requested-but-unknown
    /// permission names are enqueued from the authorization hot-path without touching the database; a single
    /// worker task drains them after a short debounce and materializes the whole batch through one
    /// <see cref="ISecurityRepository.EnsureRequestedPermissions"/> call (hence one <c>SaveChanges</c> and one
    /// security change-signal), instead of one write-and-invalidate round-trip per permission.
    /// <para>
    /// Process-wide dedup: each distinct name is attempted once (kept in <see cref="handled"/>). A name whose
    /// batch failed is released so a later request re-enqueues it. The worker runs in a fresh DI scope with an
    /// empty (tenant-neutral) context, so the created permissions are global (<c>TenantId == null</c>).
    /// </para>
    /// </summary>
    public sealed class AutoPermissionRegistrar : IAutoPermissionRegistrar, IDisposable
    {
        // Debounce window: a fresh-database bootstrap discovers many permissions in a burst as the admin navigates.
        // Waiting briefly after the first enqueue coalesces that burst into as few batched writes as possible.
        private readonly int debounceMilliseconds;

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<AutoPermissionRegistrar> logger;
        private readonly ConcurrentDictionary<string, byte> handled = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
        private readonly object gate = new();
        private readonly SemaphoreSlim wake = new(0);
        private readonly CancellationTokenSource cts = new();
        private readonly Task worker;

        public AutoPermissionRegistrar(IServiceScopeFactory scopeFactory, ILogger<AutoPermissionRegistrar> logger,
            IOptions<AutoPermissionsOptions> options)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
            debounceMilliseconds = Math.Max(0, options?.Value?.DebounceMilliseconds ?? 100);
            worker = Task.Run(() => RunAsync(cts.Token));
        }

        /// <inheritdoc />
        public void Enqueue(IEnumerable<string> permissionNames)
        {
            if (permissionNames == null)
            {
                return;
            }

            var added = false;
            lock (gate)
            {
                foreach (var name in permissionNames)
                {
                    // handled.TryAdd is the process-wide dedup: a name enters the queue at most once until it is
                    // either persisted (stays handled) or its batch fails (released below to allow a retry).
                    if (!string.IsNullOrWhiteSpace(name) && handled.TryAdd(name, 0))
                    {
                        pending.Add(name);
                        added = true;
                    }
                }
            }

            if (added)
            {
                wake.Release();
            }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await wake.WaitAsync(ct).ConfigureAwait(false);
                    if (debounceMilliseconds > 0)
                    {
                        await Task.Delay(debounceMilliseconds, ct).ConfigureAwait(false);
                    }
                    // Consume the permits raised by every enqueue that piled up during the debounce window, so one
                    // drained batch corresponds to one worker iteration rather than one iteration per enqueue.
                    while (wake.Wait(0))
                    {
                    }

                    string[] batch;
                    lock (gate)
                    {
                        batch = pending.ToArray();
                        pending.Clear();
                    }

                    if (batch.Length != 0)
                    {
                        ProcessBatch(batch);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Auto-permission-registration worker iteration failed.");
                }
            }
        }

        private void ProcessBatch(string[] batch)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;
                // A tenant-neutral empty context: no current user/tenant, so the created permissions are global.
                sp.PrepareEmptyContext(out _);

                var options = sp.GetService<IOptions<AutoPermissionsOptions>>()?.Value;
                if (options is not { Enabled: true })
                {
                    return;
                }

                var repo = sp.GetService<ISecurityRepository>();
                repo?.EnsureRequestedPermissions(batch, options);
                logger.LogInformation("Auto-registered {count} requested permission(s) in the background.", batch.Length);
            }
            catch (Exception ex)
            {
                // Release the names so a later authorization request re-enqueues and retries them.
                foreach (var name in batch)
                {
                    handled.TryRemove(name, out _);
                }

                logger.LogError(ex, "Auto-permission-registration batch failed; the affected permissions will be retried on a later request.");
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            try
            {
                worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore shutdown races
            }

            cts.Dispose();
            wake.Dispose();
        }
    }
}
