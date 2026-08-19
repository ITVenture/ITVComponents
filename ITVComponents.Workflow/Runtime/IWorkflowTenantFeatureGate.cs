using System;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Workflow.Runtime
{
    /// <summary>
    /// Beantwortet die EINE Frage, die der Workflow-Kern selbst nicht beantworten kann: <b>ist dieses
    /// Feature fuer diesen Mandanten aktiv?</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Kern ist bewusst toolkit-neutral - er wertet den Mandanten einer Definition nicht aus und
    /// kennt weder Feature-Aktivierungen noch Abonnements. Trotzdem muss er die Frage stellen koennen,
    /// denn <see cref="Model.WorkflowDefinition.RequiredFeature"/> ist die einzige Bedingung, die auch
    /// <b>ohne Benutzer</b> gilt: ein Zeitplan laeuft im Hintergrund, und ein zentral gepflegter
    /// Zahlungslauf darf nicht munter weiterlaufen, nachdem das Abonnement des Mandanten abgelaufen ist.
    /// </para>
    /// <para>
    /// Wer nichts verdrahtet, bekommt <see cref="AlwaysEnabled"/> und damit das bisherige Verhalten.
    /// </para>
    /// </remarks>
    public interface IWorkflowTenantFeatureGate
    {
        /// <summary>Ist das Feature fuer den Mandanten aktiv?</summary>
        /// <param name="tenantId">der Mandant, oder null (Ein-Mandanten-Betrieb)</param>
        /// <param name="featureName">der Name des Features</param>
        /// <returns>true, wenn der Mandant es benutzen darf</returns>
        bool IsEnabled(string tenantId, string featureName);
    }

    /// <summary>
    /// Die Vorgabe: es gibt keine Feature-Verwaltung, also steht nichts im Weg. Damit verhaelt sich ein
    /// Host, der nichts verdrahtet, genau wie vor der Einfuehrung der Bedingung.
    /// </summary>
    public sealed class AlwaysEnabledFeatureGate : IWorkflowTenantFeatureGate
    {
        /// <summary>Die geteilte Instanz - sie haelt keinen Zustand.</summary>
        public static readonly AlwaysEnabledFeatureGate Instance = new AlwaysEnabledFeatureGate();

        /// <inheritdoc/>
        public bool IsEnabled(string tenantId, string featureName)
        {
            return true;
        }
    }

    /// <summary>
    /// Hilfsmittel rund um das Feature-Gate - damit die Auswertung nicht an mehreren Stellen leicht
    /// unterschiedlich ausfaellt.
    /// </summary>
    public static class WorkflowFeatureGateExtensions
    {
        /// <summary>
        /// Darf dieser Mandant etwas benutzen, das das angegebene Feature verlangt? Kein Feature
        /// verlangt = ja. Kein Gate verdrahtet = ja.
        /// </summary>
        /// <remarks>
        /// Ein Gate, das beim Fragen wirft, sagt <b>nein</b> und protokolliert: bei einer
        /// Zugangsentscheidung ist "ich weiss es nicht" kein Ja. Der Zeitplan setzt dann aus, statt im
        /// Zweifel etwas zu tun, das der Mandant nicht bezahlt hat.
        /// </remarks>
        public static bool AllowsFeature(this IWorkflowTenantFeatureGate gate, string tenantId,
            string featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName))
            {
                return true;
            }

            if (gate == null)
            {
                return true;
            }

            try
            {
                return gate.IsEnabled(tenantId, featureName);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Die Feature-Pruefung '{featureName}' fuer Mandant '{tenantId ?? "-"}' ist "
                    + $"fehlgeschlagen - sie gilt deshalb als NICHT erfuellt: {ex.OutlineException()}",
                    LogSeverity.Error);
                return false;
            }
        }
    }
}
