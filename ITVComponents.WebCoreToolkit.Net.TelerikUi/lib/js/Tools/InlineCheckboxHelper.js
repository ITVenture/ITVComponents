ITVenture.Tools.InlineCheckboxHelper = {
    ActivateChecks: function(e) {
        var table = e.sender.element;
        var data = table.data("kendoGrid").dataSource.data();
        var switches = $(table).find(".itv-icb-marker");
        $.each(switches,
            function(index, item) {
                var it = $(item);
                var callback = ITVenture.Tools.InlineCheckboxHelper.defaultCheckedCallback;
                var customCallback = it.attr("itvCustomChangeHandler");
                var readOnly = it.attr("itv-readOnly");
                readOnly = typeof readOnly === "string" && readOnly.toLowerCase() === "true";
                if (typeof customCallback === "string") {
                    try {
                        customCallback = eval(customCallback);
                        callback = customCallback;
                    } catch (ex) {
                        console.log(ex);
                    }
                }

                it.kendoSwitch({
                    change: callback,
                    enabled:!readOnly
                });
            });
    }
}