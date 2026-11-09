using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Cookies
{
    /// <summary>
    /// Buffers the content of server cookies (<c>ServerCookies</c>) per key and validity. A server cookie row is written
    /// once and never changed - a new value gets a new key - so a buffered content cannot become stale; it can only be
    /// deleted, which <see cref="DbCookieService"/> reports through <see cref="Remove"/>. Without this buffer every request
    /// read the same row again. Registered as a singleton next to <see cref="DbCookieService"/>, i.e. one buffer per DI
    /// container.
    /// </summary>
    /// <remarks>
    /// Entries are dropped when they expire or when they were not used for <see cref="IdleLifetime"/>, so the buffer only
    /// holds the cookies of currently active users.
    /// </remarks>
    public sealed class ServerCookieBuffer
    {
        /// <summary>How long an unused entry is kept.</summary>
        public static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);

        private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

        private readonly ConcurrentDictionary<(Guid Key, DateTime ValidThrough), Entry> entries = new();

        private long nextSweepTicks = DateTime.UtcNow.Add(SweepInterval).Ticks;

        /// <summary>
        /// Gets the content of the given cookie, reading it with <paramref name="read"/> when it is not buffered.
        /// A cookie that does not exist (read returns found=false) is not buffered.
        /// </summary>
        /// <param name="key">the key of the server cookie</param>
        /// <param name="validThrough">the validity of the server cookie (part of its primary key)</param>
        /// <param name="read">reads the cookie from the database</param>
        /// <param name="content">the content of the cookie</param>
        /// <returns>whether the cookie exists</returns>
        public bool TryGetContent(Guid key, DateTime validThrough, Func<(bool Found, string Content)> read, out string content)
        {
            var now = DateTime.UtcNow;
            SweepIfDue(now);
            var id = (key, validThrough);
            if (entries.TryGetValue(id, out var entry))
            {
                entry.LastAccessUtc = now;
                content = entry.Content;
                return true;
            }

            var (found, raw) = read();
            content = raw;
            if (found)
            {
                entries[id] = new Entry(raw) { LastAccessUtc = now };
            }

            return found;
        }

        /// <summary>
        /// Removes a cookie that was deleted from the database.
        /// </summary>
        public void Remove(Guid key, DateTime validThrough)
        {
            entries.TryRemove((key, validThrough), out _);
        }

        private void SweepIfDue(DateTime now)
        {
            var due = Interlocked.Read(ref nextSweepTicks);
            if (now.Ticks < due || Interlocked.CompareExchange(ref nextSweepTicks, now.Add(SweepInterval).Ticks, due) != due)
            {
                return;
            }

            var idleLimit = now - IdleLifetime;
            var localNow = DateTime.Now;
            foreach (var item in entries)
            {
                // ValidThrough is compared like DbCookieService does (against DateTime.Now).
                if (item.Value.LastAccessUtc < idleLimit || item.Key.ValidThrough < localNow)
                {
                    entries.TryRemove(item.Key, out _);
                }
            }
        }

        private sealed class Entry
        {
            public Entry(string content)
            {
                Content = content;
            }

            public string Content { get; }

            public DateTime LastAccessUtc { get; set; }
        }
    }
}
