if (!ITVenture.Pages.Help) {
    ITVenture.Pages.Help = {};
}

ITVenture.Pages.Help.ModuleVideo = {
    fileCommitted: function (msg, origName, e) {
        var parentTutorial = e.attr("parentTutorial");
        $("#StreamList".concat(parentTutorial)).data("kendoGrid").dataSource.read();
    },
    FileUpdateHint: function (e) {
        var tr = e.closest("tr");
        var grid = e.closest(".k-grid");
        var table = grid.data("kendoGrid");
        var item = table.dataItem(tr);
        var parentTutorial = grid.attr("parentTutorial");
        return "##VID#".concat(parentTutorial).concat("#").concat(item.TutorialStreamId);
    },
    BatchFileUpdateHint: function (e) {
        var parentTutorial = e.attr("parentTutorial");
        return "##VID#".concat(parentTutorial);
    }
}