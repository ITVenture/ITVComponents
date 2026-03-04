ITVenture.Pages.ExternalOAuthServices = {
    ConnectService: function (e) {
        var tr = $(e.currentTarget).closest("tr");
        var table = tr.closest(".k-grid").data("kendoGrid");
        var item = table.dataItem(tr);
        var url = `~/XSvcAuth/connect/${item.UniqueConnectionName}`;
        window.open(ITVenture.Helpers.ResolveUrl(url), "_blank", "width=600,height=700");
    },
    RunServiceTest: function (e) {
        var btn = $(e.sender.element);
        var p = btn.closest("div");
        var f = p.children("form");
        f.submit();
    },
    submitTestData: async function (e) {
        e.preventDefault();
        var sda = $(e.target).serialize();
        try {
            var response = await ITVenture.Ajax.ajaxFormPost("~/Connectivity/ExternalService/PerformServiceTest", sda, "text");
            $(`#${$(e.target).attr("id")}Result`).text(response);
        }
        catch (err) {
            $(`#${$(e.target).attr("id")}Result`).text(JSON.stringify(err));
        }
    }
};