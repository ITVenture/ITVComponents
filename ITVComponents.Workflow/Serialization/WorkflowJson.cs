using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ITVComponents.Json;
using ITVComponents.Json.Contracts;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;

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
    /// Der Knackpunkt ist <c>Dictionary&lt;string,object&gt;</c> (Instanz-Variablen und
    /// Knoten-Konfiguration): System.Text.Json liest <c>object</c>-Werte sonst als <see cref="JsonElement"/>
    /// zurueck, wodurch aus einem <c>int</c> ein JSON-Knoten wuerde. Der <see cref="ObjectValueConverter"/>
    /// gibt Primitive typerhaltend zurueck. Bekannte Grenze: verschachtelte Objekte/Arrays als
    /// Variablenwert kommen als <see cref="JsonElement"/> zurueck (Workflow-Variablen sind ueblicherweise
    /// Primitive).
    /// </remarks>
    public static class WorkflowJson
    {
        private static readonly JsonSerializerOptions Compact = Build(false);
        private static readonly JsonSerializerOptions Indented = Build(true);

        static WorkflowJson()
        {
            // Die Ablage-Form der Variablen beim Contract-Resolver anmelden - ohne das kennt der
            // Deserialisierer den Diskriminator nicht und der Variablen-Stack liesse sich nicht lesen.
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(WorkflowVariableBag),
                WorkflowVariableBag.Discriminator);

            // Die Fehlerliste einer Iteration ist ein Kern-Typ der Bibliothek - sie soll ohne Zutun der
            // Anwendung typtreu durch die Ablage kommen. Sie traegt ihre Felder einzeln (wegen des
            // object-Feldes Item) und braucht deshalb BEIDES: den Contract und den Kurznamen.
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(IterationFailure),
                "wf-iteration-failure");
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
            return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Compact);
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
        /// Sammlungen brauchen keine eigene Anmeldung: eine Liste oder ein Array angemeldeter Elemente
        /// wird ueber den Elementnamen abgelegt und kommt als <b>Array</b> zurueck.
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
        public static string SerializeVariables(IDictionary<string, object> variables)
        {
            var bag = new WorkflowVariableBag
            {
                Values = variables == null
                    ? new Dictionary<string, object>(StringComparer.Ordinal)
                    : new Dictionary<string, object>(variables, StringComparer.Ordinal)
            };
            return JsonHelper.ToJson<IManualSerializer>(bag, SerializationTypingMode.AssistedPolymorphism);
        }

        /// <summary>
        /// Liest einen Variablen-Stack zurueck. Liefert nie null (leerer Text = leerer Stack).
        /// </summary>
        /// <remarks>
        /// Laeuft unter der Beschraenkung auf <b>angemeldete</b> Typen: aus dieser Ablage wird kein
        /// beliebiger .NET-Typ geladen, auch wenn im Datenstrom einer benannt ist. Was sich nicht
        /// aufloesen laesst, kommt untypisiert zurueck (und steht im Log).
        /// </remarks>
        public static Dictionary<string, object> DeserializeVariables(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            using (JsonHelper.RestrictManualTypesToRegistered())
            {
                var bag = JsonHelper.FromJsonString<WorkflowVariableBag>(json,
                    SerializationTypingMode.AssistedPolymorphism);
                return bag?.Values ?? new Dictionary<string, object>(StringComparer.Ordinal);
            }
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
        /// Der Gegenzug zur bekannten Grenze des <see cref="ObjectValueConverter"/>: zusammengesetzte
        /// Variablenwerte kommen nach einem Park (Commit + Neuladen) als <see cref="JsonElement"/>
        /// zurueck. Ein <see cref="JsonElement"/> ist <b>kein</b> <c>IEnumerable</c> und kein
        /// Dictionary - wer damit weiterarbeiten will, laeuft ohne diese Umwandlung auf.
        /// <para>
        /// Was NICHT zurueckkommt, ist der urspruengliche .NET-Typ: aus einem Datensatz-Objekt wird ein
        /// Dictionary, kein POCO. Dafuer braeuchte die Ablage einen Typ-Diskriminator - eine Entscheidung
        /// mit Format- und Sicherheitsfolgen, die hier bewusst nicht getroffen wird.
        /// </para></remarks>
        public static object Materialize(object value)
        {
            switch (value)
            {
                case JsonElement element:
                    return FromElement(element);
                case JsonNode node:
                    // Derselbe Fall, andere Bauform: der manuelle Serialisierungs-Pfad reicht nicht
                    // aufgeloeste Nutzlasten als JsonNode heraus.
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
            var options = new JsonSerializerOptions { WriteIndented = indented };
            options.Converters.Add(new ObjectValueConverter());
            return options;
        }

        /// <summary>Haelt Primitive typerhaltend, wenn sie als <c>object</c> serialisiert/gelesen werden.</summary>
        private sealed class ObjectValueConverter : JsonConverter<object>
        {
            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.True:
                        return true;
                    case JsonTokenType.False:
                        return false;
                    case JsonTokenType.String:
                        return reader.GetString();
                    case JsonTokenType.Number:
                        if (reader.TryGetInt32(out int i))
                        {
                            return i;
                        }

                        if (reader.TryGetInt64(out long l))
                        {
                            return l;
                        }

                        if (reader.TryGetDecimal(out decimal m))
                        {
                            return m;
                        }

                        return reader.GetDouble();
                    case JsonTokenType.Null:
                        return null;
                    default:
                        // Objekt/Array: als JsonElement erhalten (seltener Fall bei Variablen).
                        using (JsonDocument document = JsonDocument.ParseValue(ref reader))
                        {
                            return document.RootElement.Clone();
                        }
                }
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                // Ueber den konkreten Typ serialisieren - fuer int/string/bool greifen deren eigene
                // Konverter, nicht erneut dieser hier (keine Rekursion).
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }
    }
}
