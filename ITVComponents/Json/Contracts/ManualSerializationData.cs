using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ITVComponents.Logging;

namespace ITVComponents.Json.Contracts
{
    public class ManualSerializationData
    {
        private bool processed = false;

        public string PropertyName { get; set; }

        /// <summary>
        /// Die Typkennung der Nutzlast: der ueber <see cref="ManualTypeRegistry"/> registrierte
        /// <b>Kurzname</b>, sonst der <c>AssemblyQualifiedName</c> (siehe <see cref="FromValue"/>).
        /// </summary>
        public string TypeName { get; set; }

        public object Data { get; set; }

        /// <summary>
        /// Die rohe Nutzlast, wenn <see cref="TypeName"/> nicht aufgeloest werden konnte - sonst null.
        /// Damit kann ein Aufrufer den Wert wenigstens untypisiert retten (oder gezielt melden), statt
        /// nur ein stilles null vorzufinden. Wird nicht mitserialisiert.
        /// </summary>
        [JsonIgnore]
        public JsonNode UnresolvedPayload { get; private set; }

        /// <summary>
        /// Baut einen Eintrag aus einem Wert. Die Typkennung ist der registrierte Kurzname des
        /// Laufzeittyps, ersatzweise sein <c>AssemblyQualifiedName</c>.
        /// </summary>
        public static ManualSerializationData FromValue(string propertyName, object value)
        {
            return new ManualSerializationData
            {
                PropertyName = propertyName,
                TypeName = ManualTypeRegistry.NameOf(value?.GetType()),
                Data = value
            };
        }

        public void ReadValues(JsonSerializerOptions serializationOptions)
        {
            if (processed)
            {
                return;
            }

            Type t = ManualTypeRegistry.Resolve(TypeName, out bool refused);
            if (t == null)
            {
                // Kein stiller Verlust: der Wert ist nicht typisierbar, aber er ist da - der Aufrufer
                // bekommt ihn ueber UnresolvedPayload und erfaehrt im Log, warum.
                UnresolvedPayload = Data as JsonNode;
                Data = null;
                if (!string.IsNullOrEmpty(TypeName))
                {
                    // Zwei verschiedene Sachverhalte, deshalb zwei Stufen:
                    //   refused  = vorgesehener Rueckfall. Der Typ ist schlicht nicht angemeldet, der Wert
                    //              kommt untypisiert zurueck. Waere das ein Fehler, liefe bei jedem Lesen
                    //              eines untypisierten Wertes das Fehler-Log voll.
                    //   sonst    = wir HAETTEN den Typ laden duerfen und konnten es nicht - also eine
                    //              umbenannte oder entfernte Klasse. Das gehoert gemeldet.
                    LogEnvironment.LogEvent(
                        refused
                            ? $"Type '{TypeName}' of member '{PropertyName}' is not registered; the value is " +
                              "kept untyped. Register it to read it back typed."
                            : $"Could not resolve type '{TypeName}' for member '{PropertyName}'. The value is " +
                              "kept untyped (UnresolvedPayload); the type was most likely renamed or removed.",
                        refused ? LogSeverity.Report : LogSeverity.Error);
                }

                return;
            }

            if (Data is JsonNode je)
            {
                switch (je.GetValueKind())
                {
                    case JsonValueKind.Undefined:
                        Data = null;
                        break;
                    case JsonValueKind.Object:
                        Data = je.AsObject().Deserialize(t, serializationOptions);
                        break;
                    case JsonValueKind.Array:
                        Data = je.AsArray().Deserialize(ArrayTargetType(t), serializationOptions);
                        break;
                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                        Data = je.Deserialize(t, serializationOptions);
                        break;
                    case JsonValueKind.True:
                        Data = true;
                        break;
                    case JsonValueKind.False:
                        Data = false;
                        break;
                    case JsonValueKind.Null:
                        Data = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            processed = true;
        }

        /// <summary>
        /// Der Zieltyp fuer eine Nutzlast, die als JSON-<b>Array</b> vorliegt. Ist der Typ selbst schon
        /// eine Sammlung (Array, <c>List&lt;T&gt;</c>, ...), wird direkt gegen ihn gelesen; nur ein
        /// Einzelwert-Typ wird zum Array erweitert.
        /// </summary>
        /// <remarks>
        /// Die frueher unbedingte Erweiterung machte generische Listen unlesbar: aus
        /// <c>List&lt;T&gt;</c> wurde <c>List&lt;T&gt;[]</c>, und ein Array von Objekten liess sich
        /// dagegen nicht deserialisieren. Arrays funktionierten, Listen nicht.
        /// <c>string</c> bleibt bewusst aussen vor - eine Zeichenkette ist zwar aufzaehlbar, aber ein
        /// JSON-Array dazu meint <c>string[]</c>.
        /// </remarks>
        private static Type ArrayTargetType(Type t)
        {
            if (t.IsArray || (typeof(IEnumerable).IsAssignableFrom(t) && t != typeof(string)))
            {
                return t;
            }

            return t.MakeArrayType();
        }
    }
}
