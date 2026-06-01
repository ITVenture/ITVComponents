(function($){
    $.fn.loadPartial = function () {
        var options = null;
        if (arguments.length == 1 && typeof (arguments[0]) === "object") {
            options = arguments[0];
        }
        else {
            options = {
                replace: false, whenDone: null, extendViewSource: null
            };

            if (arguments.length > 0 && typeof (arguments[0]) === "boolean") {
                options.replace = arguments[0];
            }

            if (arguments.length > 1 && typeof (arguments[1]) === "function") {
                options.whenDone = arguments[1];
            }

            if (arguments.length > 2 && typeof (arguments[2]) === "function") {
                options.extendViewSource = arguments[2];
            }
        }

        if (typeof (options.extendViewSource) === "undefined") {
            options.extendViewSource = null;
        }

        if (typeof (options.whenDone) === "undefined") {
            options.whenDone = null;
        }

        if (typeof (options.replace) === "undefined") {
            options.replace = false;
        }

        $.each(this,
            function(index, item) {
                var that = $(item);
                var plug = {
                    target: that,
                    url: that.attr("viewSrc"),
                    extendViewSource: options.extendViewSource,
                    load: function () {
                        var url = plug.url;
                        var useReplace = options.replace;
                        if (typeof (plug.extendViewSource) === "function") {
                            url = plug.extendViewSource.apply(plug, [plug.url]);
                        }

                        if (arguments.length > 0 && typeof(arguments[0]) === "string" && arguments[0] != "") {
                            url = arguments[0];
                        }

                        if (arguments.length > 1 && typeof (arguments[1]) === "boolean") {
                            useReplace = arguments[1];
                        }

                        if (url != null && url != "") {
                            var dtype = "text";
                            if (typeof (options.dataType) === "string") {
                                dtype = options.dataType;
                            }

                            var request = {
                                url: ITVenture.Helpers.ResolveUrl(url),
                                type: "GET",
                                dataType: dtype,
                                success: function (data) {
                                    var proto = data;
                                    if (typeof (options.template) === "function") {
                                        proto = options.template.apply(plug, [data]);
                                    }

                                    if (useReplace) {
                                        plug.target.replaceWith(proto);
                                    } else {
                                        plug.target.html(proto);
                                    }

                                    if (typeof (options.whenDone) === "function") {
                                        options.whenDone();
                                    }
                                },
                                fail: function (data) {
                                    plug.target.html("<p>error loading view!</p>");
                                }
                            };

                            $.ajax(request);
                        }
                        else {
                            plug.target.html("<p>no source specified!</p>");
                        }
                    }
                };

                that.data("viewLoader", plug);
                var defer = false;
                if (that.attr("deferLoad") != null) {
                    defer = that.attr("deferLoad").toLowerCase() === "true";
                }

                if (plug.url == null || plug.url == "") {
                    defer = true;
                }

                if (!defer) {
                    plug.load();
                }
            });
        return this;
    }
}(jQuery));




//
// <div id="nameOfDiv" viewSrc="~/controller/action"></div>
// usage: $("#nameOfDiv").loadPartial();
//