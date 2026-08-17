using System;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DIIntegration.Impl
{
    /// <summary>
    /// Haengt den <c>EntityWriteTrackerInterceptor</c> an einen Kontext, der ueber das Plugin-System
    /// geladen wird.
    /// </summary>
    /// <typeparam name="TContext">der Kontext, dessen Schreibvorgaenge gemeldet werden sollen</typeparam>
    /// <remarks>
    /// <para>
    /// Der Weg dafuer, dass ein <b>per Plugin gebauter</b> Kontext Schreib-Meldungen abgibt - und damit
    /// alles speist, was daran haengt (Puffer-Invalidierung, <c>IEntityChangeSignal</c>). Ein Kontext, der
    /// nicht ueber die WebPart-Kette registriert wird, kommt an den dortigen Konfigurator nicht heran;
    /// dieser Loader ist das Gegenstueck fuer die Plugin-Konfiguration.
    /// </para>
    /// <para>
    /// Der Verfolger wird <b>zur Schreibzeit</b> aus dem Dienstverzeichnis geholt (nach dem konkreten
    /// Kontext-Typ), nicht hier: die Registrierung <c>IEntityWriteTracker&lt;&gt;</c> kann zum Zeitpunkt der
    /// Plugin-Konstruktion noch fehlen. Ist zur Schreibzeit keiner registriert, sagt der Interceptor das
    /// selbst im Log - die Aenderung wird dann nirgends gemeldet.
    /// </para>
    /// </remarks>
    /// <example>
    /// In der Plugin-Konfiguration wird der Loader ueber den <b>vorhandenen</b> Options-Loader des Kontexts
    /// gelegt (derselbe Aufbau wie beim Mandanten-Interceptor): der eigentliche Loader baut die Verbindung,
    /// dieser haengt den Interceptor dazu, und der Kontext bekommt den <b>aeussersten</b> Loader.
    /// </example>
    public class EntityWriteTrackerInterceptorOptionsLoader<TContext> : ContextOptionsLoader<TContext>
        where TContext : DbContext
    {
        private readonly IServiceProvider services;
        private readonly IEntityWriteTracker<TContext> tracker;

        /// <summary>
        /// Erzeugt den Loader. <b>Der Weg aus der Konfiguration</b> - der Verfolger wird zur Schreibzeit
        /// ueber das Dienstverzeichnis aufgeloest.
        /// </summary>
        /// <param name="parent">der Loader, der die Verbindung dieses Kontexts baut</param>
        /// <param name="services">das Dienstverzeichnis, aus dem der Verfolger geholt wird</param>
        public EntityWriteTrackerInterceptorOptionsLoader(ContextOptionsLoader<TContext> parent,
            IServiceProvider services) : base(parent)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Erzeugt den Loader mit einem <b>festen</b> Verfolger - fuer einen Host ohne Dienstverzeichnis
        /// (und fuer Tests, die den Verfolger selbst mitbringen).
        /// </summary>
        /// <param name="parent">der Loader, der die Verbindung dieses Kontexts baut</param>
        /// <param name="tracker">der Verfolger, dem die geschriebenen Tabellen gemeldet werden</param>
        public EntityWriteTrackerInterceptorOptionsLoader(ContextOptionsLoader<TContext> parent,
            IEntityWriteTracker<TContext> tracker) : base(parent)
        {
            this.tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        /// <inheritdoc/>
        protected override void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> builder)
        {
            if (tracker != null)
            {
                builder.AddEntityWriteTrackerInterceptor(tracker);
                return;
            }

            builder.AddEntityWriteTrackerInterceptor(services);
        }
    }
}
