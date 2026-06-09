using System;
using System.Collections.Generic;
using System.IO;
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
        /// Gets or sets the framework-neutral outcome code of the operation.
        /// </summary>
        public FileOperationCode Code { get; set; } = FileOperationCode.OK;

        /// <summary>
        /// Gets or sets the file/stream a responding handler produced for the completed upload
        /// (set when <see cref="Code"/> is <see cref="FileOperationCode.OkWithContent"/>).
        /// </summary>
        public FileReadResult ReadResult { get; set; }

        /// <summary>
        /// Gets or sets the validation errors that were produced while processing the file.
        /// </summary>
        public IReadOnlyList<FileError> Errors { get; set; } = Array.Empty<FileError>();

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static FileOperationResult Ok() => new FileOperationResult { Success = true };

        /// <summary>
        /// Creates a forbid result.
        /// </summary>
        public static FileOperationResult Forbid() => new FileOperationResult { Success = false, Code = FileOperationCode.Forbid };

        /// <summary>
        /// Creates a forbid result.
        /// </summary>
        public static FileOperationResult UnAuthorized() => new FileOperationResult { Success = false, Code = FileOperationCode.UnAuthorized };

        /// <summary>
        /// Creates a failed result with the given errors.
        /// </summary>
        public static FileOperationResult Fail(FileOperationCode code, params FileError[] errors)
            => new FileOperationResult { Success = false, Code = code, Errors = errors ?? Array.Empty<FileError>() };

        /// <summary>
        /// Creates a failed result with a single keyed error.
        /// </summary>
        public static FileOperationResult Fail(string key, string message, FileOperationCode code)
            => Fail(code, new FileError(key, message));

        public static FileOperationResult BadRequest(string exMessage) => Fail(FileOperationCode.BadRequest ,new FileError("BadRequest", exMessage));

        /// <summary>
        /// Creates a not-found result (e.g. the requested download was not produced by the handler).
        /// </summary>
        public static FileOperationResult NotFound(params FileError[] errors) => Fail(FileOperationCode.NotFound, errors);

        public static FileOperationResult OkWithContent(FileReadResult uploadResponse) =>
            new()
            {
                Success = true,
                ReadResult = uploadResponse,
                Code = FileOperationCode.OkWithContent
            };
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

    public enum FileOperationCode
    {
        OK,
        OkWithContent,
        BadRequest,
        NotFound,
        UnAuthorized,
        Forbid
    }
}
