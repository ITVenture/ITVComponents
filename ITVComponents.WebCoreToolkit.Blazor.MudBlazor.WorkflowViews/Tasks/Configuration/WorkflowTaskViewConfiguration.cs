using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Configuration
{
    /// <summary>
    /// Die Zuordnung <b>Schluessel -&gt; Komponente</b> fuer Aufgaben-Masken. Der Host registriert sie
    /// beim Start; die Definition nennt nur den Schluessel.
    /// </summary>
    /// <remarks>
    /// Warum ein Schluessel und nicht der Typ: Definitionen sind Daten aus der Datenbank. Stuende ein
    /// (assembly-qualifizierter) Typname darin, waere Datenpflege gleichbedeutend mit Code-Ausfuehrung.
    /// Der Schluessel dagegen kann nur auf etwas zeigen, das der Host selbst registriert hat.
    /// <para>
    /// Aufgeloest wird in dieser Reihenfolge: <c>ViewKey</c> des Knotens, sonst sein <c>TaskKey</c>, sonst
    /// die generische Maske aus der Feld-Deklaration. Ein unbekannter Schluessel faellt ebenfalls auf die
    /// generische Maske zurueck - aber mit einer Log-Zeile, nicht still.
    /// </para>
    /// </remarks>
    public class WorkflowTaskViewConfiguration
    {
        private readonly Dictionary<string, Type> views =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registriert eine Komponente fuer einen Schluessel (den <c>ViewKey</c> oder den <c>TaskKey</c>
        /// des Knotens).
        /// </summary>
        /// <typeparam name="T">die Blazor-Komponente; sie liest ihren Zustand ueber
        /// <c>[CascadingParameter] WorkflowTaskContext</c></typeparam>
        /// <param name="key">der Schluessel aus der Definition</param>
        public void RegisterTaskView<T>(string key) where T : IComponent
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            }

            if (views.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"A workflow task view for the key '{key}' is already registered.");
            }

            views[key] = typeof(T);
        }

        /// <summary>Liefert die registrierte Komponente, oder null.</summary>
        public Type? GetTaskViewType(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            views.TryGetValue(key!, out Type? type);
            return type;
        }

        /// <summary>Ist ueberhaupt eine Maske registriert? (Fuer die Unterscheidung "unbekannter Schluessel"
        /// von "gar keine Registry".)</summary>
        public bool HasAnyView => views.Count > 0;
    }
}
