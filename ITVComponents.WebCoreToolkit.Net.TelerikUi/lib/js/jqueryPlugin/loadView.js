(function($){
    $.fn.loadPartial = function(replace, whenDone) {
        $.each(this,
            function(index, item) {
                var that = $(item);
                var plug = {
                    target: that,
                    url: that.attr("viewSrc"),
                    load: function() {
                        var request = {
                            url: ITVenture.Helpers.ResolveUrl(plug.url),
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
                plug.load();
            });
        return this;
    }
}(jQuery));




//
// <div id="nameOfDiv" viewSrc="~/controller/action"></div>
// usage: $("#nameOfDiv").loadPartial();
//