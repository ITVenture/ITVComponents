if (!ITVenture.Pages.Util) {
    ITVenture.Pages.Util = {};
}

ITVenture.Pages.Util.PlugInAnalysis = {
    ShowAnalyzerDialog: function() {
        ITVenture.Tools.Popup.Open("assemblyAnalyzer", ITVenture.Helpers.ResolveUrl("~/Util/PlugIn/AnalyzeAssembly"));
    }
};