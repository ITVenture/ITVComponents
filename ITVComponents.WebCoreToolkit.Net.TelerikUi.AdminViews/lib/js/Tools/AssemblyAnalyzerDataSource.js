ITVenture.Tools.AssemblyAnalyzerDataSource={
    currentConstructors: [],
    currentGenericArguments:[],
    rootElement: null,
    InitializeFor: function(rootElement) {
        ITVenture.Tools.AssemblyAnalyzerDataSource.rootElement = rootElement;
        ITVenture.Tools.AssemblyAnalyzerDataSource.currentConstructors.length = 0;
        ITVenture.Tools.AssemblyAnalyzerDataSource.currentGenericArguments.length = 0;
    },
    GetConstructors: function(id) {
        var tmp = $(ITVenture.Tools.AssemblyAnalyzerDataSource.rootElement.find("#AllTypes")).data("kendoGrid");
        var src = tmp.dataSource._data;
        for (var i =0; i< src.length; i++){
            if (src[i].Uid===id) {
                var retVal = [];
                var retSrc = src[i].Constructors;
                for (var a = 0; a < retSrc.length; a++) {
                    retVal.push(retSrc[a]);
                    ITVenture.Tools.AssemblyAnalyzerDataSource.currentConstructors.push(retSrc[a]);
                }

                return new kendo.data.DataSource({ data: retVal });
            }
        }

        return null;
    },
    GetGenericParameters: function (id) {
        var tmp = $(ITVenture.Tools.AssemblyAnalyzerDataSource.rootElement.find("#AllTypes")).data("kendoGrid");
        var src = tmp.dataSource._data;
        for (var i = 0; i < src.length; i++) {
            if (src[i].Uid === id) {
                var retVal = [];
                var retSrc = src[i].GenericParameters;
                for (var a = 0; a < retSrc.length; a++) {
                    retVal.push(retSrc[a]);
                    ITVenture.Tools.AssemblyAnalyzerDataSource.currentGenericArguments.push(retSrc[a]);
                }

                return new kendo.data.DataSource({ data: retVal });
            }
        }

        return null;
    },
    GetArguments: function(id) {
        var src = ITVenture.Tools.AssemblyAnalyzerDataSource.currentConstructors;
        for (var i = 0; i < src.length; i++) {
            if (src[i].Uid === id) {
                var retVal = [];
                var retSrc = src[i].Parameters;
                for (var a = 0; a < retSrc.length; a++) {
                    retVal.push(retSrc[a]);
                }

                return new kendo.data.DataSource({ data: retVal });
            }
        }
    }
}