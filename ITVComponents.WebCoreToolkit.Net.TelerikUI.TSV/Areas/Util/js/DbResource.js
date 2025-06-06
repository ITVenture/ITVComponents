if (!ITVenture.Pages.Util) {
    ITVenture.Pages.Util = {};
}

ITVenture.Pages.Util.DbResources = {
    ResetLocalization: function () {
        ITVenture.Ajax.ajaxFormPost("~/Util/DbResource/ResetLocalization", JSON.stringify({}), "json", "application/json");
    },
    CopyFromOrigin: async function (e) {
        e.preventDefault();
        var target = $(e.currentTarget);
        while (typeof target.data().kendoGrid === "undefined" || target.data().kendoGrid === null) {
            target = target.parent();
        }

        var id = target.attr("localizationCultureId");
        await ITVenture.Ajax.ajaxFormPost("~/Util/DbResource/CopyFromSource?localizationCultureId=".concat(id), JSON.stringify({}), "json", "application/json");
        ITVenture.Tools.KendoExtensions.RefreshSources();
        target.data("kendoGrid").dataSource.read();
    }
};