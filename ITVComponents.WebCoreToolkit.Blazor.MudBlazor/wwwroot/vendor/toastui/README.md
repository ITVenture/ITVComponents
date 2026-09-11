# TOAST UI Editor 3.2.2 (vendored)

Quelle: <https://github.com/nhn/tui.editor> - Lizenz: **MIT** (NHN Cloud FE Development Lab)

| Datei | Herkunft |
|---|---|
| `toastui-editor.min.js` | **`https://uicdn.toast.com/editor/3.2.2/toastui-editor-all.min.js`** (NHN-CDN) |
| `toastui-editor.min.css` | jsDelivr, `@toast-ui/editor@3.2.2/dist/toastui-editor.css`, minifiziert |
| `toastui-editor-dark.min.css` | jsDelivr, `dist/theme/toastui-editor-dark.css`, minifiziert |
| `i18n/de-de.js`, `i18n/fr-fr.js`, `i18n/it-it.js` | jsDelivr, `dist/i18n/*` |

**Warum das JS vom NHN-CDN und nicht aus dem npm-Paket:** `dist/toastui-editor.js` aus npm ist
**nicht eigenstaendig** - es erklaert die acht `prosemirror-*`-Pakete zu Externals und bekommt sie im
Browser-Zweig als `undefined` gereicht (`e.toastui.Editor = t(e[void 0], ...)`). Es laedt dann zwar,
scheitert aber beim Aufbau des Editors. Die Fassung `toastui-editor-all.min.js` vom Hersteller-CDN
buendelt ProseMirror mit (`define([], t)` / `t()` ohne Argumente) und ist die, die man ohne
Bau-Umgebung braucht. Im npm-Paket gibt es sie nicht.

Die Sprachdateien greifen im Browser-Zweig auf `root["toastui"]["Editor"]` zu - sie muessen also
**nach** dem Bundle geladen werden; `markdown-editor.js` haelt diese Reihenfolge ein.

**Warum hier abgelegt und nicht vom CDN geladen:** das Toolkit hat keine JavaScript-Bauumgebung
(kein `package.json`), und eine Fachanwendung soll ihre Oberflaeche nicht von einem fremden Host
abhaengig machen. Die Dateien werden unveraendert ausgeliefert.

**Warum diese Version:** 3.2.2 ist der letzte Stand des Pakets (Februar 2023). Der Editor ist
funktional vollstaendig; ein Versionssprung ist nur noetig, wenn die Fassung eine Sicherheitsluecke
bekommt - dann die vier Dateien neu ziehen und diese Tabelle nachfuehren.

**Wer das hier benutzt:** ausschliesslich `wwwroot/markdown-editor.js` und
`SharedComponents/MarkdownEditor.razor` desselben Pakets. Das Bundle haengt **nicht** in der
Host-Seite - es wird beim ersten Aufbau eines Editors nachgeladen (rund 700 KB js+css), damit Seiten
ohne Markdown-Feld nichts davon zahlen.
