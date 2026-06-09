using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    /// <summary>
    /// An <see cref="IAsyncFileHandler"/> that produces a custom response for the completed upload-process.
    /// Framework-neutral: returns a <see cref="FileReadResult"/> instead of an MVC <c>IResult</c>, so the
    /// same handler is consumable from MVC and Blazor.
    /// </summary>
    public interface IAsyncRespondingFileHandler : IAsyncFileHandler
    {
        /// <summary>
        /// Gets the response for the completed upload process.
        /// </summary>
        /// <returns>the response describing the result of the upload request</returns>
        Task<FileReadResult> GetUploadResult();
    }
}
