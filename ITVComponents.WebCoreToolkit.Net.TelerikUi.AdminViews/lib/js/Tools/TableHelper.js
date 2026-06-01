ITVenture.Tools.TableHelper = {
    refreshTable: function (e) {
        e.preventDefault();
        var target = $(e.currentTarget);
        while (typeof target.data().kendoGrid === "undefined" || target.data().kendoGrid === null) {
            target = target.parent();
        }

        ITVenture.Tools.KendoExtensions.RefreshSources();
        target.data("kendoGrid").dataSource.read();
    },
    syncDataGrid: function (e) {
        e.preventDefault();
        var target = $(e.currentTarget);
        while (typeof target.data().kendoGrid === "undefined" || target.data().kendoGrid === null) {
            target = target.parent();
        }

        ITVenture.Tools.KendoExtensions.RefreshSources();
        target.data("kendoGrid").dataSource.sync();
    },
    getDataItem: function(e) {
        var tr = $(e.target).closest("tr"); //get the row for deletion
        var grid = ITVenture.Tools.TableHelper.getGrid(e);
        var data = grid.dataItem(tr); //get the row data so it can be referred later
        return data;
    },
    getGrid: function(e) {
        var target = $(e.currentTarget);
        return target.closest(".k-grid").data("kendoGrid");
    },
    confirmDelete: function (e) {
        e.preventDefault(); //prevent page scroll reset
        var tr = $(e.target).closest("tr"); //get the row for deletion
        var data = this.dataItem(tr); //get the row data so it can be referred later
        var target = $(e.currentTarget);
        while (typeof (target.data("kendoGrid")) === "undefined" || target.data("kendoGrid") === null) {
            target = target.parent();
        }
        ITVenture.Tools.Popup.Open("confirm",
            ITVenture.Text.getText("TableHelp_DeleteConfirm_Body","Do you want to delete the current Record?"),
            function () {
                var g = $(target).data("kendoGrid");
                g.dataSource.remove(data);
                g.dataSource.sync();
            });
    },
    resolveFkValue: function (dataItem, columnName, grid) {
        var row = grid.element.find("tr[data-uid='" + dataItem.uid + "']");
        var targetColumn = $.grep(grid.columns, function (e) { return e.field === columnName; });
        if (targetColumn.length === 1) {
            var col = targetColumn[0];
            var colId = $.inArray(col, grid.columns);
            if (colId !== -1) {
                var theTarget = $(row.children("td:not([class='k-hierarchy-cell'])")[colId]);
                var theRealThing = $.grep(col.values, function (e) { return e.value === dataItem[columnName]; })[0];
                theTarget.html(theRealThing.text);
            }
        }
    },
    defaultCheckedCallback: function(e) {
        var elem = e.sender.element;
        var colName = elem.attr("data-col-name");
        var row = e.sender.element.closest("tr");
        var grid = row.closest(".k-grid").data("kendoGrid");
        var dataItem = grid.dataItem(row);
        var checked = e.checked;
        dataItem.dirty = true;
        dataItem[colName] = checked;
        //dataItem.set(colName, checked);
        grid.saveChanges();
    },
    defaultMultiSelectCallback: function (withAutoSave) {
        var retVal = function (e) {
            var elem = e.sender.element;
            var colName = elem.attr("data-col-name");
            var row = e.sender.element.closest("tr");
            var grid = row.closest(".k-grid").data("kendoGrid");
            var dataItem = grid.dataItem(row);
            var value = $(elem).data("kendoMultiSelect").value();
            dataItem.dirty = true;
            dataItem[colName] = value;
            //dataItem.set(colName, value);
            if (retVal.withAutoSave) {
                grid.saveChanges();
            }
        };

        retVal.withAutoSave = withAutoSave;
        return retVal;
    },
    serializeArrays: function(data) {
        for (var a in data) {
            if (data.hasOwnProperty(a) && $.isArray(data[a])) {
                var rr = data[a];
                for (var i = 0; i < rr.length; i++) {
                    if (typeof(rr[i]) === "object" && typeof(rr[i].Key) !== "undefined") {
                        rr[i] = rr[i].Key;
                    }
                }
            }
        }
    }
};