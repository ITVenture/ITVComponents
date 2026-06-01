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
    },
    IsOAuthService: function (e) {
        return e.AuthenticationType == 0;
    },
    OnAuthTypeSelected: function (e) {
        var authType = e.dataItem.Value.toString().padStart(2, '0');
        $(".authTypeConfig").hide();
        $(".authTypeControlContainer").hide(); 
        $(".authTypeConfig").attr("required", null);
        $(`.authTypeConfig[data-auth-type*='${authType}']`).show().closest("span.authTypeConfig").show();
        $(`.authTypeControlContainer[data-auth-type*='${authType}']`).show()
        $(".authTypeConfig.authLabel").each((c, d) => {
            if (typeof ($(d).attr("data-auth-label")) !== "undefined") {
                $(d).text(ITVenture.Text.getText(`${$(d).attr("data-auth-label")}${authType}`));
            }

            $(d).attr("required", $(d).attr("data-auth-required") === "true" ? "required" : null);
        });
    },
    editorReady: function () {
        var picker = $("#AuthenticationType").data("kendoDropDownList");
        picker.trigger("select", { dataItem: picker.dataItem() });
    }
};


ITVenture.Text.setText("de", "labelForClientId00", "Client-ID");
ITVenture.Text.setText("de", "labelForClientId01", "Client-ID");
ITVenture.Text.setText("de", "labelForClientId02", "Client-ID");
ITVenture.Text.setText("de", "labelForClientId03", "Benutzername");
ITVenture.Text.setText("de", "labelForClientId04", "API-Key-Header");
ITVenture.Text.setText("de", "labelForClientSecret00", "Client-Secret");
ITVenture.Text.setText("de", "labelForClientSecret01", "Passwort");
ITVenture.Text.setText("de", "labelForClientSecret02", "Passwort");
ITVenture.Text.setText("de", "labelForClientSecret03", "Passwort");
ITVenture.Text.setText("de", "labelForClientSecret04", "API-Key");
ITVenture.Text.setText("de", "labelForClientSecret05", "Bearer-Token");

ITVenture.Text.setText("fr", "labelForClientId00", "Identifiant client");
ITVenture.Text.setText("fr", "labelForClientId01", "Identifiant client");
ITVenture.Text.setText("fr", "labelForClientId02", "Identifiant client");
ITVenture.Text.setText("fr", "labelForClientId03", "Nom d'utilisateur");
ITVenture.Text.setText("fr", "labelForClientId04", "API-Key-Header");
ITVenture.Text.setText("fr", "labelForClientSecret00", "Secret client");
ITVenture.Text.setText("fr", "labelForClientSecret01", "Mot de passe");
ITVenture.Text.setText("fr", "labelForClientSecret02", "Mot de passe");
ITVenture.Text.setText("fr", "labelForClientSecret03", "Mot de passe");
ITVenture.Text.setText("fr", "labelForClientSecret04", "API-Key");
ITVenture.Text.setText("fr", "labelForClientSecret05", "Bearer-Token");

ITVenture.Text.setText("it", "labelForClientId00", "ID client");
ITVenture.Text.setText("it", "labelForClientId01", "ID client");
ITVenture.Text.setText("it", "labelForClientId02", "ID client");
ITVenture.Text.setText("it", "labelForClientId03", "Nome utente");
ITVenture.Text.setText("it", "labelForClientId04", "API-Key-Header");
ITVenture.Text.setText("it", "labelForClientSecret00", "Segreto client");
ITVenture.Text.setText("it", "labelForClientSecret01", "Password");
ITVenture.Text.setText("it", "labelForClientSecret02", "Password");
ITVenture.Text.setText("it", "labelForClientSecret03", "Password");
ITVenture.Text.setText("it", "labelForClientSecret04", "API-Key");
ITVenture.Text.setText("it", "labelForClientSecret05", "Bearer-Token");

ITVenture.Text.setText("en", "labelForClientId00", "Client-ID");
ITVenture.Text.setText("en", "labelForClientId01", "Client-ID");
ITVenture.Text.setText("en", "labelForClientId02", "Client-ID");
ITVenture.Text.setText("en", "labelForClientId03", "Username");
ITVenture.Text.setText("en", "labelForClientId04", "API-Key-Header");
ITVenture.Text.setText("en", "labelForClientSecret00", "Client-Secret");
ITVenture.Text.setText("en", "labelForClientSecret01", "Password");
ITVenture.Text.setText("en", "labelForClientSecret02", "Password");
ITVenture.Text.setText("en", "labelForClientSecret03", "Password");
ITVenture.Text.setText("en", "labelForClientSecret04", "API-Key");
ITVenture.Text.setText("en", "labelForClientSecret05", "Bearer-Token");
