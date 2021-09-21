ITVenture.Tools.DashboardWidgets = {
    widgets: {},
    addWidget: async function (name, target) {
        if (ITVenture.Tools.DashboardWidgets.widgets.hasOwnProperty(name)) {
            delete ITVenture.Tools.DashboardWidgets.widgets[name];
        }

        var widget = await ITVenture.Ajax.ajaxGet("~/DBW/".concat(name), "json");
        widget.target = target;
        widget.lastResult = {};
        ITVenture.Tools.DashboardWidgets.widgets[name] = widget;
        ITVenture.Tools.DashboardWidgets.renderAllWidgets(false);
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
            $(widget.target).html(markup);
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
        var element = $(dashboard.element);
        for (var i = 0; i < dashboard.items.length; i++) {
            var item = dashboard.items[i];
            var panel = element.find("#".concat(item.id));
            var layoutTarget = panel.find("[dashboard-id]");
            items.push({
                SortOrder: item.order,
                WidgetId: layoutTarget.attr("dashboard-id")
            });
        }

        await ITVenture.Ajax.ajaxFormPost("~/DBW", JSON.stringify(items), "text", "application/json");
    },
    kendoExt: {
        defaultHeader:
            '<div class="k-tilelayout-item-header k-card-header k-cursor-grab" style="margin-right:25px;"><div class="k-card-title">{{!$this.DisplayName}}</div></div><a class="k-button k-button-icon k-flat k-close-button"><span class="k-icon k-i-close"></span></a></div>',
        defaultBody: '<div id="DashboardItem{{!$this.DashboardWidgetId}}" dashboard-id="{{!$this.DashboardWidgetId}}" dashboard-name="{!$this.SystemName}}"></div>',
        closeDashboardItem: function (e) {
            var currentTarget = $(e.currentTarget);
            var dashLayout = currentTarget.closest(".k-tilelayout").data("kendoTileLayout");
            var panel = currentTarget.closest(".k-tilelayout-item");
            var layoutTarget = panel.find("[dashboard-id]");
            var systemName = layoutTarget.attr("dashboard-name");
            var itemId = panel.attr("id");
            var dashItems = dashLayout.items;
            var item = dashLayout.itemsMap[itemId];
            dashItems.splice(dashItems.indexOf(item), 1);
            delete ITVenture.Tools.DashboardWidgets.widgets[systemName];
            dashLayout.setOptions({ containers: dashItems });
            ITVenture.Tools.DashboardWidgets.saveDashboard(dashLayout);
            ITVenture.Tools.DashboardWidgets.renderAllWidgets(false);
        },
        dashboardReorder: function (e) {
            ITVenture.Tools.DashboardWidgets.saveDashboard(e.sender);
        },
        addDashboard: async function (e) {
            var btn = e.sender.element;
            var dropdownName = btn.attr("dropdown");
            var dashboardName = btn.attr("dashboard");
            var dropdown = $("#".concat(dropdownName)).data("kendoDropDownList");
            var dashboard = $("#".concat(dashboardName)).data("kendoTileLayout");
            var id = dropdown.value();
            var prevItems = dashboard.items;
            if (id !== null && id !== "") {
                var selectedItem = dropdown.dataItem(dropdown.selectedIndex);
                var header = ITVenture.Text.processMessage("$",
                    ITVenture.Tools.DashboardWidgets.kendoExt.defaultHeader,
                    selectedItem.FullRecord);
                var body = ITVenture.Text.processMessage("$",
                    ITVenture.Tools.DashboardWidgets.kendoExt.defaultBody,
                    selectedItem.FullRecord);
                var item = {
                    bodyTemplate: body,
                    header: { template: header },
                    order: prevItems.length
                }

                prevItems.push(item);
                dashboard.setOptions({ containers: prevItems });
                ITVenture.Tools.DashboardWidgets.addWidget(selectedItem.FullRecord.SystemName,
                    "#".concat("DashboardItem").concat(selectedItem.FullRecord.DashboardWidgetId));
                await ITVenture.Tools.DashboardWidgets.saveDashboard(dashboard);
                ITVenture.Tools.DashboardWidgets.renderAllWidgets(false);
            }
        }
    }
}