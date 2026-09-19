// Alles in dieser Datei wird gegen __dirname aufgeloest, nicht gegen das Verzeichnis, aus dem der
// Befehl gestartet wurde. Ohne "context" nimmt webpack process.cwd() - dann haengt das ERGEBNIS davon
// ab, von wo gebaut wird: die Quellpfade in den Source-Maps wurden absolut (mitsamt Benutzernamen und
// lokaler Verzeichnisstruktur), und die glob-Suche unten haette gar nichts mehr gefunden. Abgesichert
// war bisher nur output.path - darum landeten die Bundles weiterhin richtig, waehrend ihr INHALT
// verrutschte.
const glob = require("glob");
const TerserPlugin = require("terser-webpack-plugin");
const path = require("path");
const webpack = require('webpack');
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const CssMinimizerPlugin = require("css-minimizer-webpack-plugin");
var baseTools = ["lib/js/bundler/itvComponents.js"];
baseTools = [...new Set(baseTools)];
var plugs = glob.sync("./lib/js/jqueryPlugin/*.js", { cwd: __dirname });
var cssInput = glob.sync("./lib/styles/*.css", { cwd: __dirname });
// TenantSecurityViews (merged from former TSV package): view-scripts bundle
var viewScripts = glob.sync("./TenantSecurityViews/Lib/**/*.js", { cwd: __dirname })
    .concat(glob.sync("./Areas/**/*.js", { cwd: __dirname }));
function exf(files) {
    for (var i = 0; i < files.length; i++) {
        files[i] = "./".concat(files[i]);
    }
}

exf(baseTools);
exf(plugs);
exf(cssInput);
exf(viewScripts);

module.exports = [
    {
        mode:"production",
        context: __dirname,
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
        context: __dirname,
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
    },
    {
        mode: "production",
        context: __dirname,
        output: {
            path: path.resolve(__dirname, "wwwroot/js"),
            filename: "[name].min.js"
        },
        entry: {
            viewScripts: viewScripts
        },
        optimization: {
            minimize: true,
            minimizer: [new TerserPlugin()]
        },
        devtool: 'source-map'
    }
];