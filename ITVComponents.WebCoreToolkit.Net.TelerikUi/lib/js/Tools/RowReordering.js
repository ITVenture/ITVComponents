ITVenture.Tools.RowReordering = {
    Initialize: function (targetControl, afterShiftCallback, withDetails) {
        if (typeof (withDetails) === "undefined" || withDetails == null) {
            withDetails = false;
        }
        var retVal = {
            afterShift: afterShiftCallback,
            enabled: false,
            detailRow: null,
            sortableTable: null,
            orderChanged: function (a) {
                if (retVal.detailRow != null) {
                    a.item.after(retVal.detailRow);
                }
                var grid = a.sender.element.data().kendoGrid;
                var //skip = grid.dataSource.skip(),
                    oldIndex = a.oldIndex,
                    newIndex = a.newIndex,
                    data = grid.dataSource.data(),
                    dataItem = grid.dataSource.getByUid(a.item.data("uid"));
                var from = newIndex, to = oldIndex - 1;
                var direction = 1;
                if (oldIndex < newIndex) {
                    from = oldIndex + 1;
                    to = newIndex;
                    direction = -1;
                }

                for (var i = from; i <= to; i++) {
                    retVal.afterShift(data[i], i + direction);
                    //data[i].StepOrder = i + direction;
                    data[i].dirty = true;
                }

                retVal.afterShift(data[oldIndex], newIndex);
                //data[oldIndex].StepOrder = newIndex;
                data[oldIndex].dirty = true;
                if (typeof (retVal.beforeSave) === "function") {
                    retVal.beforeSave.apply(retVal);
                }
                grid.saveChanges();
                if (typeof (retVal.afterSave) === "function") {
                    retVal.afterSave.apply(retVal);
                }
            },
            reorderHint: function (element) {
                var grid = element.closest("div.k-grid");
                grid = grid.data().kendoGrid;
                var table = grid.table.clone(), //clone Grid's table
                wrapperWidth = grid.wrapper.width(), //get Grid's width
                wrapper = $("<div class='k-grid k-widget'></div>").width(wrapperWidth);

                table.find("thead").remove(); //remove Grid's header from the hint
                table.find("tbody").empty(); //remove the existing rows from the hint
                table.wrap(wrapper); //wrap the table
                table.append(element.clone()); //append the dragged element
                if (element.next()[0] != null && element.next()[0].className.indexOf("k-detail-row") != -1) {
                    table.append(element.next().clone());
                }
                var hint = table.parent(); //get the wrapper

                return hint; //return the hint element
            },
            reorderPlaceholder: function (element) {
                return element.clone().addClass("k-state-hover").css("opacity", 0.65);
            },
            reorderEnd: function (e) {
                retVal.detailRow = null;
                if (e.draggableEvent.currentTarget.next()[0] != null &&
                    e.draggableEvent.currentTarget.next()[0].className.indexOf("k-detail-row") != -1) {
                    retVal.detailRow = e.draggableEvent.currentTarget.next();
                }
            },
            reorderStart: function (e) {
                if (!retVal.enabled) {
                    e.preventDefault();
                }
            },
            styleDraggers: function () {
                retVal.sortableTable.find(".sort-dragger").replaceWith("<span class='sort-dragger fa-solid fa-bars'></span>");
            },
            invalid: function () {
                try {
                    return !$.contains(document, retVal.sortableTable[0]);
                }
                catch (e) {
                }

                return false;
            }
        };
        var sortableTable = $("#" + targetControl);
        retVal.sortableTable = sortableTable;
        $(retVal.sortableTable)
            .data("kendoGrid")
            .bind("cancel",
                function (e) {
                    console.log(e);
                    $(retVal.sortableTable).data("kendoGrid").cancelChanges();
                });
        retVal.withDetails = withDetails;
        retVal.sortable = sortableTable
             .kendoSortable({
                 start: retVal.reorderStart,
                 change: retVal.orderChanged,
                 end: retVal.reorderEnd,
                 placeholder: retVal.reorderPlaceholder,
                 hint: retVal.reorderHint,
                 filter: withDetails ? ">div.k-grid-container>div.k-grid-content>table >tbody >tr:not(.k-detail-row)" : ">table >tbody >tr:not(.k-detail-row)",
                 handler: ".sort-dragger",
                 ignore: ":not(.sort-dragger)"
             });
        return retVal;
    }
};