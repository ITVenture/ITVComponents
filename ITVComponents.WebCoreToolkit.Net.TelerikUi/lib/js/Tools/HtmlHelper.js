ITVenture.Tools.HtmlHelper = {
    encoder: null,
    encode: function(text) {
        if (ITVenture.Tools.HtmlHelper.encoder !== null) {
            return ITVenture.Tools.HtmlHelper.encoder.encode(text);
        }

        return text;
    }
};

$(document).ready(function() {
    if (!he) {
        console.log("Please reference He.js to make Html-Encoding work!");
    } else {
        ITVenture.Tools.HtmlHelper.encoder = he;
    }
});