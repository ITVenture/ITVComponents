using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.FileHandling
{
    /// <summary>
    /// FileHandler that is able to produce a custom Response for the uploading-process
    /// </summary>
    public interface IAsyncRespondingFileHandler:IAsyncFileHandler
    {
        /// <summary>
        /// Gets the ActionResult for the complete upload process
        /// </summary>
        /// <returns>an action result as a reaction of the given upload request</returns>
        Task<IResult> GetUploadResult();
    }
}
