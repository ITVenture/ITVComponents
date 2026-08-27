using System;
using System.Linq;
using System.Security.Claims;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Was der Riegel am Ende eines Vorgangs ins Protokoll schreibt.
    /// <para>
    /// Eigener Helfer, weil es zwei Riegel gibt - den MVC-Filter und die Blazor-Komponente - und sie
    /// dasselbe mitschreiben muessen. Zwei Fassungen waeren zwei Wahrheiten darueber, was ein Zugriff
    /// ist.
    /// </para>
    /// </summary>
    public static class AssetAccessRecorder
    {
        /// <summary>
        /// Schreibt einen abgeschlossenen Vorgang mit, sofern die Vorlage es verlangt.
        /// </summary>
        /// <param name="log">das Protokoll, oder null - dann passiert nichts</param>
        /// <param name="context">der Asset-Kontext des Vorgangs</param>
        /// <param name="user">wer zugegriffen hat</param>
        /// <param name="requestPath">der angefragte Pfad</param>
        public static void Record(IAssetAccessLog log, ISharedAssetContext context, ClaimsPrincipal user,
            string requestPath)
        {
            var asset = context?.CurrentAsset;
            if (log == null || asset == null)
            {
                return;
            }

            var granted = !context.MustHoldBack;
            if (asset.AuditMode == AssetAuditMode.Off
                || (asset.AuditMode == AssetAuditMode.DeniedOnly && granted))
            {
                return;
            }

            log.Record(new AssetAccessEntry
            {
                AssetKey = asset.TicketNonce == null ? asset.AssetKey : null,
                TicketNonce = asset.TicketNonce,
                TemplateSystemKey = asset.TemplateSystemKey,
                TenantName = asset.UserScopeName,
                RecipientLabel = asset.RecipientLabel,
                AccessedBy = user?.Identity?.Name,
                RequestPath = requestPath,
                ArgumentSummary = Summarize(asset),
                Granted = granted,
                // Kurz und maschinenlesbar: "wurde nicht bestaetigt" und "hat auf etwas Fremdes gezeigt"
                // sind zwei verschiedene Vorfaelle, und nur der zweite ist ein Alarmzeichen.
                DenyReason = granted ? null : context.Denied ? "Denied" : "NotConfirmed"
            });
        }

        private static string Summarize(AssetInfo asset)
        {
            var values = asset.Values;
            return values == null || values.IsEmpty
                ? null
                : string.Join(", ", values.Names.Select(n => $"{n}={values[n]}"));
        }
    }
}
