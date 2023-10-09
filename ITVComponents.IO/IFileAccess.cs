using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.IO
{
    /// <summary>
    /// Presents some generic IO Operations such as reading, writing and deleting of files in an interface.
    /// </summary>
    public interface IFileAccess
    {
        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown, when the file was not found</exception>
        Stream OpenRead(string name);

        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown when the file was not found</exception>
        Task<Stream> OpenReadAsync(string name);

        /// <summary>
        /// Opens a writeable stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="overwrite">indicates whether - in case it already exists - the file can be overwritten</param>
        /// <returns>a stream-object with which the file can be written</returns>
        /// <exception cref="InvalidOperationException">when the file already exists and overwrite was set to false</exception>
        Stream OpenWrite(string name, bool overwrite = false);

        /// <summary>
        /// Opens a writeable stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="overwrite">indicates whether - in case it already exists - the file can be overwritten</param>
        /// <returns>a stream-object with which the file can be written</returns>
        /// <exception cref="InvalidOperationException">when the file already exists and overwrite was set to false</exception>
        Task<Stream> OpenWriteAsync(string name, bool overwrite = false);

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        bool DeleteFile(string name);

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        Task<bool> DeleteFileAsync(string name);
    }
}
