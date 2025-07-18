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
                        if (typeof (plug.extendViewSource) === "function") {
                            url = plug.extendViewSource.apply(plug, [plug.url]);
                        }
                        var request = {
                            url: ITVenture.Helpers.ResolveUrl(url),
                            type: "GET",
                            dataType: "text",
                            success: function(data) {
                                if (replace) {
                                    plug.target.replaceWith(data);
                                } else {
                                    plug.target.html(data);
                                }

                                if (typeof (whenDone) === "function") {
                                    whenDone();
                                }
                            },
                            fail: function(data) {
                                plug.target.html("<p>error loading view!</p>");
                            }
                        };

                        $.ajax(request);
                    }
                };

                that.data("viewLoader", plug);
                var defer = false;
                if (that.attr("deferLoad") != null) {
                    defer = that.attr("deferLoad").toLowerCase() === "true";
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