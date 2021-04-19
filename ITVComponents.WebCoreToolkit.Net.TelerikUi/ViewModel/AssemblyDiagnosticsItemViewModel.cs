namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class AssemblyDiagnosticsItemViewModel
    {
        public string FullName { get; set; }

        public string Location { get; set; }

        public bool IsDynamic{get; set;}
        public string LoadContext { get; set; }
        public string RuntimeVersion { get; set; }
        public bool IsCollectible { get; set; }
    }
}
