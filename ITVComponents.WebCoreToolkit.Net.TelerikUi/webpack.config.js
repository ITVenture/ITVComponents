const glob = require("glob");
const TerserPlugin = require("terser-webpack-plugin");
const path = require("path");
const webpack = require('webpack');
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const CssMinimizerPlugin = require("css-minimizer-webpack-plugin");
var baseTools = ["lib/js/bundler/itvComponents.js"];
baseTools = [...new Set(baseTools)];
var plugs = glob.sync("./lib/js/jqueryPlugin/*.js");
var cssInput = glob.sync("./lib/styles/*.css");
function exf(files) {
    for (var i = 0; i < files.length; i++) {
        files[i] = "./".concat(files[i]);
    }
}

exf(baseTools);
exf(plugs);
exf(cssInput);

module.exports = [
    {
        mode:"production",
        output: {
            path: path.resolve(__dirname, "wwwroot/js"),
            filename: "[name].min.js"
        },
        entry: {
            itvComponents: baseTools,
            itvJqPlugs: plugs
        },
        optimization: {
            minimize: true,
            minimizer: [new TerserPlugin()]
        },
        devtool: 'source-map',
        externals: {
            jquery: 'jQuery',
            kendo: 'kendo',
            dropzone: 'Dropzone'
        }
    },
    {
        mode: "production",
        output: {
            path: path.resolve(__dirname, "wwwroot/css"),
            filename: "[name].js"
        },
        entry: {
            itvComponentsBS4: cssInput
        },
        optimization: {
            minimizer: [new CssMinimizerPlugin({
                minify: CssMinimizerPlugin.cleanCssMinify
            })],
            minimize: true,
        },
        module: {
            rules: [
                {
                    test: /.css$/,
                    use: [MiniCssExtractPlugin.loader, "css-loader"/*, "sass-loader"*/],
                },
            ],
        },
        plugins: [new MiniCssExtractPlugin({
            filename: "[name].min.css"
        })],
    }
];