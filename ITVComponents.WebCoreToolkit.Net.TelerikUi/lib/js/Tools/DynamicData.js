ITVenture.Tools.DynamicData = {
    AutoSizeColumns: function () {
        for (var i = 0; i < this.columns.length; i++) {
            this.autoFitColumn(i);
        }
    }
}