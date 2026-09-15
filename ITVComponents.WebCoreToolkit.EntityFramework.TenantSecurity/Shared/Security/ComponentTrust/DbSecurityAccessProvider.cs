using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust
{
    public class DbSecurityAccessProvider:ISecurityAccessProvider
    {
        private readonly IServiceProvider services;
        private ICoreSystemContext securityDb;

        // Scoped (per circuit/request) cache of the trust-component table, keyed by (trustedTypeAQN, targetTypeAQN)
        // -> TrustLevelConfig JSON. The component set is global config that rarely changes; CreateForCaller is hit
        // dozens of times per render, and each previously ran a FirstOrDefault DB query against the *shared* scoped
        // DbContext. Serving from this cache removes those queries from the security hot path (and with them the
        // "a second operation was started on this context instance" crash when parallel Blazor lifecycle callbacks
        // hit the shared context concurrently). The one-time load runs on a dedicated, short-lived context instance
        // (never the shared one) so the load itself can't collide either.
        private ConcurrentDictionary<(string trusted, string target), string> trustConfigCache;
        private readonly object trustCacheLock = new();

        // Der erklaerende Zusatz wird nur beim ERSTEN Nichttreffer eines Paares mitgeschrieben: ein
        // Nichttreffer wiederholt sich tausendfach (jeder Aufruf des nicht vertrauten Typs erzeugt einen),
        // und der Zusatz nennt vollstaendige assembly-qualifizierte Namen - bei den Typen des
        // Sicherheitskontexts einige Kilobyte. Tausendmal wiederholt waere die Diagnose das naechste Problem.
        private readonly ConcurrentDictionary<(string trusted, string target), bool> reportedMisses = new();

        // Resolved once, not per call: CreateForCaller runs dozens of times per render, and pulling a logger
        // out of the container each time would cost more than the message it writes.
        private ILogger callLogger;
        private bool callLoggerResolved;

        public DbSecurityAccessProvider(IServiceProvider services)
        {
            this.services = services;
        }

        private ICoreSystemContext SecurityDb => (securityDb ??= services.GetService<ICoreSystemContext>());

        /// <summary>
        /// The logger for the trust decision, under a fixed category so a log-level filter can name it.
        /// </summary>
        private ILogger CallLogger
        {
            get
            {
                if (!callLoggerResolved)
                {
                    callLoggerResolved = true;
                    callLogger = services.GetService<ILoggerFactory>()?.CreateLogger(
                        "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust.DbSecurityAccessProvider");
                }

                return callLogger;
            }
        }

        /// <summary>
        /// Resolves the real caller type when the stack-frame's declaring type is a compiler-generated container.
        /// Lambdas / anonymous methods / async-iterator state machines live in nested, compiler-generated types
        /// (<c>&lt;&gt;c</c>, <c>&lt;&gt;c__DisplayClassNN_M</c>, <c>&lt;Method&gt;d__NN</c>); these are never
        /// registered for trust. Their <see cref="Type.DeclaringType"/> is the class that actually owns the code,
        /// so we walk outward until we reach a non-generated type (the open generic definition for generic owners,
        /// which matches how trust components are keyed). For a normal caller this is a no-op.
        /// </summary>
        private static Type NormalizeCallerType(Type type)
        {
            while (type?.DeclaringType != null && IsCompilerGenerated(type))
            {
                type = type.DeclaringType;
            }

            return type;
        }

        /// <summary>
        /// True for compiler-generated containers (lambda/closure display classes, anonymous types,
        /// async-iterator state machines). The <c>&lt;</c> in the name is decisive on its own — it is illegal in
        /// C# source identifiers — but we check the attribute first as the canonical signal.
        /// </summary>
        private static bool IsCompilerGenerated(Type type)
            => type != null &&
               (type.IsDefined(typeof(CompilerGeneratedAttribute), false) || type.Name.IndexOf('<') >= 0);

        /// <summary>
        /// Lazy fallback for the (near-never) case where <see cref="NormalizeCallerType"/> dead-ends on a
        /// compiler-generated type without a declaring type. Walks further up the call stack (no file info, so no
        /// PDB cost) and returns the first frame whose owning type is a real, external type. Frames belonging to
        /// this provider are skipped, so the result is robust against inlining / off-by-one skip counts.
        /// </summary>
        private static Type ResolveRealCallerType()
        {
            var trace = new StackTrace(1, false);
            for (var i = 0; i < trace.FrameCount; i++)
            {
                var t = NormalizeCallerType(trace.GetFrame(i)?.GetMethod()?.DeclaringType);
                if (t != null && !IsCompilerGenerated(t) && t != typeof(DbSecurityAccessProvider))
                {
                    return t;
                }
            }

            return null;
        }

        private string ResolveTrustLevelConfig(string trustedTypeName, string trustingTypeName)
        {
            if (trustConfigCache == null)
            {
                lock (trustCacheLock)
                {
                    if (trustConfigCache == null)
                    {
                        var dict = new ConcurrentDictionary<(string, string), string>();
                        foreach (var c in LoadTrustEntries())
                        {
                            dict[(c.FullQualifiedTypeName, c.TargetQualifiedTypeName)] = c.TrustLevelConfig;
                        }

                        trustConfigCache = dict;
                    }
                }
            }

            return trustConfigCache.TryGetValue((trustedTypeName, trustingTypeName), out var cfg) ? cfg : null;
        }

        /// <summary>
        /// Laedt die Vertrauens-Tabelle auf einer eigenen, kurzlebigen Kontext-Instanz.
        /// </summary>
        /// <remarks>
        /// Nie auf dem geteilten Scope-Kontext: der Ladevorgang liefe sonst in dieselbe Nebenlaeufigkeit wie
        /// alles andere. <c>IgnoreQueryFilters</c> haelt ihn mandantenfrei (die Tabelle ist global) und
        /// verhindert, dass er die Mandanten-/Scope-Aufloesung erneut betritt.
        /// </remarks>
        private List<TrustedFullAccessComponent> LoadTrustEntries()
        {
            var loadCtx = (ICoreSystemContext)ActivatorUtilities.CreateInstance(services, SecurityDb.GetType());
            using (loadCtx as IDisposable)
            {
                return loadCtx.TrustedFullAccessComponents.IgnoreQueryFilters().ToList();
            }
        }

        /// <summary>
        /// Sagt beim Nichttreffer, was statt des gesuchten Namens in der Tabelle steht.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der teure Teil an einem gebrochenen Vertrauens-Eintrag ist nicht der Bruch, sondern die Suche: die
        /// Meldung, die ein Betreiber meldet, ist die <b>zweite</b> ("keine passenden Bereiche fuer den
        /// Benutzer") und zeigt auf Rechte und Mandanten - wo alles richtig ist. Die Antwort steht schon in
        /// der ersten Meldung, aber die Stelligkeit darin liest man als Rauschen, nicht als Nutzlast.
        /// </para>
        /// <para>
        /// Die Tabelle liegt beim Fehlschlag vollstaendig im Puffer; der Vergleich ueber den
        /// stelligkeitsfreien Namen kostet also nichts ausser der Suche selbst - und die laeuft je
        /// Schluesselpaar nur einmal.
        /// </para>
        /// </remarks>
        private string DescribeTrustMiss(string trustedTypeName, string trustingTypeName)
        {
            if (!reportedMisses.TryAdd((trustedTypeName, trustingTypeName), true))
            {
                return string.Empty;
            }

            var key = (trusted: trustedTypeName, target: trustingTypeName);
            {
                var cache = trustConfigCache;
                if (cache == null)
                {
                    return string.Empty;
                }

                var trustedKey = TrustTypeName.GetComparisonKey(key.trusted);
                var trustingKey = TrustTypeName.GetComparisonKey(key.target);
                foreach (var candidate in cache.Keys)
                {
                    if (!string.Equals(TrustTypeName.GetComparisonKey(candidate.trusted), trustedKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Derselbe Typ ist eingetragen - nur anders geschrieben. Ob auch das Ziel passt, gehoert
                    // in die Meldung: sonst sucht man am falschen der beiden Namen weiter.
                    var targetMatches = string.Equals(candidate.target, key.target, StringComparison.Ordinal);
                    var targetSameType = targetMatches || string.Equals(
                        TrustTypeName.GetComparisonKey(candidate.target), trustingKey, StringComparison.Ordinal);
                    var sb = new StringBuilder();
                    sb.Append(" A trust entry for the SAME type exists, but with ")
                        .Append(TrustTypeName.DescribeDifference(key.trusted, candidate.trusted))
                        .Append(": '").Append(candidate.trusted).Append("'.");
                    if (!targetMatches && targetSameType)
                    {
                        sb.Append(" Its target type differs in the same way: '").Append(candidate.target).Append("'.");
                    }
                    else if (!targetMatches)
                    {
                        sb.Append(" Its target type is a different one ('").Append(candidate.target)
                            .Append("'), so it would not have matched anyway.");
                    }

                    sb.Append(" This usually means the entry predates a toolkit upgrade that changed the entity set of the security context. ")
                        .Append("Fix the stored row so it carries the caller name reported at the start of this message (do not add a second row - the pair is unique), ")
                        .Append("and call ISecurityAccessProvider.ValidateTrustEntries() on startup to see this at deployment time instead of at the first user.");
                    return sb.ToString();
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Loest jede Zeile der Vertrauens-Tabelle gegen die geladenen Assemblies auf und meldet die, die zur
        /// Laufzeit nie treffen werden.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Geprueft wird <b>nicht bloss, ob sich der Typ laden laesst</b>, sondern ob der gespeicherte Name
        /// zeichengenau dem entspricht, was der geladene Typ als <see cref="Type.AssemblyQualifiedName"/>
        /// fuehrt - denn genau so schlaegt <see cref="ResolveTrustLevelConfig"/> nach. Ein Typ kann sich
        /// laden lassen (die Bindung ist versionstolerant) und der Eintrag trotzdem nie greifen.
        /// </para>
        /// <para>
        /// Liest bewusst frisch aus der Datenbank und nicht aus dem Puffer: wer diese Methode ruft, will den
        /// Stand der Ablage wissen, nicht den Stand des Puffers.
        /// </para>
        /// </remarks>
        public TrustEntryValidationResult ValidateTrustEntries()
        {
            List<TrustedFullAccessComponent> entries;
            try
            {
                entries = LoadTrustEntries();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"The trust entries could not be loaded for validation: {ex.OutlineException()}",
                    LogSeverity.Error,
                    "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust.DbSecurityAccessProvider");
                return TrustEntryValidationResult.NotSupported;
            }

            var diagnostics = entries
                .Select(n => new TrustEntryDiagnostic
                {
                    EntryId = n.TrustedFullAccessComponentId,
                    Description = string.IsNullOrWhiteSpace(n.Description) ? "no description" : n.Description,
                    TrustedType = TrustTypeResolver.Check(n.FullQualifiedTypeName, "trusted type"),
                    TrustingType = TrustTypeResolver.Check(n.TargetQualifiedTypeName, "target type")
                })
                .ToList();

            var result = new TrustEntryValidationResult(true, diagnostics);
            if (result.AllValid)
            {
                LogEnvironment.LogEvent(
                    $"All {diagnostics.Count} trust entries resolve against the loaded assemblies.",
                    LogSeverity.Report,
                    "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust.DbSecurityAccessProvider");
            }
            else
            {
                // Warnung, nicht Fehler: die Anwendung laeuft weiter, nur ohne die besonderen Rechte dieser
                // Eintraege. Genau das ist der Fall, den man sonst erst beim ersten Benutzer bemerkt.
                LogEnvironment.LogEvent(
                    result.Describe(),
                    LogSeverity.Warning,
                    "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust.DbSecurityAccessProvider");
            }

            return result;
        }

        public IFullSecurityAccessHelper<TTrustConfig> CreateForCaller<TTrustConfig, T>(T trustingObject, TTrustConfig desiredTrust = null) where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
        {
            var type = NormalizeCallerType(new StackFrame(1, false).GetMethod()?.DeclaringType);

            // Dead-end fallback (practically never hit: a closure is always nested in its owner, so the walk
            // above resolves it). Only if we still sit on a compiler-generated/null type do we pay for a fuller
            // stack walk — without file info (no PDB cost) — and take the first genuine external caller.
            if (type == null || IsCompilerGenerated(type))
            {
                type = ResolveRealCallerType() ?? type;
            }

            var trustingType = trustingObject.GetType();
            if (type.Assembly == trustingType.Assembly)
            {
                // Same assembly as the trusting object: full desired trust, no lookup. This is the rule that
                // moving a class between assemblies silently changes - the caller keeps compiling and keeps
                // working, only it now needs a trust component it never needed before, and the consequence
                // shows up far away as data that is simply not there.
                CallLogger?.LogDebug(
                    "Trust granted implicitly: caller {Caller} shares assembly {Assembly} with {TrustingType}.",
                    type.FullName, type.Assembly.GetName().Name, trustingType.FullName);
                return new FullSecurityAccessHelper<TTrustConfig>(trustingObject, desiredTrust ?? new TTrustConfig());
            }

            CallLogger?.LogDebug(
                "Trust looked up: caller {Caller} ({CallerAssembly}) is external to {TrustingType} ({TrustingAssembly}).",
                type.FullName, type.Assembly.GetName().Name, trustingType.FullName, trustingType.Assembly.GetName().Name);
            return CreateForCallerInternal(SecurityDb, trustingObject, trustingType, type, desiredTrust);
        }

        /*private IFullSecurityAccessHelper<TTrustConfig> CreateForCallerInternal<TTrustConfig, T>(IServiceProvider services,
            T trustingObject, Type trustingType, Type trustedType, TTrustConfig desiredTrust)
            where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
        {
            var csc = services.GetService<ICoreSystemContext>();
            return CreateForCallerInternal(csc, trustingObject, trustingType, trustedType, desiredTrust);
        }*/

        private FullSecurityAccessHelper<TTrustConfig> CreateForCallerInternal<TTrustConfig,T>(ICoreSystemContext securityDb, T trustingObject, Type trustingType, Type trustedType, TTrustConfig desiredTrust)
            where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
        {
            // Trust lookup served from the scoped cache (loaded once on a dedicated context) instead of querying
            // the shared scoped DbContext on every call — see trustConfigCache.
            var trustLevelConfig = ResolveTrustLevelConfig(trustedType.AssemblyQualifiedName, trustingType.AssemblyQualifiedName);

            var configuredTrust = new TTrustConfig();
            if (trustLevelConfig != null)
            {
                configuredTrust =
                    JsonHelper.FromJsonString<TTrustConfig>(trustLevelConfig, SerializationTypingMode.StaticTyping);

            }
            else
            {
                LogEnvironment.LogEvent(
                    $"No Trust Configuration found for the caller ({trustedType.AssemblyQualifiedName}) on {TrustTypeName.GetComparisonKey(trustingType.AssemblyQualifiedName)}. No special permissions will be granted.{DescribeTrustMiss(trustedType.AssemblyQualifiedName, trustingType.AssemblyQualifiedName)}",
                    LogSeverity.Warning,
                    "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.FullSecurityAccessHelper");
            }
            //throw new InvalidOperationException($"The caller ({type.AssemblyQualifiedName}) is not trusted for {trustingType.AssemblyQualifiedName}!");
            TTrustConfig trustConfig =
                desiredTrust ?? configuredTrust;
            return new FullSecurityAccessHelper<TTrustConfig>(trustingObject, trustConfig.ApplySpecialFilters(configuredTrust));
        }
    }
}
