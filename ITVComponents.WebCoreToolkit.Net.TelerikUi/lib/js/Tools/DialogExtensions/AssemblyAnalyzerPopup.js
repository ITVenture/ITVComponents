ITVenture.Tools.AssemblyAnalyzer = {
    CreateAnalyzerPopup: function() {
        var obj = {
            refObj: null,
            acceptCallbacks: [],
            showCallbacks: [],
            cancelCallbacks: [],
            closedCallbacks: [],
            window: $('<div id="assemblyAnalyzerRoot" width="100%" height="100%">' +
                '<i>Assembly Beschreiben: </i><input id="AssemblyBox" /><a class="k-button k-button-md" href="#" title="Abrufen" onclick="ITVenture.Tools.AssemblyAnalyzer.renderAssemblyInfo(this);"><span class="fa-duotone fa-eye"></span><span class="k-button-text">Abrufen</span></a>' +
                '<div id="PluginDetailInfo" />' +
                '</div>')
        };
        obj.PluginDetailInfo = $(obj.window.find("#PluginDetailInfo"));
        obj.PluginTextBox = $(obj.window.find("#AssemblyBox"));
        obj.window.kendoWindow({
            visible: false,
            title: "Assembly Analyzer",
            minWidth: 300,
            minHeight: 100,
            modal: true,
            scrollable:false,
            position: {
                top: "30%",
                left: "30%"
            }
        });

        obj = ITVenture.Tools.Popup.EnrichWindow(obj);

        obj.onClosed(function (win, dialog, refObj) {
            var dlg = win.data("itvDialog");
            dlg.PluginTextBox.val("");
            dlg.PluginDetailInfo.html("");
        });

        return obj;
    },
    renderAssemblyInfo: function(button) {
        var diag = ITVenture.Tools.Popup.FindDialog(button).data("itvDialog");
        var assembly = diag.PluginTextBox.val();
        var query = diag.refObj.concat("?AssemblyName=").concat(assembly);
        ITVenture.Tools.AssemblyAnalyzerDataSource.InitializeFor(diag.PluginDetailInfo);
        $.ajax({
            url: query,
            success: function(data) {
                //console.log(data);
                diag.PluginDetailInfo.html(data);
            }
        });
    }
};

$(function() {
    ITVenture.Tools.Popup.dialogs.assemblyAnalyzer = ITVenture.Tools.AssemblyAnalyzer.CreateAnalyzerPopup();
})