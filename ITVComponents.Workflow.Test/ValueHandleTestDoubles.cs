using System.Collections.Generic;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.ValueHandles;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Ein Wert-Handler mit einem kleinen Gedaechtnis: er merkt sich, was gelesen und was geschrieben
    /// wurde. Genau das ist in diesen Tests die Frage - nicht der Wert, sondern <b>ob und wie oft</b>
    /// die Engine ihn herausgibt und zurueckschreibt.
    /// </summary>
    public sealed class RecordingValueHandler : IWorkflowValueHandler
    {
        private readonly Dictionary<string, object> records = new Dictionary<string, object>();

        /// <summary>Die Schluessel, die gelesen wurden - in der Reihenfolge der Zugriffe.</summary>
        public List<string> Reads { get; } = new List<string>();

        /// <summary>Die Schluessel, die geschrieben wurden - in der Reihenfolge der Zugriffe.</summary>
        public List<string> Writes { get; } = new List<string>();

        /// <summary>Wirft dieser Handler beim Lesen?</summary>
        public bool FailOnRead { get; set; }

        /// <summary>Wirft dieser Handler beim Schreiben?</summary>
        public bool FailOnWrite { get; set; }

        /// <summary>Legt einen Datensatz an.</summary>
        public RecordingValueHandler With(string key, object value)
        {
            records[key] = value;
            return this;
        }

        /// <summary>Der aktuelle Stand eines Datensatzes.</summary>
        public object Current(string key) => records.TryGetValue(key, out object value) ? value : null;

        /// <inheritdoc/>
        public object Read(ValueHandleRequest request)
        {
            string key = request["key"]?.ToString();
            Reads.Add(key);
            if (FailOnRead)
            {
                throw new System.InvalidOperationException($"reading '{key}' is not possible right now");
            }

            return records.TryGetValue(key, out object value) ? value : null;
        }

        /// <inheritdoc/>
        public void Write(ValueHandleRequest request, object value)
        {
            string key = request["key"]?.ToString();
            if (FailOnWrite)
            {
                throw new System.InvalidOperationException($"writing '{key}' is not possible right now");
            }

            records[key] = value;
            Writes.Add(key);
        }
    }

    /// <summary>Ein Datensatz, wie ihn ein Handler liefert: ein Referenztyp mit Unter-Objekt.</summary>
    public sealed class Order
    {
        /// <summary>Der Kunde.</summary>
        public string Customer { get; set; }

        /// <summary>Der Betrag.</summary>
        public decimal Amount { get; set; }

        /// <summary>Die Lieferadresse.</summary>
        public Address Ship { get; set; }
    }

    /// <summary>Die Lieferadresse eines <see cref="Order"/>.</summary>
    public sealed class Address
    {
        /// <summary>Die Strasse.</summary>
        public string Street { get; set; }

        /// <summary>Der Ort.</summary>
        public string City { get; set; }
    }

    /// <summary>Baut die Bindungen, die in diesen Tests immer wieder gebraucht werden.</summary>
    public static class ValueHandleBindings
    {
        /// <summary>Eine ValueHandle-Bindung auf einen Handler mit einem einzigen Argument 'key'.</summary>
        public static ActivityInputBinding Handle(string parameter, string handlerName, string key,
            ValueDelivery delivery = ValueDelivery.Handle,
            ValueWriteBackMode writeBack = ValueWriteBackMode.Never)
        {
            return new ActivityInputBinding
            {
                Parameter = parameter,
                Kind = ParameterBindingKind.ValueHandle,
                HandlerName = handlerName,
                Delivery = delivery,
                WriteBack = writeBack,
                HandlerArguments = new List<ActivityInputBinding>
                {
                    new ActivityInputBinding
                    {
                        Parameter = "key", Kind = ParameterBindingKind.Literal, Literal = key
                    }
                }
            };
        }
    }
}
