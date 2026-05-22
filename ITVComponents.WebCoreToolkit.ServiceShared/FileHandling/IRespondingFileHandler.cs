namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    /// <summary>
    /// A <see cref="IFileHandler"/> that produces a custom response for the completed upload-process.
    /// Framework-neutral: returns a <see cref="FileUploadResponse"/> instead of an MVC <c>IResult</c>, so the
    /// same handler is consumable from MVC and Blazor.
    /// </summary>
    public interface IRespondingFileHandler : IFileHandler
    {
        /// <summary>
        /// Gets the response for the completed upload process.
        /// </summary>
        /// <returns>the response describing the result of the upload request</returns>
        FileUploadResponse GetUploadResult();
    }
}
