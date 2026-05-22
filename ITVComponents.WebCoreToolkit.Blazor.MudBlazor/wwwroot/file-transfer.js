// Helpers for the in-process FileUpload / FileDownload components. Uploads no longer post to an MVC
// endpoint — the file bytes are streamed to the server-side IAsyncFileHandler/IFileHandler directly
// (Blazor circuit). This module only opens the native file picker and triggers a browser download from
// a server-provided stream. Loaded on demand via dynamic import — no <script> tag required.

export function clickInput(id) {
    const el = document.getElementById(id);
    if (el) {
        el.click();
    }
}

// Triggers a browser "save as" for a .NET stream (DotNetStreamReference). Used by the in-process
// download path: the server reads the file via the FileHandler and streams the bytes here.
export async function downloadFromStream(fileName, contentType, streamRef) {
    const arrayBuffer = await streamRef.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: contentType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || 'download';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
}
