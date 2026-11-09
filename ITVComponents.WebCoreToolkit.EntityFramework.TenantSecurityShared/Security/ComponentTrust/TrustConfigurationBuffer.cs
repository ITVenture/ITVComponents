using System;
using System.Collections.Concurrent;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security.ComponentTrust
{
    /// <summary>
    /// Buffers the configured trust (<c>TrustedFullAccessComponents.TrustLevelConfig</c>) per pair of trusted and
    /// trusting type. <see cref="DbSecurityAccessProvider"/> is scoped, so without this buffer every request read the
    /// same configuration rows again. Registered as a singleton next to the provider, i.e. one buffer per DI
    /// container; a missing row ("not trusted") is buffered as well.
    /// </summary>
    /// <remarks>
    /// There is no change signal on this branch, so a changed trust configuration takes effect after at most
    /// <see cref="Lifetime"/>.
    /// </remarks>
    public sealed class TrustConfigurationBuffer
    {
        /// <summary>How long a read configuration is used before it is read again.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        private readonly ConcurrentDictionary<(string Trusted, string Trusting), Entry> entries = new();

        /// <summary>
        /// Gets the trust configuration of the given pair, reading it with <paramref name="read"/> when it is not
        /// buffered or has expired.
        /// </summary>
        /// <param name="trustedType">the assembly-qualified name of the trusted (calling) type</param>
        /// <param name="trustingType">the assembly-qualified name of the trusting type</param>
        /// <param name="read">reads the configuration; returns found=false when no row exists</param>
        /// <param name="found">whether a configuration row exists</param>
        /// <returns>the configuration json, or null when none exists</returns>
        public string GetTrustConfig(string trustedType, string trustingType, Func<(bool Found, string Config)> read, out bool found)
        {
            var now = DateTime.UtcNow;
            var entry = entries.AddOrUpdate((trustedType, trustingType),
                _ => Read(read, now),
                (_, existing) => existing.ValidUntilUtc < now ? Read(read, now) : existing);
            found = entry.Found;
            return entry.Config;
        }

        private static Entry Read(Func<(bool Found, string Config)> read, DateTime now)
        {
            var (found, config) = read();
            return new Entry(found, config, now.Add(Lifetime));
        }

        private sealed record Entry(bool Found, string Config, DateTime ValidUntilUtc);
    }
}
