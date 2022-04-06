ITVenture.Tools.ListCallbackHelper = {
    filterScripts: {},
    dataCallbacks: {},
    currentFks: {},
    ResolveFk: async function (repo, table, id, emptyLabel, area) {
        var propName = repo.concat("_").concat(table).concat("_").concat(id);
        if (!ITVenture.Tools.ListCallbackHelper.currentFks.hasOwnProperty(propName)) {
            ITVenture.Tools.ListCallbackHelper.currentFks[propName] =
                ITVenture.Tools.ListCallbackHelper.makeResolver(repo, table, id, emptyLabel, area);
        }

        var prop = await ITVenture.Tools.ListCallbackHelper.currentFks[propName];
        return prop;
    },
    ShowPkValue: async function (repo, table, id, elementName, emptyLabel, area, dataUid) {
        var elementTarget = "#".concat(elementName);
        if (typeof(dataUid) !== "undefined" && dataUid != null) {
            elementTarget = "[data-uid=".concat(dataUid).concat("] ").concat(elementTarget);
        }
        var content = await ITVenture.Tools.ListCallbackHelper.ResolveFk(repo, table, id, emptyLabel, area);
        $(elementTarget).each(function(x) {
            $(this).html(content);
        });
    },
    normalizeFkValue: function(val) {
        var retVal =  val?.toString()?.replaceAll(/\W/gim, "_")??null;
        return retVal;
    },
    getFilterScript: function(filterScriptName) {
        var retVal = function(elem) {
            ITVenture.Tools.ListCallbackHelper.filterScripts[retVal.scriptName].apply(this, elem);
        };

        retVal.scriptName = filterScriptName;
        return retVal;
    },
    makeResolver: function(repo, table, id, emptyLabel, area) {
        if (area != null && area !== "") {
            area = "/".concat(area);
        } else {
            area = "";
        }
        var fx = new Promise(resolve => {
            ITVenture.Ajax.ajaxGet("~".concat(area).concat("/ForeignKey/").concat(repo).concat("/").concat(table).concat("/").concat(id),"json")
                .done(
                    function(data) {
                        if (data != null) {
                            resolve(data.Label);
                        } else {
                            resolve(emptyLabel);
                        }
                    }
                );
        });
        return fx;
    },
    GetFkIndex: function (target) {
        var ids = target.split("_");
        return ids[2];
    }
};