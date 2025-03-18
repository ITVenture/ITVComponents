(function ($) {
    $.fn.getKendoButton = function () {
        var tkb = this;
        if (!tkb.hasClass("k-button")) {
            var tmp = tkb.closest(".k-button");
            if (tmp.length == 1) {
                tkb = tmp;
            }
        }

        return tkb;
    }
}(jQuery));

(function ($) {
    $.fn.isKendoButton = function () {
        var tkb = this.getKendoButton();
        return tkb.length != 0 && tkb.hasClass("k-button");
    }
}(jQuery));
