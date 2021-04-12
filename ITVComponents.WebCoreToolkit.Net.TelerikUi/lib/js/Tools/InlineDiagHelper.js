ITVenture.Tools.InlineDiagnostics = {
    DiagPopupFor: function(elementName) {
        var target = $("#".concat(elementName));
        var tr = target.closest("tr"); //get the row for deletion
        var table = tr.closest(".k-grid").data("kendoGrid");
        var item = table.dataItem(tr);
        var col = target.attr("diagProperty");
        var targetElem = $(target.children()[0]);
        var data = item[col];
        var popupStyle = "";
        targetElem.html("");
        if (data != null) {
            if ((data.Result & 4) === 4) {
                targetElem.addClass("fas fa-times-circle");
                targetElem.attr("style", "color:red");
                popupStyle = "background-color:#b34e4d;text-shadow:0 -1px 0 #1a3c4d;color:white";
            } else if ((data.Result & 2) === 2) {
                targetElem.addClass("fas fa-exclamation-circle");
                targetElem.attr("style", "color:#FACC00");
                popupStyle = "background-color:#c09854;text-shadow:0 -1px 0 #1a3c4d;color:white";
            } else {
                targetElem.addClass("fas fa-check-circle");
                targetElem.attr("style", "color:green");
                popupStyle = "background-color:green;text-shadow:0 -1px 0 #1a3c4d;color:white";
            }

            var sfx = ITVenture.Tools.InlineDiagnostics.GetPopupMethod(popupStyle);
            target
                .kendoTooltip({
                    content: data.Message,
                    position: "right",
                    callout: false,
                    show: sfx
                });
        }
    },
    GetPopupMethod: function(popupStyle) {
        var retVal = function (e) {
            if (!retVal.styled) {
                e.sender.popup.element
                    .attr("style",
                        retVal.popupStyle);
                e.sender.arrow.attr("style",
                    retVal.arrowStyle);
                e.sender.popup.element.css("white-space", "pre");
                e.sender.popup.element.css("text-align", "left");
                retVal.styled = true;
            }
        };

        retVal.popupStyle = popupStyle;
        retVal.styled = false;
        retVal.arrowStyle = "border-right-color:red";
        return retVal;
    }
}