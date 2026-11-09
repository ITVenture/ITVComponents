using System;
using System.Collections.Concurrent;
using ITVComponents.DIServices;

namespace ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels
{
    /// <summary>
    /// Buffers values per key with a time-to-live (30 minutes unless the caller says otherwise). <c>null</c> is
    /// buffered as well, so "does not exist" costs one read per key and period, too.
    /// </summary>
    internal class TimedObjectProvider:IObjectProvider
    {
        private readonly ConcurrentDictionary<string, BufferedObject> objectBuffer =
            new ConcurrentDictionary<string, BufferedObject>();

        private readonly Func<DateTime> lastSourceChangeUtc;

        /// <summary>
        /// Initializes a new instance of the TimedObjectProvider class
        /// </summary>
        /// <param name="lastSourceChangeUtc">
        /// Optional: tells when the data behind the buffered values last changed (UTC). An entry whose reading
        /// began before that point is read again, regardless of its time-to-live. Without it, only the
        /// time-to-live applies.
        /// </param>
        public TimedObjectProvider(Func<DateTime> lastSourceChangeUtc = null)
        {
            this.lastSourceChangeUtc = lastSourceChangeUtc;
        }

        public T GetBufferedValue<T>(string key, out DateTime validThrough)
        {
            if (objectBuffer.TryGetValue(key, out var v) && !IsOutdated(v, LastSourceChange()))
            {
                validThrough = v.ValidThrough;
                return (T)v.Value;
            }

            validThrough = DateTime.MinValue;
            return default(T);
        }

        public T GetBufferedValue<T>(string key, Func<string, T> defaultValue, Func<DateTime> validUntil)
        {
            var lastChange = LastSourceChange();
            var buffer = objectBuffer.AddOrUpdate(key,
                k => Load(k, defaultValue, validUntil),
                (k, x) => x.ValidThrough < DateTime.Now || IsOutdated(x, lastChange)
                    ? Load(k, defaultValue, validUntil)
                    : x);

            return (T)buffer.Value;
        }

        public T UpdateBufferedValue<T>(string key, T value, DateTime? validThrough = null)
        {
            var now = DateTime.UtcNow;
            return (T)objectBuffer.AddOrUpdate(key,
                k => new BufferedObject(value, validThrough ?? DateTime.Now.AddMinutes(30), now),
                (k, x) => new BufferedObject(value, validThrough ?? DateTime.Now.AddMinutes(30), now)).Value;
        }

        private static BufferedObject Load<T>(string key, Func<string, T> defaultValue, Func<DateTime> validUntil)
        {
            // Taken BEFORE reading: a change written while the value is being read must make it stale.
            var started = DateTime.UtcNow;
            var value = defaultValue(key);
            return new BufferedObject(value, validUntil?.Invoke() ?? DateTime.Now.AddMinutes(30), started);
        }

        private static bool IsOutdated(BufferedObject entry, DateTime lastChangeUtc)
            => entry.LoadStartedUtc < lastChangeUtc;

        private DateTime LastSourceChange()
            => lastSourceChangeUtc?.Invoke() ?? DateTime.MinValue;
    }
}
