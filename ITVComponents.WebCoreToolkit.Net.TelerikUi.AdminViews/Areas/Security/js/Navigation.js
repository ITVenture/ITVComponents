ITVenture.Pages.Navigation= {
    reorderer: [],
    NavigationRequestCompleted: function(e) {
        if (e.type === "create" || e.type === "update" || e.type === "destroy") {
            //ITVenture.Pages.Navigation.rebuildNavigation();
            e.sender.read();
        }
    },
    /*rebuildNavigation: function() {
        $('#navdiv').load('/Navigation/BuildNavigation');
    },*/
    initReordering: function(e) {
        var currentName = e.sender.element.attr("id");
        if (ITVenture.Pages.Navigation.reorderer[currentName] == null || ITVenture.Pages.Navigation.reorderer[currentName].invalid()) {
            ITVenture.Pages.Navigation.reorderer[currentName] = ITVenture.Tools.RowReordering.Initialize(currentName,
                function (entity, newIndex) {
                    entity.SortOrder = newIndex;
                },true);
        }
        ITVenture.Pages.Navigation.reorderer[currentName].styleDraggers();
    }/*,
    initReordering: function(e) {
        var currentName = e.sender.element.attr("id");
        if (ITVenture.Pages.Navigation.reorderer[currentName] == null || ITVenture.Pages.Navigation.reorderer[currentName].invalid()) {
            ITVenture.Pages.Navigation.reorderer[currentName] = ITVenture.Tools.RowReordering.Initialize(currentName,
                function(entity, newIndex) {
                    entity.SortOrder = newIndex;
                },true);
        }
        ITVenture.Pages.Navigation.reorderer[currentName].styleDraggers();
    }*/,
    toggleReordering: function(e) {
        var reorderControl = ITVenture.Pages.Navigation.reorderer[$(e.currentTarget).parent().parent().attr("id")];
        reorderControl.enabled = !reorderControl.enabled;
        $(e.currentTarget).text(reorderControl.enabled ? ITVenture.Text.getText("ITVGlobal_Disable_Reorder","Disable Reorder") : ITVenture.Text.getText("ITVGlobal_Enable_Reorder","Enable Reorder"));
    },
    JoinTenants: function(arr) {
        console.log(arguments);
        if (typeof(arr) !== "undefined") {
            var retVal = "<ul>";
            for (var i = 0; i < arr.length; i++) {
                retVal += "<li>" + arr[i].Label + "</li>";
            }

            retVal += "</ul>";
            return retVal;
        }
        return "";
    }
}