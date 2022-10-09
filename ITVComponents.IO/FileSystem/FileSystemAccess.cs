using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.IO.FileSystem
{
    public class FileSystemAccess:IFileAccess
    {
        private readonly string baseDirectory;
        private readonly string pathSeparator;

        /// <summary>
        /// Initializes a new instance of the FileSystemAccess class
        /// </summary>
        public FileSystemAccess()
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileSystemAccess class
        /// </summary>
        /// <param name="baseDirectory">the base directory</param>
        /// <param name="pathSeparator">a separator that is used to turn a file-name into a folder structure</param>
        public FileSystemAccess(string baseDirectory, string pathSeparator)
        {
            this.baseDirectory = baseDirectory;
            this.pathSeparator = pathSeparator;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown, when the file was not found</exception>
        public Stream OpenRead(string name)
        {
            var path = ConstructPath(name);
            if (File.Exists(path))
            {
                return File.OpenRead(path);
            }

            throw new InvalidOperationException("The requested file does not exist");
        }

        /// <summary>
        /// Creates a path based on the name of a target-file
        /// </summary>
        /// <param name="name">the name of a file that either must be written or read</param>
        /// <returns>a full-path identifying the complete path of the target file</returns>
        private string ConstructPath(string name)
        {
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return name;
            }

            var splitted = name.Split(pathSeparator);
            var p = string.Join("/", splitted);
            var pt = Path.GetDirectoryName(Path.Join(baseDirectory, p));
            if (!Directory.Exists(pt))
            {
                Directory.CreateDirectory(pt);
            }

            return Path.Join(pt, name);
        }

        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown when the file was not found</exception>
        public Task<Stream> OpenReadAsync(string name)
        {
            return Task.FromResult(OpenRead(name));
        }

        /// <summary>
        /// Opens a writeable stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="overwrite">indicates whether - in case it already exists - the file can be overwritten</param>
        /// <returns>a stream-object with which the file can be written</returns>
        /// <exception cref="InvalidOperationException">when the file already exists and overwrite was set to false</exception>
        public Stream OpenWrite(string name, bool overwrite = false)
        {
            var path = ConstructPath(name);
            if (overwrite || !File.Exists(path))
            {
                return File.Create(path);
            }

            throw new InvalidOperationException("The requested file does not exist");
        }

        /// <summary>
        /// Opens a writeable stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="overwrite">indicates whether - in case it already exists - the file can be overwritten</param>
        /// <returns>a stream-object with which the file can be written</returns>
        /// <exception cref="InvalidOperationException">when the file already exists and overwrite was set to false</exception>
        public Task<Stream> OpenWriteAsync(string name, bool overwrite = false)
        {
            return Task.FromResult(OpenWrite(name, overwrite));
        }

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        public bool DeleteFile(string name)
        {
            var path = ConstructPath(name);
            if (File.Exists(name))
            {
                try
                {
                    File.Delete(path);
                    return true;
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Delete of file failed: {ex.Message}", LogSeverity.Error);
                }
            }

            return false;
        }

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        public Task<bool> DeleteFileAsync(string name)
        {
            return Task.FromResult(DeleteFile(name));
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
