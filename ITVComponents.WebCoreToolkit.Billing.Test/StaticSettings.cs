using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Einstellungen ohne Ablage — derselbe Wert, egal wonach gefragt wird.
    /// </summary>
    /// <remarks>
    /// Die Anbieter-Wege lesen ihre Einstellungen über <see cref="IGlobalSettings{TSettings}"/>, und das
    /// ist im Betrieb ein Weg in die Datenbank. Geprüft werden soll aber der Weg, nicht die Ablage — also
    /// steht hier genau der eine Wert, den der Test gesetzt hat.
    /// </remarks>
    public sealed class StaticSettings<TSettings> : IGlobalSettings<TSettings>
        where TSettings : class, new()
    {
        /// <summary>Initializes a new instance of the <see cref="StaticSettings{TSettings}"/> class.</summary>
        public StaticSettings(TSettings value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        public TSettings Value { get; }

        /// <inheritdoc/>
        public TSettings ValueOrDefault => Value;

        /// <inheritdoc/>
        public TSettings GetValue(string explicitSettingName) => Value;

        /// <inheritdoc/>
        public TSettings GetValueOrDefault(string explicitSettingName) => Value;
    }
}
