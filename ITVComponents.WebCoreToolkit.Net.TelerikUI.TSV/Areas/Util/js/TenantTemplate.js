if (!ITVenture.Pages.Util) {
    ITVenture.Pages.Util = {};
}

ITVenture.Pages.Util.TenantTemplates =
{
    applyTemplate: async function(e) {
        var btn = e.sender.element;
        var tenantId = btn.attr("tenantId");
        var dropdownName = btn.attr("dropdownName");
        var dropdown = $("#".concat(dropdownName)).data("kendoDropDownList");
        var id = dropdown.value();
        var model = {
            TenantId: tenantId,
            TemplateId: id
        }

        var result = await ITVenture.Ajax.ajaxFormPost("~/Util/TenantTemplate/ApplyTemplate", JSON.stringify(model), "json", "application/json");
        await ITVenture.Tools.Popup.OpenAsync("alert", result.Message);
    },
    createTemplate: async function(e) {
        var btn = e.sender.element;
        var tenantId = btn.attr("tenantId");
        var rs = await ITVenture.Tools.Popup.OpenAsync("input",
            ITVenture.Text.getText("ITVTenantTmpl_InputName", "Enter a name for the new Template"));
        if (rs.accepted) {
            var model = {
                TenantId: tenantId,
                Name: rs.customArg
            };
            var result = await ITVenture.Ajax.ajaxFormPost("~/Util/TenantTemplate/CreateTemplate",
                JSON.stringify(model),
                "json",
                "application/json");
            await ITVenture.Tools.Popup.OpenAsync("alert", result.Message);
        }
    }
}