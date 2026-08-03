using System;
using System.Collections.Generic;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;

namespace ITVComponents.Workflow.Serialization
{
    /// <summary>
    /// Die Ablage-Form eines Variablen-Stacks: <b>je Variable ein eigener Eintrag</b> mit eigener
    /// Typkennung. Nur so ueberlebt der konkrete Typ eines Wertes den Weg in die Datenbank und zurueck -
    /// ein <c>Dictionary&lt;string,object&gt;</c> am Stueck serialisiert traegt die Typen seiner Werte
    /// nicht.
    /// </summary>
    /// <remarks>
    /// Derselbe Zuschnitt, den <c>DynamicResult</c> im Datenzugriff benutzt. Die Typkennung ist der ueber
    /// <see cref="ManualTypeRegistry"/> angemeldete <b>Kurzname</b> (siehe
    /// <see cref="WorkflowJson.RegisterVariableType{T}"/>); Grundtypen sind ab Werk angemeldet. Beim Lesen
    /// gilt die Beschraenkung auf registrierte Typen - aus einer Datenbank-Spalte wird also kein
    /// beliebiger .NET-Typ geladen.
    /// <para>
    /// Ein Wert, dessen Typ nicht (mehr) aufloesbar ist, geht <b>nicht verloren</b>: er kommt untypisiert
    /// zurueck (Liste/Dictionary/Primitive, wie bisher) und die Ursache steht im Log.
    /// </para></remarks>
    /// <remarks>
    /// Bewusst <b>nicht</b> <c>sealed</c>: der Contract-Resolver haengt die Polymorphie-Optionen des
    /// <see cref="IManualSerializer"/>-Vertrags auch an den abgeleiteten Typ, und System.Text.Json lehnt
    /// das an einem versiegelten Typ ab („does not support polymorphism"). <c>SimpleContract</c> ist aus
    /// demselben Grund offen.
    /// </remarks>
    internal class WorkflowVariableBag : IManualSerializer
    {
        /// <summary>Der Diskriminator dieser Ablage-Form im JSON.</summary>
        public const string Discriminator = "wf-vars";

        /// <summary>Die Variablen. Wird nicht direkt serialisiert - der Contract laesst nur <see cref="Data"/> durch.</summary>
        public Dictionary<string, object> Values { get; set; } =
            new Dictionary<string, object>(StringComparer.Ordinal);

        /// <inheritdoc/>
        public IList<ManualSerializationData> Data { get; set; }

        /// <inheritdoc/>
        public void GetObjectData()
        {
            foreach (KeyValuePair<string, object> pair in Values)
            {
                Data.Add(ManualSerializationData.FromValue(pair.Key, pair.Value));
            }
        }

        /// <inheritdoc/>
        public void ApplyObjectData()
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (ManualSerializationData entry in Data)
            {
                if (entry?.PropertyName == null)
                {
                    continue;
                }

                // Konnte der Typ nicht aufgeloest werden (umbenannt, nicht angemeldet), steht der Wert
                // noch als roher Knoten bereit - dann lieber untypisiert weiterreichen als verlieren.
                // ManualSerializationData hat den Grund bereits protokolliert.
                values[entry.PropertyName] = entry.Data ?? WorkflowJson.Materialize(entry.UnresolvedPayload);
            }

            Values = values;
        }
    }
}
