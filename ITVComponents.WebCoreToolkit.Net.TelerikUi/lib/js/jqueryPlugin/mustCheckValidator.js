$(function ($) {
    $.validator.addMethod(
        'mustcheck',
        function (value, element, param) {
            return element.checked;
        },
        'Checkbox must be checked.'
    );
    $.validator.unobtrusive.adapters.add("mustcheck",
        ["condition"],
        function (options) {
            options.rules["mustcheck"] = {};
            options.messages['mustcheck'] = options.message;
        });
}(jQuery));