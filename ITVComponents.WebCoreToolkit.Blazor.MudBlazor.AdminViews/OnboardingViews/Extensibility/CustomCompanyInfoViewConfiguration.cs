using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Extensibility;

/// <summary>
/// Die Zuordnung <b>Schluessel -&gt; Komponente</b> fuer eigene Zusatzangaben-Masken. Der Host
/// registriert sie beim Start; das Modul nennt nur den Schluessel (<c>ViewKey</c>).
/// </summary>
/// <remarks>
/// Warum ein Schluessel und nicht der Typ: der Vertrag des Moduls liegt in einem Paket, das Blazor nicht
/// kennt - ein <c>Type</c> darin waere entweder eine falsche Abhaengigkeit oder ein Typname als
/// Zeichenkette, und letzteres machte Konfigurationspflege gleichbedeutend mit Code-Ausfuehrung. Der
/// Schluessel dagegen kann nur auf etwas zeigen, das der Host selbst registriert hat.
/// </remarks>
public class CustomCompanyInfoViewConfiguration
{
    private readonly Dictionary<string, Type> views = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registriert eine Komponente fuer einen Schluessel.
    /// </summary>
    /// <typeparam name="T">
    /// die Blazor-Komponente. Sie liest ihren Zustand ueber
    /// <c>[CascadingParameter] CustomCompanyInfoViewContext</c> und erfuellt
    /// <see cref="ICustomCompanyInfoView"/> - letzteres wird schon beim UEBERSETZEN verlangt und nicht
    /// erst beim Oeffnen des Reiters. Ohne den Vertrag haette das Formular niemanden, den es beim
    /// Abschicken fragen koennte, und die Eingaben des Reiters gingen still verloren.
    /// </typeparam>
    /// <param name="key">der Schluessel, den das Modul als <c>ViewKey</c> nennt</param>
    public void RegisterView<T>(string key) where T : IComponent, ICustomCompanyInfoView
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        if (views.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"A custom company-info view for the key '{key}' is already registered.");
        }

        views[key] = typeof(T);
    }

    /// <summary>Liefert die registrierte Komponente, oder null.</summary>
    public Type? GetViewType(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        views.TryGetValue(key!, out Type? type);
        return type;
    }
}
