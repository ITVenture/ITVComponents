// Wirt fuer den TOAST UI Editor (wwwroot/vendor/toastui) - der Editor, der Markdown im WYSIWYG-Modus
// bearbeitet und trotzdem Markdown speichert. Angesprochen wird er ausschliesslich ueber
// SharedComponents/MarkdownEditor.razor.
//
// Fuenf Entscheidungen, die man hier sehen soll:
//
// 1. **Ein ES-Modul, kein Host-Skript.** Die Komponente holt es per `import()` beim ersten Aufbau
//    (derselbe Weg wie FileUpload/FileDownload nebenan). Damit waechst der Initial-Load einer
//    Anwendung durch diese Datei um NICHTS - wer nie ein Markdown-Feld oeffnet, laedt weder sie noch
//    den Editor dahinter. Ein Modul hat ausserdem seinen eigenen Geltungsbereich; der frueher noetige
//    IIFE-Mantel entfaellt.
// 2. **Der Editor selbst wird nachgeladen.** Das Bundle bringt rund 700 KB (js+css) mit und kommt
//    erst beim ersten init().
// 3. **UMD neben einem AMD-Loader.** Die Host-Seite bindet fuer den CodeEditor Monacos `loader.js`
//    ein, und der setzt ein globales `define` mit `define.amd`. Das TOAST-UI-Bundle prueft genau
//    darauf, bevor es `window.toastui` setzt - es meldete sich sonst bei Monacos Loader an und
//    `toastui` bliebe undefiniert. Deshalb wird `define` fuer die Dauer des Ladens weggenommen.
// 4. **Der Editor besitzt seinen Text.** Blazor bekommt ihn gebuendelt (DEBOUNCE_MS) und beim
//    Verlassen des Feldes, nicht bei jedem Tastendruck - unter Blazor Server waere das ein
//    Server-Roundtrip je Zeichen.
// 5. **`resource:`-Bilder werden nur fuer die ANZEIGE aufgeloest**, siehe fixImages().

const DEBOUNCE_MS = 600;

// Nur Werkzeuge, die auch etwas Sinnvolles tun. Das eingebaute Bild-Werkzeug fehlt bewusst: es laedt
// in keine Ablage hoch, sondern setzt eine blob:-Adresse, die nach dem Neuladen der Seite kaputt ist -
// ohne dass es beim Erfassen aufgefallen waere. An seiner Stelle steht der Ressourcen-Knopf (siehe
// buildPickerButtons), der in die Bibliothek greift.
const TOOLBAR = [
    ['heading', 'bold', 'italic', 'strike'],
    ['hr', 'quote'],
    ['ul', 'ol', 'indent', 'outdent'],
    ['table', 'link'],
    ['code', 'codeblock']
];

// Alle Editoren dieses Moduls, nach der Id ihres Wirtselements. Modul-Geltungsbereich: nach aussen
// sichtbar ist allein, was unten exportiert wird.
const editors = {};

let loadPromise = null;

function loadStylesheet(href) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`link[href="${href}"]`)) {
            resolve();
            return;
        }
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        link.onload = () => resolve();
        link.onerror = () => reject(new Error(`Stylesheet could not be loaded: ${href}`));
        document.head.appendChild(link);
    });
}

function loadScript(src) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`script[src="${src}"]`)) {
            resolve();
            return;
        }
        const script = document.createElement('script');
        script.src = src;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Script could not be loaded: ${src}`));
        document.head.appendChild(script);
    });
}

// Laedt ein UMD-Bundle so, dass es sich als GLOBAL registriert und nicht als AMD-Modul - siehe
// Punkt 3 im Kopf. Der bekannte Weg fuer UMD-Pakete neben einem AMD-Loader; die Alternative waere,
// den Editor ueber Monacos `require` zu ziehen - dann hinge er aber an fremder Ladeinfrastruktur.
function loadScriptAsGlobal(src) {
    const previousDefine = window.define;
    const hasAmd = typeof previousDefine === 'function' && previousDefine.amd;
    if (hasAmd) {
        window.define = undefined;
    }

    return loadScript(src).finally(() => {
        if (hasAmd) {
            window.define = previousDefine;
        }
    });
}

// assets = { css, darkCss, js, languageJs } - fertige Adressen aus der Komponente. Dieses Modul baut
// KEINE Pfade zusammen: im PathSegment-Modus setzt der Wirt ein <base href="/{tenant}/">, an dem ein
// selbstgebauter absoluter Pfad vorbeizielt.
async function ensureEditorLoaded(assets) {
    if (!loadPromise) {
        loadPromise = (async () => {
            await loadStylesheet(assets.css);
            if (assets.darkCss) {
                // Beide Themen immer laden: das dunkle greift nur unter .toastui-editor-dark, und so
                // kostet ein Themenwechsel zur Laufzeit keinen zweiten Ladevorgang.
                await loadStylesheet(assets.darkCss);
            }
            await loadScriptAsGlobal(assets.js);

            if (typeof window.toastui === 'undefined' || typeof window.toastui.Editor !== 'function') {
                // Geladen, aber nicht angekommen. Ohne diese Pruefung fiele es erst beim Aufbau als
                // ReferenceError auf, und die Meldung nennte nicht den Grund.
                throw new Error('The TOAST UI bundle was loaded but did not register as window.toastui '
                    + '(foreign AMD loader?).');
            }
        })();
    }

    try {
        await loadPromise;
    } catch (e) {
        // Ein gescheiterter Versuch darf den naechsten nicht auf ewig vergiften: das Versprechen
        // bliebe sonst als abgelehnt stehen, und jeder weitere Editor scheiterte mit, selbst wenn die
        // Ursache behoben waere.
        loadPromise = null;
        throw e;
    }

    if (assets.languageJs) {
        try {
            // Auch die Sprachdateien sind UMD - dasselbe Spiel mit `define`. Sie greifen ausserdem auf
            // root["toastui"]["Editor"] zu, muessen also NACH dem Bundle kommen; das tun sie hier.
            await loadScriptAsGlobal(assets.languageJs);
        } catch (e) {
            // Fehlende Uebersetzung ist kein Grund, den Editor nicht anzuzeigen - er spricht dann
            // englisch. Nur wissen soll man es.
            console.warn('[ITV] Markdown editor: language file missing:', assets.languageJs, e);
        }
    }
}

/**
 * Ob die Oberflaeche gerade dunkel ist.
 *
 * Gemessen statt erfragt: MudBlazor fuehrt seinen Dunkel-Modus im ThemeProvider, und den bis in jede
 * Maske durchzureichen, die ein Markdown-Feld enthaelt, waere ein Parameter, den jeder Aufrufer
 * vergessen kann. Die Helligkeit der Seitenfarbe ist dieselbe Auskunft, nur ohne Verdrahtung.
 * Wer es genau wissen muss, setzt den Parameter Dark der Komponente.
 */
function detectDark() {
    try {
        const color = window.getComputedStyle(document.body).backgroundColor || '';
        const parts = color.match(/\d+(\.\d+)?/g);
        if (!parts || parts.length < 3) {
            return false;
        }
        const luminance = (0.299 * +parts[0] + 0.587 * +parts[1] + 0.114 * +parts[2]) / 255;
        return luminance < 0.5;
    } catch (e) {
        console.warn('[ITV] Markdown editor could not determine the surface brightness:', e);
        return false;
    }
}

/**
 * Loest eine Schema-Adresse (`resource:name`) an der Praefix-Tabelle auf, oder liefert null.
 * schemes = { "resource:": "/{tenant}/help/res/" }
 */
function resolveUrl(url, schemes) {
    if (!url || !schemes) {
        return null;
    }
    for (const scheme of Object.keys(schemes)) {
        if (url.toLowerCase().startsWith(scheme.toLowerCase())) {
            const name = url.substring(scheme.length).trim().replace(/^\/+/, '');
            return name ? schemes[scheme] + encodeURIComponent(name) : null;
        }
    }
    return null;
}

/**
 * Loest `resource:`-Bilder fuer die ANZEIGE auf - im WYSIWYG wie in der Markdown-Vorschau.
 *
 * **Warum das den gespeicherten Text nicht anfasst:** der Editor rendert Bilder im WYSIWYG ueber eine
 * ProseMirror-NodeView OHNE contentDOM. Fuer die ignoriert ProseMirror DOM-Aenderungen (Voreinstellung
 * von ignoreMutation), und `getMarkdown()` serialisiert ohnehin aus den Knoten-Attributen
 * (`imageUrl`), nicht aus dem DOM. Im Text steht also weiter `resource:name` - nur das <img> zeigt
 * auf den echten Endpunkt.
 *
 * Die urspruengliche Adresse bleibt in data-itv-src stehen: ohne sie wuerde der zweite Durchlauf die
 * bereits aufgeloeste Adresse nicht mehr wiedererkennen.
 */
function fixImages(root, schemes) {
    if (!root || !schemes) {
        return;
    }
    root.querySelectorAll('img').forEach(img => {
        const raw = img.getAttribute('data-itv-src') || img.getAttribute('src') || '';
        const resolved = resolveUrl(raw, schemes);
        if (resolved && img.getAttribute('src') !== resolved) {
            img.setAttribute('data-itv-src', raw);
            img.setAttribute('src', resolved);
        }
    });
}

/**
 * Haelt fixImages an der Anzeige dran: der Editor baut seinen Inhalt beim Tippen staendig neu auf.
 * Gebuendelt ueber requestAnimationFrame - eine Bearbeitung loest Dutzende Mutationen aus, und jede
 * einzeln zu beantworten hiesse, den Baum Dutzende Male zu durchlaufen.
 */
function observeImages(root, schemes) {
    let scheduled = false;
    const observer = new MutationObserver(() => {
        if (scheduled) {
            return;
        }
        scheduled = true;
        window.requestAnimationFrame(() => {
            scheduled = false;
            fixImages(root, schemes);
        });
    });
    observer.observe(root, { childList: true, subtree: true, attributes: true, attributeFilter: ['src'] });
    fixImages(root, schemes);
    return observer;
}

/**
 * Baut die Knoepfe fuer die Ressourcen-Auswahl als eigene Werkzeugleisten-Eintraege.
 *
 * Bewusst IN der Leiste und nicht daneben: ein Knopf ausserhalb des Editors sieht aus wie eine
 * Eigenschaft der Maske, nicht wie eines des Textes - und sitzt bei zwei Editoren untereinander
 * am falschen.
 */
function buildPickerButtons(pickers, onPick) {
    return pickers.map(p => {
        const button = document.createElement('button');
        button.type = 'button';
        // Die Symbole der Leiste kommen aus einer Sprite-Datei des Editors, angesprochen ueber die
        // Klasse. Fuer "aus der Bibliothek" gibt es keines - genommen wird deshalb das des
        // eingebauten Bild- bzw. Verweis-Werkzeugs, das hier ersetzt wird; die Bedeutung traegt der
        // Tooltip. Ein eigenes Symbol hiesse, eine zweite Symbolquelle einzufuehren.
        button.className = 'toastui-editor-toolbar-icons itv-md-picker '
            + (p.kind === 'Video' ? 'link' : 'image');
        button.style.margin = '0';
        button.addEventListener('click', () => onPick(p.kind));
        return { name: 'itvPick' + p.kind, tooltip: p.tooltip || p.kind, el: button };
    });
}

/**
 * Baut den Editor in das Element mit der Id auf.
 * options: { value, height, initialEditType, language, dark, readOnly, allowUpload, schemes, pickers,
 *            assets: { css, darkCss, js, languageJs } }
 * Liefert true, wenn er steht. Scheitert es, WIRFT diese Funktion - der Grund landet damit ueber die
 * JSException auch im Server-Log und nicht nur in der Browser-Konsole. Die Komponente faengt das und
 * schaltet auf ein einfaches Textfeld um.
 */
export async function init(id, dotNetRef, options) {
    const opts = options || {};
    try {
        if (!opts.assets || !opts.assets.css || !opts.assets.js) {
            throw new Error('Markdown editor: the addresses of the editor files are missing '
                + '(options.assets.css/js).');
        }

        await ensureEditorLoaded(opts.assets);

        const host = document.getElementById(id);
        if (!host) {
            throw new Error(`Markdown editor: element ${id} not found.`);
        }

        destroy(id);

        const schemes = opts.schemes || null;
        let timer = null;
        const entry = {};

        const push = () => {
            if (!entry.editor || !dotNetRef) {
                return;
            }
            try {
                dotNetRef.invokeMethodAsync('OnEditorChanged', entry.editor.getMarkdown());
            } catch (e) {
                // Kein Circuit mehr (Seite verlassen, Verbindung weg): der Text ist dann ohnehin
                // nicht mehr zu retten, aber stillschweigend verschwinden soll der Fehler nicht.
                console.warn('[ITV] Markdown editor could not report its text to Blazor:', e);
            }
        };

        const pickerItems = Array.isArray(opts.pickers) && opts.pickers.length !== 0
            ? buildPickerButtons(opts.pickers, async kind => {
                try {
                    const markdown = await dotNetRef.invokeMethodAsync('PickResource', kind);
                    if (markdown) {
                        entry.editor.insertText(markdown);
                        entry.editor.focus();
                        push();
                    }
                } catch (e) {
                    console.error('[ITV] Markdown editor: resource picker failed:', e);
                }
            })
            : [];

        const toolbar = pickerItems.length !== 0 ? TOOLBAR.concat([pickerItems]) : TOOLBAR;

        const editor = new toastui.Editor({
            el: host,
            height: opts.height || '400px',
            initialEditType: opts.initialEditType || 'wysiwyg',
            previewStyle: 'vertical',
            usageStatistics: false,
            hideModeSwitch: false,
            language: opts.language || 'en-US',
            toolbarItems: toolbar,
            initialValue: opts.value || '',
            hooks: {
                // Eingefuegte/gezogene Bilder. OHNE diesen Haken schriebe der Editor sie als data:-URI
                // in den Text - der landete in der Datenbank und in jeder Auslieferung. Hier gehen sie
                // stattdessen in die Ressourcen-Bibliothek und kommen als `resource:name` zurueck.
                addImageBlobHook: (blob, callback) => {
                    if (!opts.allowUpload) {
                        console.warn('[ITV] Markdown editor: image paste rejected (no upload target).');
                        dotNetRef.invokeMethodAsync('OnUploadRejected').catch(() => { });
                        return false;
                    }

                    // Ueber eine JS-Stream-Referenz und NICHT als base64-Argument: eine
                    // Blazor-Server-Verbindung nimmt je Nachricht standardmaessig nur 32 KB an - ein
                    // eingefuegter Screenshot risse sie ab.
                    const streamRef = DotNet.createJSStreamReference(blob);
                    dotNetRef.invokeMethodAsync('UploadResource', blob.name || null, blob.type || null, streamRef)
                        .then(name => {
                            if (name) {
                                callback('resource:' + name, name);
                            }
                        })
                        .catch(e => console.error('[ITV] Markdown editor: upload failed:', e));
                    return false;   // false = der Editor fuegt nichts selbst ein; das macht callback.
                }
            }
        });

        editor.on('change', () => {
            // Gebuendelt: der Server soll den Zwischenstand kennen, aber nicht jeden Tastendruck.
            window.clearTimeout(timer);
            timer = window.setTimeout(push, DEBOUNCE_MS);
        });

        // Beim Verlassen sofort - sonst koennte ein Klick auf "Speichern" schneller sein als das
        // Buendel-Fenster und die zuletzt getippten Zeichen fehlten.
        editor.on('blur', () => {
            window.clearTimeout(timer);
            push();
        });

        entry.editor = editor;
        entry.host = host;
        entry.observer = schemes ? observeImages(host, schemes) : null;
        editors[id] = entry;

        setDark(id, opts.dark === null || opts.dark === undefined ? detectDark() : opts.dark === true);

        if (opts.readOnly === true) {
            host.classList.add('itv-markdown-readonly');
        }

        return true;
    } catch (e) {
        // Beides: in der Konsole steht das echte Fehlerobjekt (mit Stack), weitergeworfen wird es
        // fuer das Server-Log.
        console.error('[ITV] Markdown editor could not be created:', e);
        throw e;
    }
}

/** Der aktuelle Text. Das ist der verlaessliche Weg zum Inhalt - nicht der zuletzt gemeldete. */
export function getMarkdown(id) {
    const entry = editors[id];
    return entry ? entry.editor.getMarkdown() : null;
}

/**
 * Setzt den Text von aussen (z.B. weil die Maske eine andere Sprache geladen hat).
 * Ist er derselbe, passiert nichts - sonst spraenge die Einfuegemarke bei jedem Rendern ans Ende.
 */
export function setMarkdown(id, value) {
    const entry = editors[id];
    if (!entry) {
        return;
    }
    const next = value || '';
    if (entry.editor.getMarkdown() !== next) {
        entry.editor.setMarkdown(next, false);
    }
}

/**
 * Fuegt Markdown an der Einfuegemarke ein - der Weg fuer "Bild einfuegen".
 *
 * `insertText` ist die einzige Editor-Schnittstelle, die in BEIDEN Modi (WYSIWYG und Markdown)
 * dasselbe tut: sie schiebt Rohtext an die aktuelle Stelle und laesst ihn im WYSIWYG-Modus vom Editor
 * selbst deuten. Der Umweg ueber setMarkdown(getMarkdown() + ...) haengte ihn stattdessen ans Ende und
 * verloere die Einfuegemarke.
 *
 * Liefert den Text NACH dem Einfuegen zurueck, damit Blazor seinen Stand nachziehen kann, ohne auf die
 * gebuendelte Meldung zu warten.
 */
export function insert(id, markdown) {
    const entry = editors[id];
    if (!entry) {
        return null;
    }
    entry.editor.insertText(markdown || '');
    entry.editor.focus();
    return entry.editor.getMarkdown();
}

/** Dunkles Thema an/aus. Die Farben liegen unter .toastui-editor-dark, beide CSS sind geladen. */
export function setDark(id, dark) {
    const entry = editors[id];
    if (!entry) {
        return;
    }
    entry.host.classList.toggle('toastui-editor-dark', dark === true);
}

export function destroy(id) {
    const entry = editors[id];
    if (!entry) {
        return;
    }
    try {
        if (entry.observer) {
            entry.observer.disconnect();
        }
        entry.editor.destroy();
    } catch (e) {
        // Aufraeumen darf nichts umwerfen - aber es soll nachvollziehbar bleiben, wenn es hakt.
        console.warn('[ITV] Markdown editor could not be disposed:', e);
    }
    delete editors[id];
}
