using System;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    /// <summary>
    /// Framework-neutral result of a file-upload operation. The host edge (MVC / Blazor) maps this
    /// onto its own validation system (ModelState / EditContext) without the FileHandler knowing about either.
    /// </summary>
    public class FileOperationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the validation errors that were produced while processing the file.
        /// </summary>
        public IReadOnlyList<FileError> Errors { get; set; } = Array.Empty<FileError>();

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static FileOperationResult Ok() => new FileOperationResult { Success = true };

        /// <summary>
        /// Creates a failed result with the given errors.
        /// </summary>
        public static FileOperationResult Fail(params FileError[] errors)
            => new FileOperationResult { Success = false, Errors = errors ?? Array.Empty<FileError>() };

        /// <summary>
        /// Creates a failed result with a single keyed error.
        /// </summary>
        public static FileOperationResult Fail(string key, string message)
            => Fail(new FileError(key, message));
    }

    /// <summary>
    /// A single validation error produced by a FileHandler. <see cref="Key"/> is the logical field
    /// (e.g. "File") that the host edge maps onto its validation system.
    /// </summary>
    public class FileError
    {
        /// <summary>
        /// Initializes a new, empty instance of the <see cref="FileError"/> class.
        /// </summary>
        public FileError()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileError"/> class.
        /// </summary>
        /// <param name="key">the logical field the error belongs to</param>
        /// <param name="message">the error message</param>
        public FileError(string key, string message)
        {
            Key = key;
            Message = message;
        }

        /// <summary>
        /// Gets or sets the logical field the error belongs to.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }
    }
}
