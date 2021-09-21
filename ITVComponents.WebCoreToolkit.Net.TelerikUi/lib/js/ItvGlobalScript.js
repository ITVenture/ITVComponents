kendo.culture("de-CH");

var ITVenture = {
    Lang: "de",
    Text: {
        setText: function (language, label, text) {
            var lng = language; //.toUpperCase();
            if (!ITVenture.Text.hasOwnProperty(lng)) {
                ITVenture.Text[lng] = {};
            }

            ITVenture.Text[lng][label] = text;
        },
        getText: function (label, defaultValueOrValues) {
            var lng = ITVenture.Lang; //.toUpperCase();
            if ((!ITVenture.Text.hasOwnProperty(lng) ||
                (ITVenture.Text.hasOwnProperty(lng) && !ITVenture.Text[lng].hasOwnProperty(label))) &&
                lng.indexOf("-") !== -1) {
                lng = lng.substr(0, lng.indexOf("-"));
            }
            if (ITVenture.Text.hasOwnProperty(lng)) {
                return ITVenture.Text.processMessage(label, ITVenture.Text[lng], defaultValueOrValues);
            }

            if (typeof (defaultValueOrValues) === "string") {
                return defaultValueOrValues;
            }

            return label;
        },
        getLocaleMessage: function (messageObject, messageArguments) {
            if (typeof (messageObject) === "string") {
                return ITVenture.Text.getText(messageObject, messageArguments);
            }
            else if (typeof (messageObject) === "object") {
                var lng = ITVenture.Lang; //.toUpperCase();
                if (!messageObject.hasOwnProperty(lng) && lng.indexOf("-") !== -1) {
                    lng = lng.substr(0, lng.indexOf("-"));
                }
                if (messageObject.hasOwnProperty(lng)) {
                    return ITVenture.Text.processMessage("-", messageObject[lng], messageArguments);
                }
            }

            return messageObject;
        },
        processMessage: function (label, messageSource, messageArguments) {
            var retVal = label;
            if (typeof (messageSource) === "object" && messageSource.hasOwnProperty(label)) {
                retVal = messageSource[label];
            }
            else if (typeof (messageSource) === "string") {
                retVal = messageSource;
            }
            if (typeof messageArguments === "object") {
                retVal = retVal.replaceAll(/\{\{\s*(?<paramName>(-\>|!$|$)?(\\\{|\\\}|[^\}\{])+)\s*\}\}/g,
                    function () {
                        try {
                            var p = arguments[arguments.length - 1];
                            var paramName = p.paramName.replace("\\{", "{").replace("\\}", "}");
                            if (!paramName.startsWith("->") &&
                                !paramName.startsWith("$") &&
                                messageArguments.hasOwnProperty(paramName)) {
                                return messageArguments[paramName];
                            } else if (paramName.startsWith("->")) {
                                return ITVenture.Text.getText(paramName.substring(2), messageArguments);
                            } else if (paramName.startsWith("!$")) {
                                var fx = new Function("return ".concat(paramName.substring(2)).concat(";"));
                                return fx.apply(messageArguments);
                            } else if (paramName.startsWith("$")) {
                                var fx = new Function(paramName.substring(1));
                                return fx.apply(messageArguments);
                            }

                            return paramName;

                        }
                        catch (e) {
                            return "";
                        }
                    });
            }

            return retVal;
        }
    },
    Tools: {},
    Pages: {},
    Helpers: {
        htmlEntities: function (raw) {
            return raw.replace(/[&<>'\"]/g,
                tag => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[tag]));
        },
        serializeArray: function (object, keys) {
            var retVal = {};
            if (typeof (keys) === "undefined" || typeof (keys) === "function") {
                for (var key in object) {
                    if (object.hasOwnProperty(key) &&
                        typeof object[key] !== "function" &&
                        (key.substring(0, 1) !== "_") &&
                        key !== "dirty" &&
                        (typeof (keys) === "undefined" || keys(key))) {
                        var val = object[key];
                        if (val instanceof Date) {
                            val = val.toJSON();
                        }

                        retVal[key] = val;
                    }
                }
            }
            else if (Array.isArray(keys)) {
                for (i = 0; i < keys.length; i++) {
                    if (object.hasOwnProperty(keys[i])) {
                        retVal[keys[i]] = object[keys[i]];
                    }
                }
            }

            return retVal;
        },
        serializeItemArray: function (arr, keys) {
            var retVal = [];
            for (var i = 0; i < arr.length; i++) {
                retVal.push(ITVenture.Helpers.serializeArray(arr[i], keys));
            }

            return retVal;
        },
        isNumber: function (n) {
            return !isNaN(parseFloat(n)) && isFinite(n);
        },
        dummyProc: function () {
            alert("horndampf");
        },
        getCurrentEditedModel: function (gridName) {
            var grid = $("#" + gridName).data("kendoGrid");
            var editRow = grid.tbody.find("tr[class='k-grid-edit-row']");
            return grid.dataItem(editRow);
        },
        ResolveUrl: function (url) {
            if (typeof url !== "undefined" && url !== null) {
                if (url.indexOf("~/") === 0) {
                    url = ITVenture.Ajax.baseUrl + url.substring(2);
                }

                return url;
            }

            return null;
        },
        GetControllerName: function () {
            var fullUrl = window.location.pathname;
            var firstIndex = ITVenture.Ajax.baseUrl.length;
            var tmp = fullUrl.substring(firstIndex);
            var lastIndex = tmp.indexOf("/");
            var ret = tmp;
            if (lastIndex !== -1) {
                if (lastIndex !== -1) {
                    ret = tmp.substring(0, lastIndex);
                }
            }

            return ret;
        },
        listErrorHandler: function (e) {
            var rollbackGrid = false;
            var editError = false;
            if (e.errors) {
                var message = "";

                $.each(e.errors,
                    function (key, value) {
                        rollbackGrid |= (key === "Error");
                        editError |= (key === "EdError");
                        if ('errors' in value) {
                            $.each(value.errors,
                                function () {
                                    message = this + "\r\n";
                                });
                        }

                    });

                console.log(e.sender);

                if (rollbackGrid && !editError) {
                    ITVenture.Tools.Popup.Open("alert", message);
                    this.cancelChanges();
                } else if (!editError) {
                    ITVenture.Tools.Popup.Open("alert", message);
                    this.cancelChanges();
                    this.read();
                } else {
                    ITVenture.Tools.Popup.Open("alert", message);
                }
            }
        },
        makeListErrorHandler: function (gridName) {
            var retVal = function (e) {
                var rollbackGrid = false;
                var editError = false;
                if (e.errors) {
                    var message = "";

                    $.each(e.errors,
                        function (key, value) {
                            rollbackGrid |= (key === "Error");
                            editError |= (key === "EdError");
                            if ('errors' in value) {
                                $.each(value.errors,
                                    function () {
                                        message = this + "\r\n";
                                    });
                            }

                        });

                    console.log(e.sender);

                    if (rollbackGrid && !editError) {
                        ITVenture.Tools.Popup.Open("alert", message);
                        this.cancelChanges();
                    } else if (!editError) {
                        ITVenture.Tools.Popup.Open("alert", message);
                        this.cancelChanges();
                        this.read();
                    } else {
                        var grid = $("#" + retVal.gridName).data("kendoGrid");
                        grid.one("dataBinding",
                            function (args) {
                                args.preventDefault();
                            });
                        ITVenture.Tools.Popup.Open("alert", message);
                    }
                }
                else if (e.errorThrown) {
                    ITVenture.Tools.Popup.Open("alert",
                        ITVenture.Text.getText("ITVHelpers_generatedLEH_FAIL", "The Server reported an error. The command may have been processed incompletly."));
                    this.cancelChanges();
                    this.read();
                }
            };

            retVal.gridName = gridName;
            return retVal;
        }
    },
    Ajax: {
        baseUrl: "/",
        ajaxGet: function (url, expectedType) {
            if (typeof expectedType === "undefined" || expectedType === null) {
                expectedType = "json";
            }

            var request = {
                url: ITVenture.Helpers.ResolveUrl(url),
                type: "GET",
                dataType: expectedType
            };

            return $.ajax(request);
        },
        ajaxPost: function (url, data, expectedType, contentType) {
            if (typeof expectedType === "undefined" || expectedType === null) {
                expectedType = "json";
            }
            var request = {
                url: ITVenture.Helpers.ResolveUrl(url),
                type: "POST",
                dataType: expectedType,
                data: { data: data }
            };

            if (typeof contentType !== "undefined" && contentType !== null) {
                request.contentType = contentType;
            }

            return $.ajax(request);
        },
        ajaxFormPost: function (url, data, expectedType, contentType) {
            if (typeof expectedType === "undefined" || expectedType === null) {
                expectedType = "json";
            }
            var request = {
                url: ITVenture.Helpers.ResolveUrl(url),
                type: "POST",
                dataType: expectedType,
                data: data
            };

            if (typeof contentType !== "undefined" && contentType !== null) {
                request.contentType = contentType;
            }

            return $.ajax(request);
        },
        defaultAjaxErrorHandler: function (e) {
            var message = e.statusText;
            if (typeof e.status !== "undefined") {
                message += "(" + e.status + ")";
            }
            ITVenture.Tools.Popup.Open("alert", message);
        }
    }
};