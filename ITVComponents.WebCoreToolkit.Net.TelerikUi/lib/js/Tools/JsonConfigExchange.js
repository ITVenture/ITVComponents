ITVenture.Tools.JSonCfgExchange = function (postTarget, idExtension, resultTab, resultCallback) {
    if (typeof resultCallback === "undefined" || resultCallback == null) {
        resultCallback = function(data) {
            return data;
        }
    }

    var comparer = {
        idExtension: idExtension,
        currentCompare: null,
        resultTab: resultTab,
        postTarget: postTarget,
        enrichPostObj: resultCallback,
        GetUploadHint: function(cbOrString) {
            if (typeof cbOrString === "function") {
                var ag = arguments.slice(1);
                return function() {
                    return cbOrString.apply(this, ag);
                };
            }

            return function() { return cbOrString; };
        },
        ProcessDifferences: function() {
            var tab = $("#".concat(resultTab).concat(" > tbody"));
            tab.html("");
            var idSeed = Math.floor(Math.random() * 4500);
            if (comparer.idExtension !== null) {
                idSeed = "".concat(idSeed).concat(comparer.idExtension);
            }
            var checkSeed = "check_".concat(idSeed);
            var tableSeed = "detTab_".concat(idSeed);
            var results = arguments[arguments.length - 1];
            comparer.currentCompare = results;
            for (var i = 0; i < results.length; i++) {
                var item = results[i];
                if (item.ChangeType < 3) {
                    var row = "<tr><td><span>".concat(item.EntityName)
                        .concat("</span></td>")
                        .concat("<td><span style='font-size:1.5rem;' class='fad fa-")
                        .concat(comparer.iconForType(item.ChangeType)).concat("'></span></td>")
                        .concat("<td><input type='checkbox' id='").concat(checkSeed).concat("_").concat(i).concat("'")
                        .concat(item.Apply ? " checked" : "").concat(" jccMode='dyn' jccRootId='").concat(i)
                        .concat("' jccType='check' /></td>")
                        .concat("<td>").concat((typeof (item.Key) !== "undefined" && item.Key != null) ? item.Key : "")
                        .concat("</td>")
                        .concat("<td><table id='").concat(tableSeed).concat("_").concat(i)
                        .concat("'></table></td></tr>");
                    tab.append(row);
                    if (item.Details.length !== 0) {
                        var id = "#".concat(tableSeed).concat("_").concat(i);
                        var cid = "#".concat(checkSeed).concat("_").concat(i);
                        var detTab = $(id);
                        comparer.BuildDetails(detTab, item.Details, idSeed, i);
                        $(cid).on("change", comparer.parentCheckChanged);
                    }
                } else {
                    var row = "<tr><td colspan='5'><span style='font-size:1.5rem;' class='fad fa-"
                        .concat(comparer.iconForType(item.ChangeType))
                        .concat("'></span>&nbsp;&nbsp;")
                        .concat(item.Key).concat("</td></tr>");
                    tab.append(row);
                }
            }
        },
        BuildDetails: function(table, details, itemSeed, rootIndex) {
            var checkSeed = "check_".concat(itemSeed).concat("_").concat(rootIndex);
            var txSeed = "tx_".concat(itemSeed).concat("_").concat(rootIndex);
            table.html("<colgroup>"
                .concat('<col style="width: 120px;" />')
                .concat('<col style="width: 50px;" />')
                .concat('<col style="width: 200px;" />')
                .concat('<col style="width: 200px;" />')
                .concat('</colgroup>')
                .concat('<thead class="k-gird-header">')
                .concat('<tr>')
                .concat('<th class="k-header">').concat(ITVenture.Text.getText("Json_Cfg_Name")).concat('</th>')
                .concat('<th class="k-header">').concat(ITVenture.Text.getText("Json_Cfg_Apply")).concat('</th>')
                .concat('<th class="k-header">').concat(ITVenture.Text.getText("Json_Cfg_Original_Value")).concat('</th>')
                .concat('<th class="k-header">').concat(ITVenture.Text.getText("Json_Cfg_New_Value")).concat('</th>')
                .concat('</tr>')
                .concat('</thead>')
                .concat('<tbody>')
                .concat('</tbody>"'));
            var id = table.attr("id");
            var body = $("#".concat(id).concat(">tbody"));
            for (var i = 0; i < details.length; i++) {
                var item = details[i];
                var row = "<tr>"
                    .concat("<td>").concat(item.Name).concat("</td>")
                    .concat("<td>").concat("<input type='checkbox' id='").concat(checkSeed).concat("_").concat(i)
                    .concat("'").concat(item.Apply ? " checked" : "").concat(" jccMode='dyn' jccRootId='")
                    .concat(rootIndex).concat("' jccDetailId='").concat(i).concat("' jccType='check' />")
                    .concat("</td>")
                    .concat("<td>")
                    .concat((typeof item.CurrentValue !== "undefined" && item.CurrentValue != null)
                        ? item.CurrentValue
                        : "").concat("</td>")
                    .concat("<td>").concat(!item.MultilineContent ? "<input id='" : "<textarea id='").concat(txSeed)
                    .concat("_").concat(i).concat("'").concat(" jccMode='dyn' jccRootId='").concat(rootIndex)
                    .concat("' jccDetailId='").concat(i).concat("' jccType='tx' style='width:100%;' />").concat("</td>")
                    .concat("</tr>");
                body.append(row);
                $("#".concat(txSeed).concat("_").concat(i)).val(item.NewValue);
            }
        },
        iconForType: function(typ) {
            switch (typ) {
            case 0:
                return "file-plus";
            case 1:
                return "file-edit";
            case 2:
                return "file-minus";
            case 3:
                return "info-circle";
            case 4:
                return "exclamation-triangle";
            }

            return "question";
        },
        parentCheckChanged: function() {
            var that = $(this);
            var id = that.attr("jccrootid");
            var deps = $("[jccrootid=".concat(id).concat("][jccdetailid][jcctype='check']"));
            console.log(deps);
            deps.each(function(i, e) {
                $(e).prop("checked", $(that).prop("checked"));
            });
        },
        ApplyConfigChanges: function() {
            var allControls = $("[jccMode='dyn']");
            allControls.each(function(i, e) {
                var ctl = $(e);
                var tp = ctl.attr("jcctype");
                var tid = parseInt(ctl.attr("jccrootid"));
                var cid = parseInt(ctl.attr("jccdetailid"));
                var att = tp === "check" ? "Apply" : "NewValue";
                var val = tp === "check" ? ctl.prop("checked") : ctl.val();
                var item = comparer.currentCompare[tid];
                if (!isNaN(cid)) {
                    item = item.Details[cid];
                }

                item[att] = val;

            });

            ITVenture.Ajax.ajaxFormPost(comparer.postTarget,
                JSON.stringify(comparer.enrichPostObj({
                    Changes: comparer.currentCompare
                }, comparer)),
                "text",
                "application/json").done(function(data) {

                if (data === "OK") {
                    ITVenture.Tools.Popup.Open("alert", ITVenture.Text.getText("Json_Cfg_Config_OK"));
                } else {
                    ITVenture.Tools.Popup.Open("alert", data);
                }
            }).fail(function() {
                ITVenture.Tools.Popup.Open("alert", ITVenture.Text.getText("Server-action failed!"));
            });
        }
    };

    return comparer;
}