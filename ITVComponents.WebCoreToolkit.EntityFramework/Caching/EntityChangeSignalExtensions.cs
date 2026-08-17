using ITVComponents.WebCoreToolkit.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Caching
{
    /// <summary>
    /// Registers the EF-backed change-signal - without knowing what the topics are about.
    /// </summary>
    public static class EntityChangeSignalExtensions
    {
        /// <summary>
        /// Registers <see cref="EntityChangeSignal{TContext}"/> as the implementation of
        /// <see cref="IEntityChangeSignal{TContext}"/> and - for the given context - also as the
        /// context-agnostic <see cref="IEntityChangeSignal"/>.
        /// </summary>
        /// <typeparam name="TContext">the context that answers the non-generic signal</typeparam>
        /// <param name="services">the services-collection to register in</param>
        /// <returns>the same services-collection</returns>
        /// <remarks>
        /// <para>
        /// Die Registrierung ist <b>offen generisch</b>: damit ist das Signal fuer JEDEN Kontext
        /// aufloesbar, sobald es fuer ihn einen <c>IEntityWriteTracker&lt;TContext&gt;</c> gibt - fuer den
        /// Workflow-Kontext also genauso wie fuer den Security-Kontext. Themen bringt jeder Verbraucher
        /// selbst mit (<c>EntitySignalOptions&lt;TContext&gt;</c>); diese Methode legt keine fest.
        /// </para>
        /// <para>
        /// Der nicht-generische Alias ist fuer die Verbraucher, die den Kontext nicht benennen koennen
        /// (Navigation, Berechtigungs-Bereich, <c>EntityChangeRefresher</c>). Er kann nur EINEM Kontext
        /// gehoeren - deshalb steht er hier als Typ-Argument und nicht implizit fest.
        /// </para>
        /// <para>
        /// Ohne aktiven Schreib-Verfolger bleibt das Signal ein stiller No-op (es meldet
        /// <c>DateTime.MinValue</c> und loest nie aus) - das ist beabsichtigt: die puffernden Verbraucher
        /// fallen damit auf ihr TTL-Verhalten zurueck.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddEntityChangeSignal<TContext>(this IServiceCollection services)
            where TContext : DbContext
        {
            services.AddEntityChangeSignal();
            services.TryAddSingleton<IEntityChangeSignal>(sp => sp.GetRequiredService<IEntityChangeSignal<TContext>>());
            return services;
        }

        /// <summary>
        /// Registers the signal for every context - <b>without</b> claiming the context-agnostic
        /// <see cref="IEntityChangeSignal"/>.
        /// </summary>
        /// <param name="services">the services-collection to register in</param>
        /// <returns>the same services-collection</returns>
        /// <remarks>
        /// Der Weg fuer jeden Kontext, der <b>nicht</b> der Kontext der kontext-blinden Verbraucher ist -
        /// zum Beispiel den Workflow-Kontext. Den nicht-generischen Alias darf er nicht beanspruchen: er
        /// wird per <c>TryAdd</c> registriert, also gewinnt schlicht der erste Anmelder. Waere das der
        /// Workflow-Kontext, haengen Navigation und Berechtigungs-Puffer danach still am falschen Signal
        /// und werden nie mehr ungueltig - ein Fehler, den man an der Oberflaeche als "Rechte ziehen nicht"
        /// erlebt und an ganz anderer Stelle sucht.
        /// </remarks>
        public static IServiceCollection AddEntityChangeSignal(this IServiceCollection services)
        {
            services.TryAddSingleton(typeof(IEntityChangeSignal<>), typeof(EntityChangeSignal<>));
            return services;
        }
    }
}
