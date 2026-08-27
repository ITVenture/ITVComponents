using System;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.SharedAssets
{
    /// <summary>
    /// Schreibt Zugriffe auf Freigaben in die Systemtabelle.
    /// </summary>
    public class DbAssetAccessLog : IAssetAccessLog
    {
        private readonly ICoreSystemContextFactory contextFactory;

        public DbAssetAccessLog(ICoreSystemContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <inheritdoc/>
        public void Record(AssetAccessEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.TenantName))
            {
                return;
            }

            try
            {
                var db = contextFactory.CreateContext();
                try
                {
                    db.ShowAllTenants = true;
                    db.SharedAssetAccesses.Add(new SharedAssetAccess
                    {
                        AssetKey = Trim(entry.AssetKey, 128),
                        TicketNonce = Trim(entry.TicketNonce, 64),
                        TemplateSystemKey = Trim(entry.TemplateSystemKey, 128),
                        TenantName = Trim(entry.TenantName, 128),
                        RecipientLabel = Trim(entry.RecipientLabel, 256),
                        AccessedBy = Trim(entry.AccessedBy, 256),
                        RequestPath = Trim(entry.RequestPath, 1024),
                        ArgumentSummary = Trim(entry.ArgumentSummary, 1024),
                        Granted = entry.Granted,
                        DenyReason = Trim(entry.DenyReason, 64),
                        Created = entry.Created
                    });
                    db.SaveChanges();
                }
                finally
                {
                    (db as IDisposable)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Das Protokoll darf den Zugriff nicht aufhalten - aber es darf auch nicht stillschweigend
                // ausfallen: ein Protokoll, von dem niemand weiss, dass es Luecken hat, ist schlimmer als
                // keines.
                LogEnvironment.LogEvent(
                    $"Ein Zugriff auf die Freigabe '{entry.AssetKey ?? entry.TicketNonce}' konnte nicht protokolliert werden: {ex.OutlineException()}",
                    LogSeverity.Error);
            }
        }

        /// <summary>
        /// Kuerzt auf die Spaltenbreite. Ein zu langer Pfad soll die Zeile nicht verhindern - das
        /// Protokoll ist eine Auskunft und kein Vertrag.
        /// </summary>
        private static string Trim(string value, int max)
            => string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
    }
}
