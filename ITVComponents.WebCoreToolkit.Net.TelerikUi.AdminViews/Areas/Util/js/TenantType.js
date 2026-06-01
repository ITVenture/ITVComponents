if (!ITVenture.Pages.Util) {
    ITVenture.Pages.Util = {};
}

ITVenture.Pages.Util.TenantTypes =
{
    applyToAllTenants: async function (e) {
        var tr = $(e.currentTarget).closest("tr");
        var table = tr.closest(".k-grid").data("kendoGrid");
        var item = table.dataItem(tr);
        var ret = await ITVenture.Tools.Popup.OpenAsync("confirm",
            ITVenture.Text.getText("UT_TT_ApplyToAll", "Do you want to apply the attached template to all tenants of this type?"));
        if (ret.accepted) {
            try {
                var response = await ITVenture.Ajax.ajaxFormPost("~/Util/TenantType/ApplyTemplate", JSON.stringify({ TenantTypeId: item.TenantTypeId }), "json", "application/json");
                ITVenture.Tools.Popup.OpenAsync("alert", response);
            }
            catch (ex) {
                ITVenture.Tools.Popup.OpenAsync("alert", ex);
            }

        }
    },
    showApply: function (e) {
        return typeof(e.TenantTemplateId) === "number" && e.TenantTemplateId > 0;
    }
}