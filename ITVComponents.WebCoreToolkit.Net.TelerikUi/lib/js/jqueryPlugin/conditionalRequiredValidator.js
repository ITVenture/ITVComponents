$(function ($) {
    $.validator.addMethod(
        'requiredif',
        function (value, element, param) {
            var retVal = true;
            var cnd = param.condition;
            if (typeof (cnd) === "string") {
                cnd = eval(cnd);
            } 

            if (typeof (cnd) === "function") {
                retVal = !cnd();
                if (!retVal) {
                    retVal = value != null && value !== "";
                }
            }

            return retVal;
        },
        'This value is currently required.'
    );
    $.validator.unobtrusive.adapters.add("requiredif",
        ["condition"],
        function (options) {
            options.rules["requiredif"] = {
                condition: options.params["condition"]
            };
            options.messages['requiredif'] = options.message;
        });
}(jQuery));