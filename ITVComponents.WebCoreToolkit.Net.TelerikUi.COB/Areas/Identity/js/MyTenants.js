if (!ITVenture.Pages.Identity) {
    ITVenture.Pages.Identity = {};
}

ITVenture.Pages.Identity.MyTenants = {
    AcceptInvitation: async function(e) {
        var tr = $(e.currentTarget).closest("tr");
        var table = tr.closest(".k-grid").data("kendoGrid");
        var item = table.dataItem(tr);
        var url = "~/Identity/Account/Manage/MyTenants?handler=AcceptInvitation";
        var postObj = {
            CompanyInfoId : item.CompanyInfoId
        }

        //var result = ITVenture.Ajax.ajaxFormPost
        var ok = null;
        try {
            ok = await ITVenture.Ajax.ajaxFormPost(url,
                postObj,
                "text",
                null,
                function() {
                    var token = $('input[name="__RequestVerificationToken"]').val();
                    this.data.__RequestVerificationToken = token;
                });
            console.log(ok);
        } catch (ex) {
            ok = ex.Message;
        }
        await ITVenture.Tools.Popup.OpenAsync("alert", ok);
        location.reload();
    },
    AcceptAvailable: function(m) {
        return m.Status === 1;
    }
}