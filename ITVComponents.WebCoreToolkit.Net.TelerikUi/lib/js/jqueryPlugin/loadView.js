(function($){
    $.fn.loadPartial = function(replace, whenDone, extendViewSource) {
        $.each(this,
            function(index, item) {
                var that = $(item);
                var plug = {
                    target: that,
                    url: that.attr("viewSrc"),
                    extendViewSource: extendViewSource,
                    load: function () {
                        var url = plug.url;
                        var useReplace = replace;
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
                            var request = {
                                url: ITVenture.Helpers.ResolveUrl(url),
                                type: "GET",
                                dataType: "text",
                                success: function (data) {
                                    if (useReplace) {
                                        plug.target.replaceWith(data);
                                    } else {
                                        plug.target.html(data);
                                    }

                                    if (typeof (whenDone) === "function") {
                                        whenDone();
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