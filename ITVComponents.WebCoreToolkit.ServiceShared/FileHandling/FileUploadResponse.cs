using System.Text;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    /// <summary>
    /// Framework-neutral response that a <see cref="IRespondingFileHandler"/> / <see cref="IAsyncRespondingFileHandler"/>
    /// produces for the completed upload. The host edge (MVC / Blazor) renders it: MVC maps it onto an
    /// <c>IResult</c>, Blazor surfaces the content in the component. Replaces the former MVC-only <c>IResult</c> payload.
    /// </summary>
    public class FileUploadResponse
    {
        /// <summary>
        /// Gets or sets the raw response content (e.g. a comparison/diff document).
        /// </summary>
        public byte[] Content { get; set; }

        /// <summary>
        /// Gets or sets the content-type of <see cref="Content"/> (e.g. "application/json").
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the suggested download file-name. When empty the content is meant to be shown inline.
        /// </summary>
        public string DownloadName { get; set; }

        /// <summary>
        /// Convenience: builds a response from a text payload (UTF-8 encoded).
        /// </summary>
        /// <param name="content">the textual content</param>
        /// <param name="contentType">the content-type, defaults to application/json</param>
        public static FileUploadResponse Text(string content, string contentType = "application/json")
            => new FileUploadResponse { Content = Encoding.UTF8.GetBytes(content ?? string.Empty), ContentType = contentType };

        /// <summary>
        /// Convenience: builds a binary response.
        /// </summary>
        /// <param name="content">the binary content</param>
        /// <param name="contentType">the content-type</param>
        /// <param name="downloadName">an optional download file-name (inline when empty)</param>
        public static FileUploadResponse Bytes(byte[] content, string contentType, string downloadName = null)
            => new FileUploadResponse { Content = content, ContentType = contentType, DownloadName = downloadName };
    }
}
