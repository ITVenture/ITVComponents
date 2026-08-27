namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Ein Protokoll, das nichts schreibt. Es gibt den Riegeln etwas, das sie immer aufloesen koennen -
    /// der eingebaute DI-Container beruecksichtigt Standardwerte von Konstruktor-Parametern nicht.
    /// <para>
    /// Das EF-Paket ueberschreibt es mit der Fassung, die in die Systemtabelle schreibt.
    /// </para>
    /// </summary>
    public sealed class NullAssetAccessLog : IAssetAccessLog
    {
        /// <inheritdoc/>
        public void Record(AssetAccessEntry entry)
        {
        }
    }
}
