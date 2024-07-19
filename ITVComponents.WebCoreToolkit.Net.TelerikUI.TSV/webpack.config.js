const glob = require("glob");
const TerserPlugin = require("terser-webpack-plugin");
const path = require("path");
const webpack = require('webpack');
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const CssMinimizerPlugin = require("css-minimizer-webpack-plugin");
var baseTools = glob.sync("./Lib/**/*.js").concat(glob.sync("./Areas/**/*.js"));
function exf(files) {
    for (var i = 0; i < files.length; i++) {
        files[i] = "./".concat(files[i]);
    }
}

exf(baseTools);

module.exports = [
    {
        mode:"production",
        output: {
            path: path.resolve(__dirname, "wwwroot/js"),
            filename: "[name].min.js"
        },
        entry: {
            viewScripts: baseTools
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