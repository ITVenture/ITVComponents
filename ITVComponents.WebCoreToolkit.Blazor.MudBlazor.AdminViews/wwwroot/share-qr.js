// Saving and printing the QR code of a share link.
//
// Das Bild selbst entsteht auf dem Server (ShareQrCode) und steht im Dialog schon als data:-URL im
// <img>. Hier geht es nur um die zwei Dinge, die reines HTML nicht kann: eine Datei ablegen und ein
// Blatt drucken, auf dem ausser dem Code auch steht, worum es geht.
//
// Geladen wird das Modul bei Bedarf (`import` aus der Komponente), nicht ueber AddToolkitClientScript -
// es betrifft einen einzigen Dialog, und der Rest der Anwendung soll es nicht mitschleppen.

/**
 * Decodes the Base64 PNG into a blob.
 */
function toBlob(base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    return new Blob([bytes], { type: 'image/png' });
}

/**
 * Hands the QR code to the browser as a file download.
 *
 * @param {string} fileName the name the file gets
 * @param {string} base64 the PNG, Base64-encoded
 */
export function save(fileName, base64) {
    const url = URL.createObjectURL(toBlob(base64));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    // Waehrend der Download anlaeuft, braucht der Browser die URL noch - sofortiges Freigeben laesst ihn
    // in Safari mit einer leeren Datei enden.
    setTimeout(() => URL.revokeObjectURL(url), 30000);
}

/**
 * Prints a sheet holding the QR code, the title of the share and the link in plain text.
 *
 * Der Link steht mit auf dem Blatt, weil ein Ausdruck auch dann noch brauchbar sein soll, wenn die
 * Kamera nicht mitspielt. Aufgebaut wird die Seite in einem unsichtbaren iframe statt in einem neuen
 * Fenster: ein Fenster faellt dem Popup-Blocker zum Opfer, sobald der Klick nicht mehr als direkte
 * Benutzeraktion gilt - und in Blazor Server ist er das nach der Runde ueber den Circuit nicht mehr.
 *
 * @param {string} title the title of the share
 * @param {string} link the share link
 * @param {string} base64 the PNG, Base64-encoded
 */
export function printCode(title, link, base64) {
    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;';
    document.body.appendChild(frame);

    const doc = frame.contentWindow.document;
    doc.open();
    doc.write('<!DOCTYPE html><html><head><meta charset="utf-8"></head><body></body></html>');
    doc.close();

    const style = doc.createElement('style');
    style.textContent =
        '@page{margin:15mm}'
        + 'body{margin:0;font-family:system-ui,sans-serif;text-align:center;color:#000;background:#fff}'
        + 'h1{font-size:16pt;margin:0 0 8mm;word-break:break-word}'
        + 'img{width:90mm;height:90mm;image-rendering:pixelated}'
        + 'p{font-size:9pt;margin:8mm auto 0;max-width:120mm;word-break:break-all;font-family:monospace}';
    doc.head.appendChild(style);

    // Ueber das DOM aufgebaut und nicht als HTML-Zeichenkette geschrieben: Titel und Link kommen aus
    // Benutzereingaben, und textContent kann man nicht falsch maskieren.
    doc.title = title || 'QR code';

    if (title) {
        const heading = doc.createElement('h1');
        heading.textContent = title;
        doc.body.appendChild(heading);
    }

    const image = doc.createElement('img');
    image.alt = '';
    image.src = 'data:image/png;base64,' + base64;
    doc.body.appendChild(image);

    const caption = doc.createElement('p');
    caption.textContent = link;
    doc.body.appendChild(caption);

    let done = false;
    const cleanUp = () => {
        if (done) {
            return;
        }

        done = true;
        frame.remove();
    };

    const go = () => {
        // onafterprint raeumt auf, sobald der Dialog weg ist. Nicht jeder Browser meldet es, deshalb
        // zusaetzlich eine grosszuegige Frist - zu frueh entfernt bricht der laufende Druckauftrag ab.
        frame.contentWindow.onafterprint = cleanUp;
        setTimeout(cleanUp, 120000);
        frame.contentWindow.focus();
        frame.contentWindow.print();
    };

    if (image.complete) {
        go();
    } else {
        // Ohne fertiges Bild druckt Firefox ein leeres Blatt. Auch der Fehlerfall muss weiter - dann
        // steht wenigstens der Link auf dem Papier.
        image.onload = go;
        image.onerror = go;
    }
}
