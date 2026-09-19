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
var files = glob.sync("./wwwroot/js/ViewScripts/**/*.js", { cwd: __dirname })
    .concat(glob.sync("./Areas/**/*.js", { cwd: __dirname }));
for (var i = 0; i < files.length; i++) {
    files[i] = "./".concat(files[i]);
}

if (files.length > 0) {
    module.exports = [
        {
            mode: "production",
            context: __dirname,
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
            context: __dirname,
            entry: ["."],
            output: {}
        }
    ];
}