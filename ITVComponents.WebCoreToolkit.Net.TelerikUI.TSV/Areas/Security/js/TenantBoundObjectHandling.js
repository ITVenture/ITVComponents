if (!ITVenture.Pages.Security) {
    ITVenture.Pages.Security = {};
}

ITVenture.Pages.Security.TenantBoundObjectsHelper =
{
    detailsDialog: null,
    isEditable: function(item) {
        return item.Editable;
    },
    showTenantSettings: function (e) {
        var tr = $(e.currentTarget).closest("tr");
        var table = tr.closest(".k-grid").data("kendoGrid");
        var item = table.dataItem(tr);
        var url = "~/Security/Tenant/DetailsView?tenantId=".concat(item.TenantId);
        ITVenture.Pages.Security.TenantBoundObjectsHelper.detailsDialog.Open(
            {
                contentUrl:url,
                dialogOpening:
                    function(obj, dg) {
                        dg.maximize();
                    }
            });
    }
};

$(function() {
    ITVenture.Pages.Security.TenantBoundObjectsHelper.detailsDialog = ITVenture.Tools.Popup.createGenericPopup("tenantDetailsViewRoot",
        {
            visible: false,
            title: "Mandant-Infos bearbeiten",
            minWidth: 300,
            //maxWidth:1200,
            minHeight: 100,
            //maxHeight:900,
            modal: true,
            position: {
                top: "25%",
                left: "10%"
            }
        });
})