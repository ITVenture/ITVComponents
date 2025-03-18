const glob = require("glob");
const TerserPlugin = require("terser-webpack-plugin");
const path = require("path");
const webpack = require('webpack');
var files = glob.sync("./wwwroot/js/ViewScripts/**/*.js").concat(glob.sync("./Areas/**/*.js"));
for (var i = 0; i < files.length; i++) {
    files[i] = "./".concat(files[i]);
}

if (files.length > 0) {
    module.exports = [
        {
            mode: "production",
            output: {
                path: path.resolve(__dirname, "wwwroot/js"),
                filename: "ViewScripts.min.js"
            },
            entry: files,
            optimization: {
                minimize: true,
                minimizer: [new TerserPlugin()]
            }
        }
    ];
}
else {
    module.exports = [
        {
            mode: "none",
            entry: ["."],
            output: {}
        }
    ];
}