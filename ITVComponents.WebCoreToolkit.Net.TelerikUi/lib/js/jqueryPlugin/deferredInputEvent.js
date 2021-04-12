(function($){
    $.fn.deferredInputEvent = function(timeout, targetFunction) {
        $.each(this,
            function(index, item) {
                var that = $(item);
                var plug = {
                    target: that,
                    waiting: false,
                    timeout: timeout,
                    targetFunction : targetFunction,
                    handler: function() {
                        if (!plug.waiting) {
                            plug.waiting = true;
                            setTimeout(function() {
                                    plug.waiting = false;
                                    plug.targetFunction();
                                },
                                plug.timeout);
                        }
                    }
                };

                that.data("inputDeferrer", plug);
                that.keyup(plug.handler);
            });
        return this;
    }
}(jQuery));