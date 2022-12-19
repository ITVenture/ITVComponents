if (!ITVenture.Pages.Identity) {
    ITVenture.Pages.Identity = {};
}

ITVenture.Pages.Identity.Register = {
    RequireInvoiceFields: function () {
        return $("[name='UseInvoiceAddr']").data("kendoSwitch").value();
    }
};

