using System.Collections.Generic;
using ITVComponents.Plugins;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Liefert die zulaessigen Werte eines <see cref="ActivityParameterKind.CallbackList"/>-Parameters.
    /// </summary>
    /// <remarks>
    /// Der Katalog konstruiert den Provider bei Bedarf in einem eigenen Plugin-Scope und gibt ihn
    /// danach wieder frei. Bewusst ein Objekt (kein statischer Callback): ueber die
    /// Konstruktor-Injection zieht der Provider seine Abhaengigkeiten selbst - der Kontext, aus dem sich
    /// die dynamischen Werte ableiten, kommt also aus der konkreten Konstruktion, statt als lange
    /// Argumentliste uebergeben werden zu muessen. Der Vertrag ist <see cref="IPlugin"/>, damit die
    /// Factory ihn ueber den Scope laden und beim Schliessen disposen kann.
    /// </remarks>
    public interface IValuesProvider : IPlugin
    {
        /// <summary>Liefert die Auswahlwerte fuer den angegebenen Parameter.</summary>
        IEnumerable<ActivityParameterValue> GetValues(string parameterName);
    }
}
