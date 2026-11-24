using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ITVComponents.Json;
using ITVComponents.Json.Contracts;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.ValueHandles;

namespace ITVComponents.Workflow.Serialization
{
    /// <summary>
    /// Die kanonische JSON-Serialisierung der Workflow-Bibliothek (System.Text.Json). Dasselbe Format,
    /// das der Store persistiert, ist auch das <b>portable Export-/Import-Format</b>: der Knotengraph wird
    /// ueber stabile, selbstgewaehlte Diskriminatoren (<c>"kind"</c>) abgebildet - typnamen-unabhaengig,
    /// sodass eine exportierte Definition beim Import (auch in einem anderen System/einer anderen Version)
    /// wieder aufloest.
    /// </summary>
    /// <remarks>
    /// Der Knackpunkt sind die offenen Stellen der Definition (<c>Literal</c> einer Bindung,
    /// Knoten-Konfiguration, feste Startwerte): System.Text.Json kennt dort nur den deklarierten Typ
    /// <c>object</c>. Jeder solche Wert steht deshalb in der Huelle <c>{"$type": …, "$value": …}</c>
    /// (<see cref="JsonHelper.UseEnvelopePolymorphism"/>) - Zahlen, Datumswerte, Guids, Enums und angemeldete
    /// Datensaetze kommen exakt zurueck. Zeichenketten und Wahrheitswerte stehen blank, wie bisher; eine blanke
    /// Zahl (aelterer Bestand, Hand-JSON) wird nach der Staffel int, long, decimal, double gelesen.
    /// <para>
    /// Gelesen wird unter der Beschraenkung auf <b>angemeldete</b> Typen - eine importierte Definition laedt
    /// keinen beliebigen .NET-Typ. Ein nicht angemeldeter Datensatz kommt untypisiert zurueck (als
    /// <see cref="JsonNode"/>, siehe <see cref="Materialize"/>) und steht im Log.
    /// </para>
    /// </remarks>
    public static class WorkflowJson
    {
        private static readonly JsonSerializerOptions Compact = Build(false);
        private static readonly JsonSerializerOptions Indented = Build(true);

        static WorkflowJson()
        {
            // Die Fehlerliste einer Iteration ist ein Kern-Typ der Bibliothek - sie soll ohne Zutun der
            // Anwendung typtreu durch die Ablage kommen, auch unter der Beschraenkung auf angemeldete Typen.
            ManualTypeRegistry.Register<IterationFailure>("wf-iteration-failure");
        }

        /// <summary>Serialisiert einen Wert. <paramref name="indented"/> = eingerueckt (fuer Export/Dateien).</summary>
        public static string Serialize<T>(T value, bool indented = false)
        {
            return JsonSerializer.Serialize(value, indented ? Indented : Compact);
        }

        /// <summary>Deserialisiert einen Wert; liefert <c>default</c> bei leerem Text.</summary>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }

            using (JsonHelper.RestrictManualTypesToRegistered())
            {
                return JsonSerializer.Deserialize<T>(json, Compact);
            }
        }

        /// <summary>
        /// Meldet einen Typ an, der als Wert einer Workflow-Variablen <b>typtreu</b> abgelegt und wieder
        /// gelesen werden soll - unter einem kurzen, stabilen Namen.
        /// </summary>
        /// <remarks>
        /// Ohne Anmeldung kommt ein zusammengesetzter Wert nach einem Park als Liste/Dictionary zurueck
        /// (nicht als der urspruengliche Datensatz-Typ) - brauchbar, aber untypisiert. Der Name gehoert
        /// zum abgelegten Format: einmal vergeben, darf er sich nicht mehr aendern; die Klasse dahinter
        /// darf dagegen umziehen und umbenannt werden. Genau darum geht es.
        /// <para>
        /// Sammlungen brauchen keine eigene Anmeldung: eine Liste, ein Set, ein Dictionary oder ein Array
        /// angemeldeter Elemente bekommt einen zusammengesetzten Namen (<c>list&lt;sign-item&gt;</c>,
        /// <c>sign-item[]</c>) und kommt als <b>genau diese</b> Sammlung zurueck.
        /// </para>
        /// <example>
        /// <code>
        /// WorkflowJson.RegisterVariableType&lt;SignItem&gt;("sign-item");
        /// </code>
        /// </example>
        /// </remarks>
        public static void RegisterVariableType<T>(string alias) => ManualTypeRegistry.Register<T>(alias);

        /// <summary>Meldet einen Typ fuer typtreue Variablen-Ablage an (siehe <see cref="RegisterVariableType{T}"/>).</summary>
        public static void RegisterVariableType(Type type, string alias) => ManualTypeRegistry.Register(type, alias);

        /// <summary>
        /// Serialisiert einen Variablen-Stack - je Variable ein Eintrag mit eigener Typkennung, damit der
        /// konkrete Typ eines Wertes den Weg in die Ablage und zurueck uebersteht.
        /// </summary>
        /// <remarks>
        /// Format: ein JSON-Objekt mit einer Eigenschaft je Variable; jeder Wert steht in der Huelle
        /// <c>{"$type": …, "$value": …}</c> (<see cref="SerializationTypingMode.EnvelopePolymorphism"/>), nur
        /// Zeichenketten und Wahrheitswerte stehen blank. Dasselbe gilt auf jeder tieferen Ebene, also auch fuer ein
        /// <c>object</c>-Feld innerhalb eines Datensatzes.
        /// </remarks>
        public static string SerializeVariables(IDictionary<string, object> variables)
        {
            GuardAgainstValueHandles(variables);
            var values = variables == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(variables, StringComparer.Ordinal);
            return JsonHelper.ToJson(values, SerializationTypingMode.EnvelopePolymorphism);
        }

        /// <summary>
        /// Der <b>letzte</b> der drei Riegel gegen einen Griff im Variablen-Stack (die anderen beiden
        /// sind der Validator zur Entwurfszeit und das Ziel-Flag in <c>ResolveInputs</c>).
        /// </summary>
        /// <remarks>
        /// Faultend und nicht als Log-Zeile: ein persistierter <see cref="ValueHandle"/> waere nach dem
        /// naechsten Neustart ein toter Verweis, und die naechsten Schritte arbeiteten damit weiter -
        /// dass er tot ist, liesse sich dann nicht mehr rekonstruieren.
        /// <para>
        /// Bewusst nur eine Ebene tief: der Weg, auf dem ein Griff realistisch hierher kaeme, ist eine
        /// Bindung, deren Ergebnis direkt in eine Variable geht. Ein rekursiver Durchlauf durch jeden
        /// Wert bei jedem Commit waere ein hoher Preis fuer einen Fall, den es nicht gibt.
        /// </para>
        /// </remarks>
        /// <param name="variables">der Variablen-Stack, der abgelegt werden soll</param>
        private static void GuardAgainstValueHandles(IDictionary<string, object> variables)
        {
            if (variables == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in variables)
            {
                if (pair.Value is ValueHandle handle)
                {
                    throw new InvalidOperationException(
                        $"Variable '{pair.Key}' holds a value handle ({handle.Request}). A handle is a live " +
                        "object with a plugin scope behind it - storing it would persist a reference that " +
                        "is dead after the next restart. The validator and the runtime should have caught " +
                        "this earlier; this is the last lock.");
                }
            }
        }

        /// <summary>
        /// Liest einen Variablen-Stack zurueck. Liefert nie null (leerer Text = leerer Stack).
        /// </summary>
        /// <remarks>
        /// Laeuft unter der Beschraenkung auf <b>angemeldete</b> Typen: aus dieser Ablage wird kein
        /// beliebiger .NET-Typ geladen, auch wenn im Datenstrom einer benannt ist. Was sich nicht
        /// aufloesen laesst, kommt untypisiert zurueck (als Liste/Dictionary/Primitive, siehe
        /// <see cref="Materialize"/>) und steht im Log.
        /// </remarks>
        public static Dictionary<string, object> DeserializeVariables(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            Dictionary<string, object> read;
            using (JsonHelper.RestrictManualTypesToRegistered())
            {
                read = JsonHelper.FromJsonString<Dictionary<string, object>>(json,
                    SerializationTypingMode.EnvelopePolymorphism);
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            if (read != null)
            {
                foreach (KeyValuePair<string, object> pair in read)
                {
                    // Ein nicht aufloesbarer Typ kommt als roher Knoten - lieber als gewoehnliche Werte
                    // weiterreichen als verlieren. Der Grund steht bereits im Log.
                    values[pair.Key] = pair.Value is JsonNode ? Materialize(pair.Value) : pair.Value;
                }
            }

            return values;
        }

        /// <summary>
        /// Serialisiert die zur Ruecknahme vorgemerkten Schritte. Der mitgefuehrte Variablen-Stand jedes
        /// Eintrags laeuft durch dieselbe typerhaltende Ablage wie die Instanz-Variablen.
        /// </summary>
        public static string SerializeCompensations(IList<CompensationEntry> compensations)
        {
            if (compensations == null || compensations.Count == 0)
            {
                return null;
            }

            var rows = new List<CompensationRow>(compensations.Count);
            foreach (CompensationEntry entry in compensations)
            {
                rows.Add(new CompensationRow
                {
                    Id = entry.Id,
                    Sequence = entry.Sequence,
                    NodeId = entry.NodeId,
                    HandlerNodeId = entry.HandlerNodeId,
                    ScopeNodeId = entry.ScopeNodeId,
                    Compensated = entry.Compensated,
                    // Als eingebetteter Text und nicht als eingebettetes Objekt: der Variablen-Stand
                    // braucht die Typkennungen, die nur der Variablen-Weg vergibt.
                    VariablesJson = SerializeVariables(entry.Variables)
                });
            }

            return Serialize(rows);
        }

        /// <summary>
        /// Liest die zur Ruecknahme vorgemerkten Schritte zurueck. Liefert nie null.
        /// </summary>
        public static List<CompensationEntry> DeserializeCompensations(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new List<CompensationEntry>();
            }

            List<CompensationRow> rows = Deserialize<List<CompensationRow>>(json);
            var result = new List<CompensationEntry>(rows?.Count ?? 0);
            if (rows != null)
            {
                foreach (CompensationRow row in rows)
                {
                    result.Add(new CompensationEntry
                    {
                        Id = row.Id,
                        Sequence = row.Sequence,
                        NodeId = row.NodeId,
                        HandlerNodeId = row.HandlerNodeId,
                        ScopeNodeId = row.ScopeNodeId,
                        Compensated = row.Compensated,
                        Variables = DeserializeVariables(row.VariablesJson)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Die Ablage-Form eines vorgemerkten Schritts: wie <see cref="CompensationEntry"/>, aber mit dem
        /// Variablen-Stand als eingebettetem Text.
        /// </summary>
        private class CompensationRow
        {
            public string Id { get; set; }

            public int Sequence { get; set; }

            public string NodeId { get; set; }

            public string HandlerNodeId { get; set; }

            public string ScopeNodeId { get; set; }

            public bool Compensated { get; set; }

            public string VariablesJson { get; set; }
        }

        /// <summary>
        /// Exportiert eine Definition als (eingerueckten) JSON-Text - das portable Austauschformat.
        /// </summary>
        public static string ExportDefinition(WorkflowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return Serialize(definition, indented: true);
        }

        /// <summary>
        /// Importiert eine Definition aus JSON-Text. Wirft <see cref="System.Text.Json.JsonException"/> bei
        /// ungueltigem JSON. Die fachliche Pruefung (Struktur/Verweise) uebernimmt der
        /// <c>WorkflowDefinitionValidator</c>.
        /// </summary>
        public static WorkflowDefinition ImportDefinition(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("The import text is empty.", nameof(json));
            }

            return Deserialize<WorkflowDefinition>(json);
        }

        /// <summary>
        /// Macht aus einem zurueckgelesenen <see cref="JsonElement"/> wieder gewoehnliche CLR-Werte:
        /// Objekte werden zu <c>Dictionary&lt;string,object&gt;</c>, Arrays zu <c>List&lt;object&gt;</c>,
        /// Primitive zu ihrem Typ. Alles andere wird unveraendert durchgereicht.
        /// </summary>
        /// <remarks>
        /// Fuer Werte, deren Typ beim Lesen nicht aufgeloest werden konnte (nicht angemeldet, umbenannt): sie
        /// kommen als <see cref="JsonNode"/> zurueck, aeltere Quellen liefern auch <see cref="JsonElement"/>.
        /// Ein <see cref="JsonElement"/> ist <b>kein</b> <c>IEnumerable</c> und kein
        /// Dictionary - wer damit weiterarbeiten will, laeuft ohne diese Umwandlung auf.
        /// <para>
        /// Was NICHT zurueckkommt, ist der urspruengliche .NET-Typ: aus einem Datensatz-Objekt wird ein
        /// Dictionary, kein POCO. Typtreu bleiben nur angemeldete Typen;
        /// <see cref="DeserializeVariables"/> reicht einen nicht
        /// aufloesbaren Wert selbst hier durch.
        /// </para></remarks>
        public static object Materialize(object value)
        {
            switch (value)
            {
                case JsonElement element:
                    return FromElement(element);
                case JsonNode node:
                    // Derselbe Fall, andere Bauform: der Variablen-Stack reicht nicht aufgeloeste
                    // Nutzlasten als JsonNode heraus.
                    return FromElement(node.Deserialize<JsonElement>());
                default:
                    return value;
            }
        }

        private static object FromElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var map = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        map[property.Name] = FromElement(property.Value);
                    }

                    return map;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        list.Add(FromElement(item));
                    }

                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    // Dieselbe Staffel wie beim Lesen - sonst waere aus einem int nach dem Umweg ein double.
                    if (element.TryGetInt32(out int i))
                    {
                        return i;
                    }

                    if (element.TryGetInt64(out long l))
                    {
                        return l;
                    }

                    if (element.TryGetDecimal(out decimal m))
                    {
                        return m;
                    }

                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }

        private static JsonSerializerOptions Build(bool indented)
        {
            // Eigene Options (portables Format, kind-Diskriminatoren); nur die offenen Stellen bekommen die Huelle.
            return new JsonSerializerOptions { WriteIndented = indented }.UseEnvelopePolymorphism();
        }
    }
}
