if (!ITVenture.Pages.Util) {
    ITVenture.Pages.Util = {};
}

ITVenture.Pages.Util.AssemblyDiagnostics = {
    ResetNativeScripts: function () {
        ITVenture.Ajax.ajaxFormPost("~/Util/AssemblyDiagnostics/ResetNativeScripts", JSON.stringify({}), "json", "application/json"); 
    }
}