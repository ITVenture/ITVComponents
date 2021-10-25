using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Net.FileHandling
{
    /// <summary>
    /// Implements a components that is capable for parsing the form-content of a multipart-message that was posted to a file-action
    /// </summary>
    public interface IAsyncFormProcessor: IAsyncFileHandler
    {
        /// <summary>
        /// Processes a form that was found in an upload-multipart-message
        /// </summary>
        /// <param name="sectionHeaders">the headers of the current section</param>
        /// <param name="sectionBody">the body of the current section</param>
        /// <returns>a string that represents the form content of the current section</returns>
        Task<string> ProcessForm(Dictionary<string, StringValues> sectionHeaders, Stream sectionBody);
    }
}
