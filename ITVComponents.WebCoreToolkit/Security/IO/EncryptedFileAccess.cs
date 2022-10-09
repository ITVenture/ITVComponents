using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.IO;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Security.IO
{
    public class EncryptedFileAccess: IFileAccess
    {
        private readonly IFileAccess innerAccess;
        private readonly ISecurityRepository securityRepository;
        private readonly IPermissionScope permissionScope;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the EncryptedFileAccess class
        /// </summary>
        /// <param name="innerAccess">the inner file-Access implementation that handles cleartext-data</param>
        /// <param name="services">the services that enable this object to identify the current user-environment</param>
        public EncryptedFileAccess(IFileAccess innerAccess, IServiceProvider services)
        {
            this.innerAccess = innerAccess;
            securityRepository = services.GetService<ISecurityRepository>();
            permissionScope = services.GetService<IPermissionScope>();
            if (securityRepository == null || permissionScope == null)
            {
                throw new InvalidOperationException("Unable to identify the current user-scope that is required for crypto operations");
            }
        }

        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown, when the file was not found</exception>
        public Stream OpenRead(string name)
        {
            return securityRepository.GetDecryptStream(innerAccess.OpenRead(name), permissionScope.PermissionPrefix);
        }

        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown when the file was not found</exception>
        public async Task<Stream> OpenReadAsync(string name)
        {
            return securityRepository.GetDecryptStream(await innerAccess.OpenReadAsync(name), permissionScope.PermissionPrefix);
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
            return securityRepository.GetEncryptStream(innerAccess.OpenWrite(name, overwrite),
                permissionScope.PermissionPrefix);
        }

        /// <summary>
        /// Opens a writeable stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="overwrite">indicates whether - in case it already exists - the file can be overwritten</param>
        /// <returns>a stream-object with which the file can be written</returns>
        /// <exception cref="InvalidOperationException">when the file already exists and overwrite was set to false</exception>
        public async Task<Stream> OpenWriteAsync(string name, bool overwrite = false)
        {
            return securityRepository.GetEncryptStream(await innerAccess.OpenWriteAsync(name, overwrite),
                permissionScope.PermissionPrefix);
        }

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        public bool DeleteFile(string name)
        {
            return innerAccess.DeleteFile(name);
        }

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        public async Task<bool> DeleteFileAsync(string name)
        {
            return await innerAccess.DeleteFileAsync(name);
        }

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
