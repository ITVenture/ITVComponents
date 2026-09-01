using System;
using System.Collections.Generic;

namespace ITVComponents.Workflow.ValueHandles
{
    /// <summary>
    /// Beschafft und schreibt einen Wert, den der Vorgang nicht selbst traegt.
    /// </summary>
    /// <remarks>
    /// Der Handler kann genau zwei Dinge: lesen und schreiben. Den Griff (<see cref="ValueHandle"/>)
    /// baut die Engine - haette ihn der Handler gebaut, haette jede Implementierung ihr eigenes
    /// Backing-Feld, ihre eigene WriteBack-Semantik und ihr eigenes (oder gar kein) Protokoll.
    /// <para>
    /// <b>Der Handler haelt keinen Zustand ueber einen Aufruf hinaus.</b> Es gibt bewusst keine
    /// Lebenszyklus-Meldungen der Engine (Zweig begonnen, Zweig aufgegeben, Instanz beendet): ein
    /// aufgegebener Zweig ist das haeufigste und am schwersten zuverlaessig zu meldende Ereignis, und
    /// alles, was daran haengen wuerde, faellt hier ersatzlos weg. Es gibt also keinen Ort, an dem ein
    /// Handler etwas verlaesslich wieder aufraeumen koennte - was er zwischen zwei Aufrufen festhaelt,
    /// haelt er unter Umstaenden fuer immer.
    /// </para>
    /// <para>
    /// <b>Kontexte werden je Aufruf geliehen, nie im Konstruktor gehalten.</b> Der Handler wird als
    /// Plugin aus dem <c>IActivityScope</c> der laufenden Arbeitseinheit geladen; wie lange diese Instanz
    /// lebt, entscheidet der Host und nicht der Handler. Ein <c>DbContext</c> im Konstruktor ist deshalb
    /// falsch, auch wenn es lokal funktioniert: er ueberlebt dann entweder zu kurz (der Scope hat ihn
    /// bereits freigegeben) oder viel zu lang (der Handler ist als AutoLoad-Plugin eingerichtet und wird
    /// prozessweit geteilt). Der richtige Weg ist eine <b>Fabrik</b> im Konstruktor und eine Leihgabe je
    /// Aufruf - so macht es der mitgelieferte <c>UserInfoValueHandler</c> mit
    /// <c>IToolkitContextFactory.Lease&lt;DbContext&gt;()</c>.
    /// </para>
    /// <para>
    /// <b>Der Handler geht nicht davon aus, allein zu sein.</b> Ein einzelner Vortrieb laeuft einthreadig,
    /// aber derselbe Handler-Name kann in mehreren Instanzen gleichzeitig aufgeloest sein - und bei einer
    /// geteilten Einrichtung ist es dann dieselbe Instanz. Wer die drei Punkte oben einhaelt, muss sich
    /// darum nicht kuemmern; wer sie verletzt, findet den Fehler erst im Mehrbenutzerbetrieb.
    /// </para>
    /// <para>
    /// <b>Idempotenz ist Handler-Sache.</b> Die Engine kennt keine Retry-Politik: ein modellierter
    /// zweiter Durchlauf loest die Bindung neu auf, der Handler liest also den aktuellen Stand. Eine
    /// nicht-idempotente Aenderung („Betrag += 100") kann die Engine nicht erkennen.
    /// </para>
    /// </remarks>
    public interface IWorkflowValueHandler
    {
        /// <summary>
        /// Liefert den Wert zu der angefragten Bindung.
        /// </summary>
        /// <param name="request">die Anfrage samt aufgeloesten Argumenten und Koordinaten</param>
        /// <returns>der Wert, den die Bindung liefert</returns>
        object Read(ValueHandleRequest request);

        /// <summary>
        /// Schreibt den (moeglicherweise geaenderten) Wert zurueck.
        /// </summary>
        /// <param name="request">dieselbe Anfrage, mit der gelesen wurde</param>
        /// <param name="value">der zurueckzuschreibende Wert</param>
        void Write(ValueHandleRequest request, object value);
    }

    /// <summary>
    /// Die Koordinaten eines Lese-/Schreibvorgangs: wer fragt, fuer welchen Parameter, mit welchen
    /// Argumenten.
    /// </summary>
    /// <remarks>
    /// Gelesen und geschrieben wird mit <b>derselben</b> Anfrage - ein Handler kann sich also an ihr
    /// festhalten, um zu wissen, welchen Datensatz er zurueckschreibt.
    /// </remarks>
    public sealed class ValueHandleRequest
    {
        /// <summary>
        /// Initialisiert eine Anfrage.
        /// </summary>
        /// <param name="handlerName">der Name, unter dem der Handler aufgeloest wurde</param>
        /// <param name="parameter">der Eingabeparameter, den die Bindung fuellt</param>
        /// <param name="arguments">die aufgeloesten Argumente der Bindung</param>
        /// <param name="instanceId">die Instanz, in deren Namen gefragt wird</param>
        /// <param name="definitionId">die Definition der Instanz</param>
        /// <param name="tenantId">der Mandant der Instanz, oder null</param>
        /// <param name="tokenId">der Zweig, in dem gefragt wird, oder null</param>
        /// <param name="nodeId">der Knoten, an dem gefragt wird</param>
        public ValueHandleRequest(string handlerName, string parameter,
            IReadOnlyDictionary<string, object> arguments, string instanceId, string definitionId,
            string tenantId, string tokenId, string nodeId)
        {
            HandlerName = handlerName;
            Parameter = parameter;
            Arguments = arguments ?? new Dictionary<string, object>();
            InstanceId = instanceId;
            DefinitionId = definitionId;
            TenantId = tenantId;
            TokenId = tokenId;
            NodeId = nodeId;
        }

        /// <summary>Der Name, unter dem der Handler aufgeloest wurde.</summary>
        public string HandlerName { get; }

        /// <summary>Der Eingabeparameter, den die Bindung fuellt.</summary>
        public string Parameter { get; }

        /// <summary>Die aufgeloesten Argumente der Bindung. Nie null.</summary>
        public IReadOnlyDictionary<string, object> Arguments { get; }

        /// <summary>Die Instanz, in deren Namen gefragt wird.</summary>
        public string InstanceId { get; }

        /// <summary>Die Definition der Instanz.</summary>
        public string DefinitionId { get; }

        /// <summary>Der Mandant der Instanz, oder null.</summary>
        public string TenantId { get; }

        /// <summary>Der Zweig, in dem gefragt wird, oder null.</summary>
        public string TokenId { get; }

        /// <summary>Der Knoten, an dem gefragt wird.</summary>
        public string NodeId { get; }

        /// <summary>
        /// Liefert das Argument mit dem angegebenen Namen, oder null.
        /// </summary>
        /// <param name="name">der Name des Arguments</param>
        /// <returns>der Wert des Arguments, oder null</returns>
        public object this[string name]
        {
            get
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                return Arguments.TryGetValue(name, out object value) ? value : null;
            }
        }

        /// <summary>
        /// Nennt die Koordinaten dieser Anfrage - fuer Protokoll und Verlauf.
        /// </summary>
        /// <returns>eine kurze, eindeutige Beschreibung dieser Anfrage</returns>
        public override string ToString()
        {
            return $"{HandlerName} -> '{Parameter}' at node '{NodeId}' of instance '{InstanceId}'";
        }
    }
}
