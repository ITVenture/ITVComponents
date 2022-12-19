ITVenture.Tools.Popup = {
    dialogs: {},
    initialize: function (name) {
        if (typeof ITVenture.Tools.Popup.dialogs[name] === 'undefined' || ITVenture.Tools.Popup.dialogs[name] === null) {
            var obj =
            {
                refObj: null,
                acceptCallbacks: [],
                showCallbacks: [],
                cancelCallbacks: [],
                closedCallbacks: [],
                window: $("#" + name)
            };
            obj = ITVenture.Tools.Popup.EnrichWindow(obj);

            ITVenture.Tools.Popup.dialogs[name] = obj;
        }

        return ITVenture.Tools.Popup.dialogs[name];
    },
    FindDialog: function (element) {
        return $(element).closest(".itv-dialog");
    },
    Open: function (name, refObj, success, cancel) {
        ITVenture.Tools.Popup.dialogs[name].Open(refObj, success, cancel);
    },
    OpenAsync: function (name, refObj) {
        var prom = new Promise(function (resolve, reject) {
            ITVenture.Tools.Popup.Open(name,
                refObj,
                function (window, dialog, ro, args) {
                    resolve({
                        accepted: true,
                        window: window,
                        dialog: dialog,
                        refObj: ro,
                        customArg: args
                    });
                },
                function (window, dialog, ro) {
                    resolve({
                        accepted: false,
                        window: window,
                        dialog: dialog,
                        refObj: ro,
                        customArg: []
                    });
                });
        });

        return prom;

    },
    Close: function (name) {
        ITVenture.Tools.Popup.dialogs[name].Close.apply(this, Array.prototype.slice.call(arguments, 1));
    },
    Accept: function (name) {
        ITVenture.Tools.Popup.dialogs[name].Accept.apply(this, Array.prototype.slice.call(arguments, 1));
    },
    EnrichWindow: function (obj) {
        var tmo = {
            refObj: null,
            acceptCallbacks: [],
            showCallbacks: [],
            cancelCallbacks: [],
            closedCallbacks: [],
            activatedCallbacks: [],
            pressedEnterCallbacks: [],
            handleReturn: false
        };
        obj = $.extend(true, tmo, obj);
        obj.dialog = function () { return obj.window.data("kendoWindow"); };
        obj.window.data("itvDialog", obj);
        obj.window.addClass("itv-dialog");
        obj.window.on("keydown",
            function (event) {
                if (event.key === "Enter" && obj.handleReturn) {
                    obj.PressedEnter(event);
                }
            });
        obj.dialog().bind("close",
            function () {
                try {
                    var i;
                    if (!obj.accepted) {
                        for (i = 0; i < obj.cancelCallbacks.length; i++) {
                            obj.cancelCallbacks[i](obj.window, obj.dialog(), obj.refObj);
                        }
                    }

                    for (i = 0; i < obj.closedCallbacks.length; i++) {
                        obj.closedCallbacks[i](obj.window, obj.dialog(), obj.refObj);
                    }
                } catch (x) {
                    console.log(x);
                }
                obj.dialog().restore();
                obj.refObj = null;
                $(document).scrollTop(obj.scrollTop);
            });
        obj.dialog().bind("activate",
            function () {
                for (var i = 0; i < obj.activatedCallbacks.length; i++) {
                    obj.activatedCallbacks[i](obj.window, obj.dialog(), obj.refObj);
                }

            });
        obj.accepted = false;
        obj.Close = function (accepted) {
            obj.accepted = accepted;
            obj.dialog().close();
        };

        obj.Accept = function () {
            var ok = true;
            for (var i = 0; i < obj.acceptCallbacks.length; i++) {
                var tmp = obj.acceptCallbacks[i](obj.window, obj.dialog(), obj.refObj, arguments);
                if (typeof tmp === "boolean") {
                    ok &= tmp;
                }
            }

            if (ok) {
                var arr = Array.prototype.slice.call(arguments, 0);
                arr.unshift(true);
                obj.Close.apply(this, arr);
            }
        };

        obj.Open = function (refObj, success, cancel) {
            if (typeof (success) === "function") {
                obj.onAccept(success);
                if (typeof (cancel) === "function") {
                    obj.onCancel(cancel);
                }

                obj.closedCallbacks = [
                    function () {
                        obj.acceptCallbacks = [];
                        obj.cancelCallbacks = [];
                        obj.closedCallbacks = [];
                    }
                ];
            }

            obj.refObj = refObj;
            for (var i = 0; i < obj.showCallbacks.length; i++) {
                obj.showCallbacks[i](obj.window, obj.dialog(), obj.refObj);
            }

            obj.scrollTop = $(document).scrollTop();
            obj.dialog().open().center();
        };

        obj.PressedEnter = function (event) {
            for (var i = 0; i < obj.pressedEnterCallbacks.length; i++) {
                obj.pressedEnterCallbacks[i].apply(obj, [event]);
            }
        };

        obj.onAccept = function (method) {
            if (typeof (method) === 'function') {
                obj.acceptCallbacks.push(method);
            }
        };

        obj.onShow = function (method) {
            if (typeof (method) === 'function') {
                obj.showCallbacks.push(method);
            }
        };

        obj.onCancel = function (method) {
            if (typeof (method) === 'function') {
                obj.cancelCallbacks.push(method);
            }
        };

        obj.onClosed = function (method) {
            if (typeof (method) === 'function') {
                obj.closedCallbacks.push(method);
            }
        };

        obj.onActivate = function (method) {
            if (typeof (method) === 'function') {
                obj.activatedCallbacks.push(method);
            }
        }

        obj.onPressedEnter = function (method) {
            if (typeof (method) === 'function') {
                obj.pressedEnterCallbacks.push(method);
            }
        }

        return obj;
    },
    createConfirmPopup: function () {
        var obj = {
            refObj: null,
            acceptCallbacks: [],
            showCallbacks: [],
            cancelCallbacks: [],
            closedCallbacks: [],
            window: $('<div class="k-edit-form-container">' +
                '<div id="AlertMessage" class="span10"></div>' +
                '<div class="k-edit-buttons k-state-default">' +
                '<a class="k-button k-button-icontext k-primary k-grid-update" href="#" onclick="ITVenture.Tools.Popup.Accept(\'confirm\')"><span class="k-icon k-update" ></span>' + ITVenture.Text.getText("Popup_General_OK", "Ok") + '</a>' +
                '<a class="k-button k-button-icontext k-grid-cancel" href="#" onclick="ITVenture.Tools.Popup.Close(\'confirm\')"><span class="k-icon k-cancel" ></span>' + ITVenture.Text.getText("Popup_General_Cancel", "Cancel") + '</a>' +
                '</div>' +
                '</div>'),
            handleReturn: true
        };
        obj.window.kendoWindow({
            visible: false,
            title: ITVenture.Text.getText("Popup_Confirm_Title", "Confirmation"),
            minWidth: 400,
            minHeight: 110,
            modal: true,
            scrollable: false,
            position: {
                top: "30%",
                left: "30%"
            }
        });
        obj = ITVenture.Tools.Popup.EnrichWindow(obj);
        obj.onPressedEnter(function (event) {
            if (!$(event.target).hasClass("k-button")) {
                event.preventDefault();
                ITVenture.Tools.Popup.Accept("confirm");
            }
        });
        obj.onShow(function (window, dialog, refObj) {
            obj.window.children("[id='AlertMessage']").html(refObj);
        });

        var oldOpen = obj.Open;
        obj.Open = function (refObj, success, cancel) {
            obj.onAccept(success);
            obj.onCancel(cancel);
            obj.closedCallbacks = [
                function () {
                    obj.acceptCallbacks = [];
                    obj.cancelCallbacks = [];
                    obj.closedCallbacks = [];
                }
            ];

            obj.Open.oldOpen(refObj);
        };
        obj.Open.oldOpen = oldOpen;
        return obj;
    },
    createAlertPopup: function () {
        var obj = {
            refObj: null,
            acceptCallbacks: [],
            showCallbacks: [],
            cancelCallbacks: [],
            closedCallbacks: [],
            window: $('<div class="k-edit-form-container d-flex flex-column align-items-fill">' +
                '<div id="AlertMessage"></div>' +
                '<div tag="buttons" class="k-edit-buttons k-state-default mt-auto p-2">' +
                '</div>' +
                '</div>'),
            defaultButton: '<a class="k-button k-button-icontext k-primary k-grid-update" href="#" onclick="ITVenture.Tools.Popup.Accept(\'alert\')"><span class="k-icon k-update" ></span>' + ITVenture.Text.getText("Popup_General_OK", "Ok") + '</a>'
        };
        obj.window.kendoWindow({
            visible: false,
            title: ITVenture.Text.getText("Popup_Alert_Title", "Information"),
            minWidth: 400,
            minHeight: 110,
            modal: true,
            scrollable: false,
            position: {
                top: "30%",
                left: "30%"
            }
        });
        obj = ITVenture.Tools.Popup.EnrichWindow(obj);
        obj.onShow(function (window, dialog, refObj) {
            //obj.window.children("[id='AlertMessage']").html(refObj);
            if (typeof refObj === "string") {
                obj.window.children("[id='AlertMessage']").html(refObj);
                obj.window.children("[tag='buttons']").html(obj.defaultButton);
            } else {
                obj.window.children("[id='AlertMessage']").html(refObj.Message);
                var buttonArea = "";
                $.each(refObj.Buttons,
                    function (id, item) {
                        buttonArea +=
                            '<a class="k-button k-button-icontext ' +
                            (item.Default ? 'k-primary k-grid-update' : '') +
                            '" href="#" onclick="ITVenture.Tools.Popup.' +
                            item.Action +
                            '(\'alert\',\'' +
                            item.ActionArgument +
                            '\')"><span class="' +
                            item.SpanClass +
                            '" ></span>' +
                            item.Text +
                            '</a>';
                    });
                obj.window.children("[tag='buttons']").html(buttonArea);
            }
        });

        var oldOpen = obj.Open;
        obj.Open = function (refObj, success, cancel) {
            obj.onAccept(success);
            obj.onCancel(cancel);
            obj.closedCallbacks = [
                function () {
                    obj.acceptCallbacks = [];
                    obj.cancelCallbacks = [];
                    obj.closedCallbacks = [];
                }
            ];

            obj.Open.oldOpen(refObj);
        };
        obj.Open.oldOpen = oldOpen;
        return obj;
    },
    createInputPopup: function () {
        var obj = {
            refObj: null,
            acceptCallbacks: [],
            showCallbacks: [],
            cancelCallbacks: [],
            closedCallbacks: [],
            window: $('<div class="k-edit-form-container d-flex flex-column align-items-fill">' +
                '<div id="AlertMessage"></div>' +
                '<div tag="cip-Content" class="flex-row d-flex"></div>' +
                '<div class="k-edit-buttons k-state-default mt-auto p-2">' +
                '<a class="k-button k-button-icontext k-primary k-grid-update" href="#" onclick="ITVenture.Tools.Popup.Accept(\'input\')"><span class="k-icon k-update" ></span>' + ITVenture.Text.getText("Popup_General_OK", "Ok") + '</a>' +
                '<a class="k-button k-button-icontext k-grid-cancel" href="#" onclick="ITVenture.Tools.Popup.Close(\'input\')"><span class="k-icon k-cancel" ></span>' + ITVenture.Text.getText("Popup_General_Cancel", "Cancel") + '</a>' +
                '</div>' +
                '</div>'),
            handleReturn: true
        };
        obj.window.kendoWindow({
            visible: false,
            title: ITVenture.Text.getText("Popup_Input_Title", "Input"),
            minWidth: 400,
            minHeight: 140,
            modal: true,
            scrollable: false,
            position: {
                top: "30%",
                left: "30%"
            }
        });
        obj = ITVenture.Tools.Popup.EnrichWindow(obj);
        obj.onPressedEnter(function (event) {
            if (!$(event.target).hasClass("k-button")) {
                event.preventDefault();
                ITVenture.Tools.Popup.Accept("input");
            }
        });

        obj.onShow(function (window, dialog, refObj) {
            var inputArea = '<input id="UserInput" class="flex-fill k-textbox" />';
            obj.mode = "default";
            if (typeof refObj === "string") {
                obj.window.children("[id='AlertMessage']").html(refObj);
            } else {
                obj.window.children("[id='AlertMessage']").html(refObj.Message);
                if (typeof refObj.Controls !== "undefined") {
                    inputArea = refObj.Controls;
                    obj.mode = "custom";
                } else {
                    obj.window.find("[id='UserInput']").val(refObj.Default);
                }
            }
            obj.window.children("[tag='cip-Content']").html(inputArea);
            //obj.window.children("[id='AlertMessage']").html(refObj);
        });

        var oldOpen = obj.Open;
        obj.Open = function (refObj, success, cancel) {
            obj.onAccept(function (window, dialog, refObj) {
                if (obj.mode === "default") {
                    success(window, dialog, refObj, window.find("#UserInput").val());
                } else {
                    success(window, dialog, refObj, obj.window.children("[tag='cip-Content']").children());
                }
            });
            obj.onCancel(cancel);
            obj.closedCallbacks = [
                function () {
                    obj.acceptCallbacks = [];
                    obj.cancelCallbacks = [];
                    obj.closedCallbacks = [];
                    obj.window.children("[tag='cip-Content']").html("");
                }
            ];

            obj.Open.oldOpen(refObj);
            obj.window.find("#UserInput").focus();
        };
        obj.onActivate(function () {
            obj.window.find("#UserInput").focus();
        });
        obj.Open.oldOpen = oldOpen;
        return obj;
    },
    createPdfPopup: function () {
        var obj = {
            refObj: null,
            acceptCallbacks: [],
            showCallbacks: [],
            cancelCallbacks: [],
            closedCallbacks: [],
            window: $('<div id="viewhoster" width="100%" height="100%"></div>')
        };
        obj.window.kendoWindow({
            visible: false,
            title: ITVenture.Text.getText("Popup_PdfView_Title", "PdfView"),
            minWidth: 1000,
            minHeight: 500,
            width: 1000,
            height: 500,
            modal: true,
            scrollable: false,
            position: {
                top: "30%",
                left: "30%"
            }
        });
        obj = ITVenture.Tools.Popup.EnrichWindow(obj);
        obj.onShow(function (window, dialog, refObj) {
            var url = refObj;
            var title = null;
            if (typeof (refObj) === "object" && refObj.hasOwnProperty("url")) {
                url = refObj.url;
                if (refObj.hasOwnProperty("title") && typeof (refObj.title) !== "undefined" && refObj.title !== null && refObj.title !== "") {
                    title = refObj.title;
                }
            }
            if (PDFObject) {
                PDFObject.embed(url, obj.window);
            } else {
                obj.window.html(/*'<object data="' + refObj + '" id="viewer" type="application/pdf" width="99%" height="99%">' +*/
                    //"<div>" +
                    '<embed src="' +
                    url +
                    '" id="emViewer" type="application/pdf" width="100%" height= "100%" />' //+
                    /*'</object>'*/);
            }

            if (title !== null) {
                dialog.title(title);
            }
        });

        obj.onClosed(function (win, dialog, refObj) {
            obj.window.empty();
        });

        return obj;
    },
    createGenericPopup: function (rootTagName, windowDef) {
        if (typeof (rootTagName) === "undefined" || rootTagName === null || rootTagName === "") {
            rootTagName = "genericPopupRoot";
        }
        if (typeof (windowDef) !== "object") {
            windowDef = {
                visible: false,
                title: "Generic Popup",
                minWidth: 300,
                minHeight: 100,
                modal: true,
                position: {
                    top: "30%",
                    left: "30%"
                }
            };
        }

        var markup = '<div id="'.concat(rootTagName).concat('" width="100%" height="100%"></div>');
        var obj = {
            refObj: null,
            acceptCallbacks: [],
            showCallbacks: [],
            cancelCallbacks: [],
            closedCallbacks: [],
            window: $(markup)
        };
        var dlgObj = obj.window.kendoWindow(windowDef);
        var dg = dlgObj.data("kendoWindow");
        obj = ITVenture.Tools.Popup.EnrichWindow(obj);
        var oldOpen = obj.Open;
        obj.Open = function (refObj, success, cancel) {
            obj.onAccept(success);
            obj.onCancel(cancel);
            obj.closedCallbacks = [
                function () {
                    var dlg = obj;
                    $(dlg.window).html("");
                    if (typeof (refObj.dialogClosing) === "function") {
                        refObj.dialogClosing(obj, dg);
                    }
                    obj.acceptCallbacks = [];
                    obj.cancelCallbacks = [];
                    obj.closedCallbacks = [];
                }
            ];
            obj.Open.oldOpen(refObj);
            if (typeof (refObj) === "object") {
                if (typeof (refObj.dialogOpening) === "function") {
                    refObj.dialogOpening(obj, dg);
                }
            }
        };

        obj.Open.oldOpen = oldOpen;

        obj.onShow(function (window, dialog, refObj) {
            if (typeof (refObj) === "object") {
                if (typeof (refObj.contentUrl) === "string") {
                    ITVenture.Ajax.ajaxGet(refObj.contentUrl, "text").done(
                        function (data) {
                            $(obj.window).html(data);
                            if (typeof (refObj.contentReady) === "function") {
                                refObj.contentReady(window, dialog);
                            }
                        }).fail(function (err) {
                            if (typeof (refObj.contentFailed) === "function") {
                                refObj.contentFailed(window, dialog, err);
                            }
                        });
                } else if (typeof (refObj.rawContent) === "string") {
                    $(obj.window).html(refObj.rawContent);
                    if (typeof (refObj.contentReady) === "function") {
                        refObj.contentReady(window, dialog);
                    }
                } else {
                    throw "property contentUrl expected!";
                }
            } else {
                throw "object expected!";
            }
        });

        return obj;
    }
}

$(document).ready(function () {
    ITVenture.Tools.Popup.dialogs.alert = ITVenture.Tools.Popup.createAlertPopup();
    ITVenture.Tools.Popup.dialogs.confirm = ITVenture.Tools.Popup.createConfirmPopup();
    ITVenture.Tools.Popup.dialogs.input = ITVenture.Tools.Popup.createInputPopup();
    ITVenture.Tools.Popup.dialogs.pdf = ITVenture.Tools.Popup.createPdfPopup();
    ITVenture.Tools.Popup.dialogs.generic = ITVenture.Tools.Popup.createGenericPopup();
});