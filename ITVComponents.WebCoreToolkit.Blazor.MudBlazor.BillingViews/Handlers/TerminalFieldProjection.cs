using System.Collections.Generic;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers
{
    /// <summary>
    /// Bildet die Feldbeschreibung einer Terminal-Anbindung auf die Maske des Toolkits ab.
    /// </summary>
    /// <remarks>
    /// Reicht an <see cref="SettingFieldProjection"/> weiter: Terminals und Agent-Geräte beschreiben sich mit
    /// demselben Feldmodell und werden deshalb auch gleich gezeichnet.
    /// </remarks>
    public static class TerminalFieldProjection
    {
        /// <inheritdoc cref="SettingFieldProjection.ToDeclaredFields"/>
        public static IReadOnlyList<DeclaredField> ToDeclaredFields(IReadOnlyList<SettingDescriptor> fields,
            IReadOnlyDictionary<string, IReadOnlyList<SettingChoice>>? choices = null)
            => SettingFieldProjection.ToDeclaredFields(fields, choices);

        /// <inheritdoc cref="SettingFieldProjection.ToDeclaredField"/>
        public static DeclaredField ToDeclaredField(SettingDescriptor field,
            IReadOnlyDictionary<string, IReadOnlyList<SettingChoice>>? choices = null)
            => SettingFieldProjection.ToDeclaredField(field, choices);

        /// <inheritdoc cref="SettingFieldProjection.Defaults"/>
        public static Dictionary<string, object?> Defaults(IReadOnlyList<SettingDescriptor> fields,
            IReadOnlyDictionary<string, string?>? existing = null)
            => SettingFieldProjection.Defaults(fields, existing);
    }
}
