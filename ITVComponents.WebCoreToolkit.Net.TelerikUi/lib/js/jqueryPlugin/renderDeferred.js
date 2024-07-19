(function($){
    $.fn.renderDeferred = function (renderMethod, status, groupIdentifier) {
        var statusObj = status;
        if (typeof (status) === "string" && status.startsWith("$$")){
            statusObj = ITVenture.Tools.RenderingDeferrer.getStatusObject(status.substring(2));
        }
        $.each(this,
            function (index, item) {
                var that = $(item);
                var plug = {
                    target: that,
                    status: statusObj,
                    triggered:false,
                    load: renderMethod
                };

                that.data("deferredRenderer", plug);
                plug.invokeRender = function () {
                    if (!plug.triggered) {
                        return plug.load.apply(plug, [that, plug.status]);
                    }

                    return null;
                }

                if (typeof (groupIdentifier) === "string" && groupIdentifier !== "") {
                    ITVenture.Tools.RenderingDeferrer.registerHandler(groupIdentifier, plug);
                }
            });
        return this;
    }
}(jQuery));




//
// <div id="nameOfDiv" viewSrc="~/controller/action"></div>
// usage: $("#nameOfDiv").loadPartial();
//