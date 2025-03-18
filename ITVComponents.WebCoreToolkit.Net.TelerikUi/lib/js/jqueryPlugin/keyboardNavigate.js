(function ($) {
    $.fn.keyboardNavigate = function (keyboardConfig, enabled, dbgPrint) {
        var deb = false;
        var enab = true;
        if (typeof (dbgPrint) === "boolean") {
            deb = dbgPrint;
        }

        if (typeof (enabled) === "boolean") {
            enab = enabled;
        }
        $.each(this,
            function (index, item) {
                var that = $(item);
                var plug = {
                    target: that,
                    keyboardConfig: keyboardConfig,
                    forDebug: deb,
                    enabled: enab
                };

                if (typeof (that.data("keyboardNavigate")) === "undefined") {
                    that.data("keyboardNavigate", plug);
                    plug.raiseEvent = function (identifiers, event) {
                        var handled = false;
                        $.each(identifiers, function (index, identifier) {
                            if (plug.keyboardConfig.hasOwnProperty(identifier) && typeof (plug.keyboardConfig[identifier]) === "function") {
                                var fx = plug.keyboardConfig[identifier];
                                fx.apply(plug, identifier, event);
                                handled = true;
                            }
                            else if (plug.forDebug) {
                                console.log(identifier);
                            }
                        });

                        if (!handled && plug.forDebug) {
                            console.log(event);
                        }
                    }
                    that.on("keydown", function (event) {
                        if (plug.enabled) {
                            var ori = event.originalEvent;
                            plug.raiseEvent([`down${(ori.shiftKey ? "+shift" : "")}${(ori.ctrlKey ? "+ctrl" : "")}${(ori.altKey ? "+alt" : "")}+${ori.code}`,
                            `down${(ori.shiftKey ? "+shift" : "")}${(ori.ctrlKey ? "+ctrl" : "")}${(ori.altKey ? "+alt" : "")}+${ori.key}`], event);
                        }
                    });

                    that.on("keyup", function (event) {
                        if (plug.enabled) {
                            var ori = event.originalEvent;
                            plug.raiseEvent([`up${(ori.shiftKey ? "+shift" : "")}${(ori.ctrlKey ? "+ctrl" : "")}${(ori.altKey ? "+alt" : "")}+${ori.code}`,
                            `up${(ori.shiftKey ? "+shift" : "")}${(ori.ctrlKey ? "+ctrl" : "")}${(ori.altKey ? "+alt" : "")}+${ori.key}`], event);
                        }
                    });

                    that.on("keypress", function (event) {
                        if (plug.enabled) {
                            var ori = event.originalEvent;
                            plug.raiseEvent([`press${(ori.shiftKey ? "+shift" : "")}${(ori.ctrlKey ? "+ctrl" : "")}${(ori.altKey ? "+alt" : "")}+${ori.code}`,
                            `press${(ori.shiftKey ? "+shift" : "")}${(ori.ctrlKey ? "+ctrl" : "")}${(ori.altKey ? "+alt" : "")}+${ori.key}`], event);
                        }
                    });
                }
                else {
                    existing = that.data("keyboardNavigator");
                    existing.keyboardConfig = keyboardConfig;
                    existing.forDebug = deb;
                    existing.enabled = enab;
                }
            });
        return this;
    }
}(jQuery));