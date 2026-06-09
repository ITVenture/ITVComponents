using System;
using System.Collections.Generic;
using System.IO;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    /// <summary>
    /// Framework-neutral carrier for a file/stream that a FileHandler serves back to the caller.
    /// Used by both the read-/download-path (<see cref="IFileHandler.ReadFile"/> /
    /// <see cref="IAsyncFileHandler.ReadFile"/>) and the post-upload response of a
    /// <see cref="IRespondingFileHandler"/> / <see cref="IAsyncRespondingFileHandler"/>.
    /// The host edge (MVC / Blazor) renders it: MVC maps it onto an <c>IResult</c>,
    /// Blazor streams the content to the browser.
    /// </summary>
    public class FileReadResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the requested file was found / produced.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the suggested download file-name. When <c>null</c> the host falls back to its own default.
        /// </summary>
        public string DownloadName { get; set; }

        /// <summary>
        /// Gets or sets the content-type set on the result. When <c>null</c> the host falls back to its own default.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the content is served as a file-download (<c>true</c>) or as an
        /// embeddable/inline result. When <c>null</c> the host falls back to its own default.
        /// </summary>
        public bool? FileDownload { get; set; }

        /// <summary>
        /// Gets or sets the content of the file.
        /// </summary>
        public Stream FileContent { get; set; }

        /// <summary>
        /// Gets a list of resources that the host must dispose once the content-stream has been consumed
        /// (e.g. the DbConnection/DbDataReader backing <see cref="FileContent"/>).
        /// </summary>
        public List<IDisposable> DeferredDisposals { get; } = new List<IDisposable>();
    }
}
