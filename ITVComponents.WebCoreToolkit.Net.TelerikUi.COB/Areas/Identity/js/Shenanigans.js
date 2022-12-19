ITVenture.Pages.IdentityShenanigans = {
    OnFormPost: async function (e) {
        debugger;
        console.log(e);
        e.preventDefault();
        var target = $(e.currentTarget);
        console.log(target.attr("id"));
        var formOk = target.data("unobtrusiveValidation").validate();
        if (formOk) {
            var postData = target.serialize();
            var postTarget = target.attr("action");
            var req = ITVenture.Ajax.ajaxFormPost(postTarget, postData, "json");
            var ret = await req;

            console.log(req);
            console.log(ret);

            var errors = [];
            if (typeof ret === "object") {
                for (var name in ret) {
                    if (ret.hasOwnProperty(name) && name !== "") {
                        var validation = ret[name];
                        var ctl = $("[data-valmsg-for='".concat(name).concat("']"), target);
                        ITVenture.Pages.IdentityShenanigans.processValidationItem(validation, errors, ctl);
                    } else {
                        var validation = ret[name];
                        ITVenture.Pages.IdentityShenanigans.processValidationItem(validation, errors);
                    }
                }

                if (errors.length !== 0) {
                    var ls = ITVenture.Pages.IdentityShenanigans.buildValidationList(errors);
                    var summ = $("[data-valmsg-summary=true]", target);
                    if (summ.length != 0) {
                        summ.html(ls);
                    }
                }
            }
            else if (typeof ret === "string") {
                window.location.href = ret;
            }
        }
    },
    processValidationItem: function (validation, errors, htmlElement) {
        if (validation.Errors.length !== 0) {
            var p = "";
            for (var i = 0; i < validation.Errors.length; i++) {
                var err = validation.Errors[i].ErrorMessage;
                var encodedError = ITVenture.Tools.HtmlHelper.encode(err);
                p = p.concat("<p>").concat(encodedError).concat("</p>");
                errors.push(encodedError);
            }

            if (typeof htmlElement !== "undefined") {
                htmlElement.html(p);
            }
        }
    },
    buildValidationList: function (errors) {
        var p = "<ul>";
        for (var i = 0; i < errors.length; i++) {
            p = p.concat("<li>").concat(errors[i]).concat("</li>");
        }
        p = p.concat("</ul>");
        return p;
    }
}