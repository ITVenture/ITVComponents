ITVenture.Tools.KendoExtensions = {
    multiSelectSources: {},
    msData: {},
    msProm: {},
    UseForeignKeyFilter: function(valuePrimitive, additionalHandler) {
        var retVal = function(e) {
            var ddl = e.container.find("[data-role= 'dropdownlist']").data("kendoDropDownList");
            if (ddl) {
                var opt = { filter: 'contains' };
                if (retVal.valuePrimitive) {
                    opt.valuePrimitive = retVal.valuePrimitive;
                }

                ddl.setOptions(opt);
                if (typeof retVal.additionalHandler === "function") {
                    retVal.additionalHandler.apply(this, arguments);
                }
            }
        };

        retVal.valuePrimitive = valuePrimitive;
        retVal.additionalHandler = additionalHandler;
        return retVal;
    },
    RefreshSources: function () {
        for (var n in ITVenture.Tools.KendoExtensions.multiSelectSources) {
            if (ITVenture.Tools.KendoExtensions.multiSelectSources.hasOwnProperty(n)) {
                var src = ITVenture.Tools.KendoExtensions.multiSelectSources[n];
                delete ITVenture.Tools.KendoExtensions.msData[n];
                delete ITVenture.Tools.KendoExtensions.msProm[n];
                src.read();
            }
        }
    },
    ForeignKeyPrimitive: function(additionalHandler) {
        var retVal = function(e) {
            var ddl = e.container.find("[data-role= 'dropdownlist']").data("kendoDropDownList");
            if (ddl) {
                var opt = { valuePrimitive: true };
                ddl.setOptions(opt);
                if (typeof retVal.additionalHandler === "function") {
                    retVal.additionalHandler.apply(this, arguments);
                }
            }
        };

        retVal.additionalHandler = additionalHandler;
        return retVal;
    },
    InitInlineCheckbox: function(pkName, idRaw, enabled, customHandler) {
        var retVal = function() {
            var nameId = this[retVal.pkName].toString();
            var qry = retVal.queryRaw.concat(nameId);
            var tmp = function () {
                $(tmp.query).kendoSwitch({
                    enabled: retVal.enabled,
                    change: retVal.handler
            });
            };
            tmp.query = qry;
            $(tmp);
        };

        retVal.pkName = pkName;
        retVal.queryRaw = "#".concat(idRaw);
        retVal.enabled = enabled;
        if (typeof customHandler === "function") {
            retVal.handler = customHandler;
        } else {
            retVal.handler = ITVenture.Tools.TableHelper.defaultCheckedCallback;
        }

        return retVal;
    },
    InitMultiSelect: function (column, pkName, idRaw, enabled, placeHolder, url, changeHandler) {
        if (!ITVenture.Tools.KendoExtensions.multiSelectSources.hasOwnProperty(url)) {
            ITVenture.Tools.KendoExtensions.multiSelectSources[url] = new kendo.data.DataSource({
                transport: {
                    read: async function (e) {
                        // On success.
                        if (!ITVenture.Tools.KendoExtensions.msData.hasOwnProperty(url)) {
                            if (!ITVenture.Tools.KendoExtensions.msProm.hasOwnProperty(url)) {
                                ITVenture.Tools.KendoExtensions.msProm[url] = ITVenture.Ajax.ajaxGet(url, "json");
                                ITVenture.Tools.KendoExtensions.msData[url] = await ITVenture.Tools.KendoExtensions.msProm[url];
                            }
                            else {
                                await ITVenture.Tools.KendoExtensions.msProm[url];
                            }
                        }

                        e.success({ data: ITVenture.Tools.KendoExtensions.msData[url], total: ITVenture.Tools.KendoExtensions.msData[url].length });
                        // On failure.
                        //e.error("XHR response", "status code", "error message");
                    }
                },
                error: function (e) {
                    // Handle data operation error.
                    alert("Status: " + e.status + "; Error message: " + e.errorThrown);
                },
                schema: {
                    data: "data",
                }
            });
        }

        var retVal = function () {
            if (typeof this[retVal.column] !== "undefined") {
                var nameId = this[retVal.pkName].toString();
                var qry = retVal.queryRaw.concat(nameId);
                var ids = this[retVal.column];
                var tmp = function () {
                    var cfg = {
                        dataTextField: "Label",
                        dataValueField: "Key",
                        enable: retVal.enabled,
                        placeholder: retVal.placeHolder,
                        dataSource: ITVenture.Tools.KendoExtensions.multiSelectSources[url]
                    };

                    if (retVal.enabled) {
                        cfg.change = retVal.handler;
                    }
                    var select = $(tmp.query).kendoMultiSelect(cfg).data("kendoMultiSelect");
                    if (typeof select !== "undefined") {
                        select.value(tmp.ids);
                        select.one("dataBound", function () { select.value(tmp.ids); });
                    }
                };

                tmp.query = qry;
                tmp.dataItem = this;
                tmp.ids = ids;
                $(tmp);
            }
        };

        retVal.column = column;
        retVal.pkName = pkName;
        retVal.queryRaw = "#".concat(idRaw);
        retVal.enabled = enabled;
        retVal.placeHolder = placeHolder;
        retVal.url = url;
        if (typeof changeHandler === "function") {
            retVal.handler = changeHandler;
        }

        return retVal;
    }
};