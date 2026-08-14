using System;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace ITVComponents.EFRepo.PostgreSql.DIIntegration
{
    public class PostgreSqlContextOptionsLoader<TContext>:ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private readonly string connectString;

        private bool useProxies;

        private readonly Action<NpgsqlDbContextOptionsBuilder> customizeNpgsqlOptions;

        /*public PostgreSqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, bool useProxies) : base(targetSetting, connectStringFormatter, 1)
        {
            this.useProxies = useProxies;
        }*/

        /*public PostgreSqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter,
            int recursionDepth, bool useProxies) : base(targetSetting, connectStringFormatter, recursionDepth)
        {
            this.useProxies = useProxies;
        }*/

        public PostgreSqlContextOptionsLoader(string connectString, bool useProxies)
            : this(connectString, useProxies, null)
        {
        }

        /// <summary>
        /// Die plugin-taugliche Variante der Migrations-Einstellungen: beides sind schlichte Strings und
        /// damit aus einer Plugin-Konfiguration heraus setzbar, wo ein Callback nicht zur Verfuegung steht.
        /// </summary>
        /// <param name="connectString">der Connectionstring</param>
        /// <param name="useProxies">Lazy-Loading-Proxies einschalten</param>
        /// <param name="migrationsAssembly">
        /// die Assembly mit den Migrationen dieses Kontexts, oder null/leer fuer die Vorgabe (die Assembly
        /// des Kontexts selbst)
        /// </param>
        /// <param name="migrationsHistoryTable">
        /// die Tabelle mit dem Migrations-Verlauf, oder null/leer fuer <c>__EFMigrationsHistory</c>. Ein
        /// eigener Name ist Pflicht, sobald mehrere Kontexte in derselben Datenbank liegen - sonst halten
        /// sie sich gegenseitig fuer unmigriert.
        /// </param>
        public PostgreSqlContextOptionsLoader(string connectString, bool useProxies,
            string migrationsAssembly, string migrationsHistoryTable)
            : this(connectString, useProxies, MigrationOptions(migrationsAssembly, migrationsHistoryTable))
        {
        }

        /// <summary>
        /// Wie die zweistellige Ueberladung, aber mit Zugriff auf den provider-eigenen Options-Builder.
        /// </summary>
        /// <param name="connectString">der Connectionstring</param>
        /// <param name="useProxies">Lazy-Loading-Proxies einschalten</param>
        /// <param name="customizeNpgsqlOptions">
        /// Der Platz fuer alles, was dieser Loader nicht selbst kennt - <c>MigrationsAssembly</c>,
        /// <c>MigrationsHistoryTable</c> (Pflicht, sobald mehrere Kontexte in derselben Datenbank liegen),
        /// <c>CommandTimeout</c>, <c>EnableRetryOnFailure</c> und Verwandtes.
        /// </param>
        public PostgreSqlContextOptionsLoader(string connectString, bool useProxies,
            Action<NpgsqlDbContextOptionsBuilder> customizeNpgsqlOptions) : base()
        {
            this.connectString = connectString;
            this.useProxies = useProxies;
            this.customizeNpgsqlOptions = customizeNpgsqlOptions;
        }

        /// <summary>
        /// Baut aus den beiden Strings den Anpassungs-Delegaten. Sind beide leer, wird bewusst
        /// <c>null</c> geliefert - dann bleibt es exakt beim Verhalten der Ueberladungen ohne Migrations-Angaben.
        /// </summary>
        private static Action<NpgsqlDbContextOptionsBuilder> MigrationOptions(string migrationsAssembly,
            string migrationsHistoryTable)
        {
            if (string.IsNullOrEmpty(migrationsAssembly) && string.IsNullOrEmpty(migrationsHistoryTable))
            {
                return null;
            }

            return o =>
            {
                if (!string.IsNullOrEmpty(migrationsAssembly))
                {
                    o.MigrationsAssembly(migrationsAssembly);
                }

                if (!string.IsNullOrEmpty(migrationsHistoryTable))
                {
                    o.MigrationsHistoryTable(migrationsHistoryTable);
                }
            };
        }

        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            builder.UseNpgsql(connectString, npg => customizeNpgsqlOptions?.Invoke(npg));
            if (useProxies)
            {
                builder.UseLazyLoadingProxies();
            }
        }
    }
}
