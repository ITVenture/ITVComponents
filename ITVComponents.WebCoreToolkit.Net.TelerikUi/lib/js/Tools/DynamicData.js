ITVenture.Tools.DynamicData = {
    AutoSizeColumns: function () {
        for (var i = 0; i < this.columns.length; i++) {
            this.autoFitColumn(i);
        }
    },
    RenderControl: function (parent, id, type, config, customCtlAttr) {
        //var customCtlAttr = "";
        if (typeof customCtlAttr === "undefined" || customCtlAttr == null) {
            customCtlAttr = "";
        }
        var ctl = "";
        switch (type.toLowerCase()) {
            case "switch":
                ctl = ctl.concat("<input id='").concat(id).concat("' type='checkbox' ").concat(customCtlAttr)
                    .concat(" />");
                break;
            case "number":
                customCtlAttr = customCtlAttr.concat(" type='number' min='0'");
            default:
                ctl = ctl.concat("<input id='").concat(id).concat("' style='width: 100%;' ").concat(customCtlAttr)
                    .concat(" />");
                break;
        }

        parent.append(ctl);
        var refO = parent.find("#".concat(id));
        var cfg;
        switch (type.toLowerCase()) {
            case "switch":
                cfg = {
                    messages: {
                        checked: "Ja",
                        unchecked: "Nein",
                    }
                };
                if (config != null) {
                    cfg = $.extend(true, cfg, config);
                }
                var retVal = {
                    decorated: refO.kendoSwitch(cfg).data("kendoSwitch"),
                    value: function (val) {
                        if (typeof (val) === "undefined") {
                            return retVal.decorated.value();
                        }

                        var m = val != null;
                        if (m) {
                            m = val.toString().toLowerCase() === "true";
                        }

                        return retVal.decorated.value(m);
                    }
                };
                return retVal;
            case "text":
                cfg = {};
                if (config != null) {
                    cfg = $.extend(true, cfg, config);
                }
                return refO.kendoTextBox(cfg).data("kendoTextBox");
            case "maskedtext":
                cfg = {};
                if (config != null) {
                    cfg = $.extend(true, cfg, config);
                }
                return refO.kendoMaskedTextBox(cfg).data("kendoMaskedTextBox");
            case "number":
                cfg = {};
                if (config != null) {
                    cfg = $.extend(true, cfg, config);
                }
                return refO.kendoNumericTextBox(cfg).data("kendoNumericTextBox");
            case "combo":
                cfg = {
                    dataTextField: "text",
                    dataValueField: "value",
                    filter: "startswith",
                };
                if (config != null) {
                    cfg = $.extend(true, cfg, config);
                }
                //return 
                var refCo = refO.kendoDropDownList(cfg).data("kendoDropDownList");
                var retCo = {
                    decorated: refCo,
                    value: function (val) {
                        if (typeof (val) === "undefined") {
                            return retCo.decorated.value();
                        }

                        return retCo.decorated.value(val);
                    },
                    detailedValue: function () {
                        return retCo.decorated.dataItem();
                    }
                };
                return retCo;
            default:
                throw "invalid type provided!";
        }
    }
}