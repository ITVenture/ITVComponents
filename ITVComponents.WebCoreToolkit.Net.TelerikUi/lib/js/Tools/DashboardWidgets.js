ITVenture.Tools.DashboardWidgets = {
    nextMultiWidgetId: 0,
    widgets: {},
    addWidget: async function (widget) {
        //var widget = await ITVenture.Ajax.ajaxGet("~/DBW/".concat(name).concat((typeof userWidgetId !== "undefined")?"/".concat(userWidgetId):""), "json");
        var addWidget = false;
        //widget.target = target;
        var nxid = ++ITVenture.Tools.DashboardWidgets.nextMultiWidgetId;
        var name = widget.SystemName;
        var localRef = name.concat(nxid);
        if (typeof widget.Params === "object" && Array.isArray(widget.Params) && widget.Params.length !== 0) {
            var param = {};
            var markup = "<table id='cfg4".concat(localRef)
                .concat(
                    "' style='width:95%'><colgroup><col style='width:150px;'/><col /></colgroup><thead><tr class='k-grid-header'><td class='k-header'>")
                .concat(ITVenture.Text.getText("Dashboard-Parameter name")).concat("</td><td class='k-header'>")
                .concat(ITVenture.Text.getText("Dashboard-Parameter value")).concat("</td></tr></thead><tbody>");
            for (var i = 0; i < widget.Params.length; i++) {
                markup = markup.concat("<tr><td>").concat(widget.Params[i].ParameterName)
                    .concat("</td><td><div id='Edit4").concat(widget.Params[i].ParameterName)
                    .concat("_'></div></td></tr>");
            }
            markup = markup.concat("</tbody><tfoot><tr><td colspan='2'><button class='k-primary' id='Accept4")
                .concat(localRef).concat("'>")
                .concat(ITVenture.Text.getText("Save dashboard"))
                .concat("</button><button class='k-primary' id='Cancel4").concat(localRef).concat("'>")
                .concat(ITVenture.Text.getText("Popup_General_Cancel")).concat("</button>")
                .concat("</td></tr></tfoot>");
            var dlg = {
                rawContent: markup,
                dialogOpening:
                    function (obj, dg) {
                        dg.title(ITVenture.Text.getText("Dashboard-Config"));
                        dg.setOptions({ width: 400 });
                    },
                contentReady: function (obj, dg) {
                    var robj = obj.data("itvDialog");
                    var cdi = robj.window.find("#cfg4".concat(localRef));
                    var btn = robj.window.find("#Accept4".concat(localRef));
                    var btn2 = robj.window.find("#Cancel4".concat(localRef));
                    btn.on("click", robj.Accept);
                    btn2.on("click", robj.Close);
                    for (var i = 0; i < widget.Params.length; i++) {
                        var p = cdi.find("#Edit4".concat(widget.Params[i].ParameterName).concat("_"));
                        param[widget.Params[i].ParameterName] = ITVenture.Tools.DynamicData.RenderControl(p,
                            "EditBox4".concat(widget.Params[i].ParameterName),
                            widget.Params[i].InputType,
                            JSON.parse(widget.Params[i].InputConfig));
                    }
                }
            }

            var ok = false;
            var dlgResult = await ITVenture.Tools.Popup.OpenAsync("generic", dlg);
            ok = dlgResult.accepted;
            if (ok) {
                for (var n in param) {
                    if (param.hasOwnProperty(n)) {
                        if (typeof (param[n].detailedValue) === "function") {
                            param[n] = param[n].detailedValue();
                        } else {
                            param[n] = param[n].value();
                        }
                    }
                }

                widget.CustomQueryString = ITVenture.Text.processMessage("", widget.CustomQueryString, param);
                if (widget.TitleTemplate !== null && widget.TitleTemplate !== "") {
                    widget.DisplayName = ITVenture.Text.processMessage("", widget.TitleTemplate, param);
                }
                addWidget = true;
            }
        }
        else {
            addWidget = true;
        }

        if (addWidget) {
            if (ITVenture.Tools.DashboardWidgets.widgets.hasOwnProperty(localRef)) {
                delete ITVenture.Tools.DashboardWidgets.widgets[localRef];
            }
            widget.lastResult = {};
            widget.LocalRef = localRef;
            ITVenture.Tools.DashboardWidgets.widgets[localRef] = widget;
            ITVenture.Tools.DashboardWidgets.renderAllWidgets(false);
            return localRef;
        }

        return null;
    },
    renderWidget: async function (name, fetch) {
        if (typeof (fetch) === "undefined") {
            fetch = true;
        }

        if (ITVenture.Tools.DashboardWidgets.widgets.hasOwnProperty(name)) {
            var widget = ITVenture.Tools.DashboardWidgets.widgets[name];
            var queryResult = fetch ? await ITVenture.Ajax.ajaxGet("~/Diagnostics/".concat((widget.Area != null && widget.Area !== "") ? widget.Area.concat("/") : "").concat(widget.QueryName).concat((widget.CustomQueryString != null && widget.CustomQueryString !== "") ? "?".concat(widget.CustomQueryString) : ""), "json") : widget.lastResult;
            widget.lastResult = queryResult;
            var markup = ITVenture.Text.processMessage("-", widget.Template, queryResult);
            $("#".concat(name)).html(markup);
        }
    },
    removeWidget: function (name) {
        if (ITVenture.Tools.DashboardWidgets.widgets.hasOwnProperty(name)) {
            delete ITVenture.Tools.DashboardWidgets.widgets[name];
        }
    },
    renderAllWidgets: function (fetch) {
        if (typeof (fetch) === "undefined") {
            fetch = true;
        }
        for (var name in ITVenture.Tools.DashboardWidgets.widgets) {
            if (ITVenture.Tools.DashboardWidgets.widgets.hasOwnProperty(name)) {
                ITVenture.Tools.DashboardWidgets.renderWidget(name, fetch);
            }
        }
    },
    saveDashboard: async function (dashboard) {
        var items = [];
        for (var i = 0; i < dashboard.items.length; i++) {
            var item = dashboard.items[i];
            var localRef = item.LocalRef;
            var refItem = ITVenture.Tools.DashboardWidgets.widgets[localRef];
            refItem.SortOrder = item.order;
            items.push(refItem);
        }

        var retItems = await ITVenture.Ajax.ajaxFormPost("~/DBW", JSON.stringify(items), "json", "application/json");
        for (var a = 0; a < retItems.length; a++) {
            var retItem = retItems[a];
            Object.assign(ITVenture.Tools.DashboardWidgets.widgets[retItem.LocalRef], retItem);
        }
    },
    kendoExt: {
        defaultHeader:
            '<div class="k-tilelayout-item-header k-card-header k-cursor-grab" style="margin-right:25px;"><div class="k-card-title">{{!$this.DisplayName}}</div></div><a class="k-button k-button-icon k-flat k-close-button"><span class="k-icon k-i-close"></span></a></div>',
        defaultBody: '<div id="{{!$this.LocalRef}}" dashboard-id="{{!$this.DashboardWidgetId}}" dashboard-name="{{!$this.SystemName}}"></div>',
        closeDashboardItem: function (e) {
            var currentTarget = $(e.currentTarget);
            var dashLayout = currentTarget.closest(".k-tilelayout").data("kendoTileLayout");
            var panel = currentTarget.closest(".k-tilelayout-item");
            var layoutTarget = panel.find("[dashboard-id]");
            var systemName = layoutTarget.attr("id");
            var itemId = panel.attr("id");
            var dashItems = dashLayout.items;
            var item = dashLayout.itemsMap[itemId];
            dashItems.splice(dashItems.indexOf(item), 1);
            console.log(item.LocalRef);
            delete ITVenture.Tools.DashboardWidgets.widgets[systemName];
            //delete ITVenture.Tools.DashboardWidgets.widgets[systemName];
            dashLayout.setOptions({ containers: dashItems });
            ITVenture.Tools.DashboardWidgets.saveDashboard(dashLayout);
            ITVenture.Tools.DashboardWidgets.renderAllWidgets(false);
        },
        dashboardReorder: function (e) {
            ITVenture.Tools.DashboardWidgets.saveDashboard(e.sender);
        },
        addDashboard: async function (e, dashboardId, userWidgetId) {
            var id;
            var dashboardName;
            var save = false;
            if (typeof (e) !== "string") {
                var btn = e.sender.element;
                var dropdownName = btn.attr("dropdown");
                dashboardName = btn.attr("dashboard");
                var dropdown = $("#".concat(dropdownName)).data("kendoDropDownList");
                id = dropdown.value();
                save = true;
            } else {
                id = e;
                dashboardName = dashboardId;
            }

            var widget = await ITVenture.Ajax.ajaxGet("~/DBW/".concat(id).concat((typeof userWidgetId !== "undefined") ? "/".concat(userWidgetId) : ""), "json");
            var dashboard = $("#".concat(dashboardName)).data("kendoTileLayout");
            var prevItems = dashboard.items;
            if (id !== null && id !== "") {

                var name = await ITVenture.Tools.DashboardWidgets.addWidget(widget);
                if (name != null) {
                    var header = ITVenture.Text.processMessage("$",
                        ITVenture.Tools.DashboardWidgets.kendoExt.defaultHeader,
                        widget);
                    var body = ITVenture.Text.processMessage("$",
                        ITVenture.Tools.DashboardWidgets.kendoExt.defaultBody,
                        widget);
                    var item = {
                        bodyTemplate: body,
                        header: { template: header },
                        order: prevItems.length
                    };
                    item.LocalRef = name;
                    prevItems.push(item);
                    dashboard.setOptions({ containers: prevItems });
                    if (save) {
                        await ITVenture.Tools.DashboardWidgets.saveDashboard(dashboard);
                        ITVenture.Tools.DashboardWidgets.renderAllWidgets(false);
                    }
                }
            }
        }
    }
}