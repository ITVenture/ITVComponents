using System;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Deklariert einen Parameter einer Aktivitaet auf Klassenebene - so kann der
    /// <see cref="ITVComponents.Workflow.Activities.IWorkflowActivityCatalog"/> die Parameter per
    /// Reflection lesen, OHNE die Aktivitaet zu instanzieren. Mehrfach anwendbar (ein Attribut je
    /// Parameter). Die konfigurierten Werte landen im <c>Configuration</c>-Dictionary des Knotens.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class ActivityParameterAttribute : Attribute
    {
        /// <summary>Deklariert einen Parameter mit dem angegebenen Namen.</summary>
        public ActivityParameterAttribute(string name)
        {
            Name = name;
        }

        /// <summary>Der Parametername (Schluessel im Konfigurations-Dictionary).</summary>
        public string Name { get; }

        /// <summary>Die Editor-Art (Standard: einzeiliger Text).</summary>
        public ActivityParameterKind Kind { get; set; } = ActivityParameterKind.String;

        /// <summary>Die Datenfluss-Richtung (Standard: Eingabe).</summary>
        public ActivityParameterDirection Direction { get; set; } = ActivityParameterDirection.Input;

        /// <summary>Pflichtfeld?</summary>
        public bool Required { get; set; }

        /// <summary>Vorgabewert (konstant), oder null.</summary>
        public object Default { get; set; }

        /// <summary>Beschreibung/Hilfetext.</summary>
        public string Description { get; set; }

        /// <summary>Optionale Gruppierung im Formular.</summary>
        public string Group { get; set; }

        /// <summary>Sortierung im Formular.</summary>
        public int Order { get; set; }

        /// <summary>Statische Auswahlwerte (bei <see cref="ActivityParameterKind.Picklist"/>).</summary>
        public string[] Values { get; set; }

        /// <summary>Optionale Anzeigelabels, parallel zu <see cref="Values"/>.</summary>
        public string[] Labels { get; set; }

        /// <summary>
        /// Der Typ eines <see cref="IValuesProvider"/>, der die Auswahlwerte dynamisch liefert. Pflicht
        /// bei <see cref="ActivityParameterKind.CallbackList"/>. Der Katalog konstruiert diesen Typ in
        /// einem eigenen Scope (volle Konstruktor-Injection) und ruft <see cref="IValuesProvider.GetValues"/>.
        /// </summary>
        public Type ValuesProvider { get; set; }
    }

    /// <summary>
    /// Optionale Anzeige-Metadaten eines Aktivitaets-Typs (Name/Beschreibung im Katalog). Fehlt das
    /// Attribut, dient der Scoped-Plugin-Name als Anzeigename.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class WorkflowActivityAttribute : Attribute
    {
        /// <summary>Anzeigename der Aktivitaet.</summary>
        public string DisplayName { get; set; }

        /// <summary>Beschreibung der Aktivitaet.</summary>
        public string Description { get; set; }
    }
}
