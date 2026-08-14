using System;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.Plugins.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.EFRepo.SqlServer.DIIntegration
{
    public class SqlContextOptionsLoader<TContext>:ContextOptionsLoader<TContext> where TContext : DbContext
    {
        private readonly string connectString;

        private bool useProxies;

        private bool useCommandTimeout = false;

        private int commandTimeout;

        private readonly Action<SqlServerDbContextOptionsBuilder> customizeSqlOptions;

        /*public SqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, bool useProxies) : base(targetSetting, connectStringFormatter, 1)
        {
            this.useProxies = useProxies;
        }

        public SqlContextOptionsLoader(string targetSetting, IStringFormatProvider connectStringFormatter, int recursionDepth, bool useProxies) : base(targetSetting, connectStringFormatter, recursionDepth)
        {
            this.useProxies = useProxies;
        }*/

        public SqlContextOptionsLoader(string connectString, bool useProxies, int commandTimeout)
            : this(connectString, useProxies, commandTimeout, null)
        {
        }

        /// <summary>
        /// Die plugin-taugliche Variante der Migrations-Einstellungen: beides sind schlichte Strings und
        /// damit aus einer Plugin-Konfiguration heraus setzbar, wo ein Callback nicht zur Verfuegung steht.
        /// </summary>
        /// <param name="connectString">der Connectionstring</param>
        /// <param name="useProxies">Lazy-Loading-Proxies einschalten</param>
        /// <param name="commandTimeout">Kommando-Zeitlimit in Sekunden; negativ = das des Providers</param>
        /// <param name="migrationsAssembly">
        /// die Assembly mit den Migrationen dieses Kontexts, oder null/leer fuer die Vorgabe (die Assembly
        /// des Kontexts selbst)
        /// </param>
        /// <param name="migrationsHistoryTable">
        /// die Tabelle mit dem Migrations-Verlauf, oder null/leer fuer <c>__EFMigrationsHistory</c>. Ein
        /// eigener Name ist Pflicht, sobald mehrere Kontexte in derselben Datenbank liegen - sonst halten
        /// sie sich gegenseitig fuer unmigriert.
        /// </param>
        public SqlContextOptionsLoader(string connectString, bool useProxies, int commandTimeout,
            string migrationsAssembly, string migrationsHistoryTable)
            : this(connectString, useProxies, commandTimeout,
                MigrationOptions(migrationsAssembly, migrationsHistoryTable))
        {
        }

        /// <summary>
        /// Wie die dreistellige Ueberladung, aber mit Zugriff auf den provider-eigenen Options-Builder.
        /// </summary>
        /// <param name="connectString">der Connectionstring</param>
        /// <param name="useProxies">Lazy-Loading-Proxies einschalten</param>
        /// <param name="commandTimeout">Kommando-Zeitlimit in Sekunden; negativ = das des Providers</param>
        /// <param name="customizeSqlOptions">
        /// Der Platz fuer alles, was dieser Loader nicht selbst kennt - <c>MigrationsAssembly</c>,
        /// <c>MigrationsHistoryTable</c> (Pflicht, sobald mehrere Kontexte in derselben Datenbank liegen),
        /// <c>EnableRetryOnFailure</c> und Verwandtes. Laeuft <b>nach</b> den Einstellungen dieses Loaders
        /// und darf sie darum auch ueberschreiben.
        /// </param>
        public SqlContextOptionsLoader(string connectString, bool useProxies, int commandTimeout,
            Action<SqlServerDbContextOptionsBuilder> customizeSqlOptions):base()
        {
            this.connectString = connectString;
            this.useProxies = useProxies;
            this.customizeSqlOptions = customizeSqlOptions;
            if (commandTimeout >= 0)
            {
                useCommandTimeout = true;
                this.commandTimeout = commandTimeout;
            }
        }

        /// <summary>
        /// Baut aus den beiden Strings den Anpassungs-Delegaten. Sind beide leer, wird bewusst
        /// <c>null</c> geliefert - dann bleibt es exakt beim Verhalten der Ueberladungen ohne Migrations-Angaben.
        /// </summary>
        private static Action<SqlServerDbContextOptionsBuilder> MigrationOptions(string migrationsAssembly,
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
            builder.UseSqlServer(connectString, o =>
            {
                if (useCommandTimeout)
                {
                    o.CommandTimeout(commandTimeout);
                }

                customizeSqlOptions?.Invoke(o);
            });
            if (useProxies)
            {
                builder.UseLazyLoadingProxies();
            }
        }
    }
}
