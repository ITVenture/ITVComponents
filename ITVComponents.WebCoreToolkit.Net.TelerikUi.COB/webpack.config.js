const glob = require("glob");
const TerserPlugin = require("terser-webpack-plugin");
const path = require("path");
const webpack = require('webpack');
var files = glob.sync("./wwwroot/js/ViewScripts/**/*.js").concat(glob.sync("./Areas/**/*.js"));
for (var i = 0; i < files.length; i++) {
    files[i] = "./".concat(files[i]);
}

module.exports = [
    {
        mode:"production",
        output: {
            path: path.resolve(__dirname, "wwwroot/js"),
            filename: "[name].min.js"
        },
        entry: {
            viewScripts: files
        },
        optimization: {
            minimize: true,
            minimizer: [new TerserPlugin()]
        },
        devtool: 'source-map',
        /*externals: {
            jquery: 'jQuery',
            kendo: 'kendo',
            dropzone: 'Dropzone'
        }*/
    }
];