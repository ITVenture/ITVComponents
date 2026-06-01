ITVenture.Tools.InlineBubble = {
    BubblePopupFor: function(elementName) {
        var target = $("#".concat(elementName));
        var tr = target.closest("tr"); //get the row for deletion
        var table = tr.closest(".k-grid").data("kendoGrid");
        var item = table.dataItem(tr);
        var col = target.attr("contentProperty");
        var bubbleStyle = target.attr("bubbleStyle");
        var targetElem = $(target.children()[0]);
        var data = item[col];
        targetElem.html("");
        if (data != null) {

            var popupStyle = bubbleStyle;

            var sfx = ITVenture.Tools.InlineBubble.GetPopupMethod(popupStyle);
            target
                .kendoTooltip({
                    content: data,
                    position: "right",
                    callout: false,
                    show: sfx
                });
        }
    },
    DiagPopupFor: function(elementName) {
        var target = $("#".concat(elementName));
        var tr = target.closest("tr"); //get the row for deletion
        var table = tr.closest(".k-grid").data("kendoGrid");
        var item = table.dataItem(tr);
        var col = target.attr("diagProperty");
        var targetElem = $(target.children()[0]);
        var popupStyle = "";
        var data = item[col];
        targetElem.html("");
        if (data != null) {
            if (data.hasOwnProperty("IconClass")) {
                targetElem.addClass(data.IconClass);
            }

            if (data.hasOwnProperty("IconStyle")) {
                targetElem.attr("style", data.IconStyle);
            }

            if (data.hasOwnProperty("PopupStyle")) {
                popupStyle = data.PopupStyle;
            }

            if (data.hasOwnProperty("Message")) {
                var sfx = ITVenture.Tools.InlineDiagnostics.GetPopupMethod(popupStyle);
                target
                    .kendoTooltip({
                        content: ITVenture.Text.getLocaleMessage(data.Message),
                        position: "right",
                        callout: false,
                        show: sfx
                    });
            }
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