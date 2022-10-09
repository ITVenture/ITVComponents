using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ITVComponents.IO.Azure
{
    public class AzureFileAccess:IFileAccess
    {
        private BlobServiceClient blobServiceClient;

        private BlobContainerClient containerClient;
        private string storageConnectionString;
        private string containerName;

        /// <summary>
        /// Initializes a new instance of the storageConnectionString class
        /// </summary>
        /// <param name="storageConnectionString">the connect string for the storage</param>
        /// <param name="containerName">the name of the target container</param>
        public AzureFileAccess(string storageConnectionString, string containerName)
        {
            this.storageConnectionString = storageConnectionString;
            this.containerName = containerName;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        private BlobServiceClient BlobService => blobServiceClient ??= new BlobServiceClient(storageConnectionString);

        private BlobContainerClient Container =>
            containerClient ??= BlobService.GetBlobContainerClient(containerName);

        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown, when the file was not found</exception>
        public Stream OpenRead(string name)
        {
            var client = GetClient(name);
            if (client.Exists().Value)
            {
                return client.OpenRead(true);
            }

            throw new InvalidOperationException($"{name} not found!");
        }

        /// <summary>
        /// Opens a read-only stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <returns>a stream-object from which the file can be read</returns>
        /// <exception cref="InvalidOperationException">is thrown when the file was not found</exception>
        public async Task<Stream> OpenReadAsync(string name)
        {
            var client = GetClient(name);
            if ((await client.ExistsAsync()).Value)
            {
                return await client.OpenReadAsync(true);
            }

            throw new InvalidOperationException($"{name} not found!");
        }

        /// <summary>
        /// Opens a writeable stream for the given file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="overwrite">indicates whether - in case it already exists - the file can be overwritten</param>
        /// <returns>a stream-object with which the file can be written</returns>
        /// <exception cref="InvalidOperationException">when the file already exists and overwrite was set to false</exception>
        public Stream OpenWrite(string name, bool overwrite=false)
        {
            var client = GetClient(name);
            if (overwrite || !client.Exists().Value)
            {
                return client.OpenWrite(overwrite);
            }

            throw new InvalidOperationException($"{name} already exists!");
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
            var client = GetClient(name);
            if (overwrite || !(await client.ExistsAsync()).Value)
            {
                return await client.OpenWriteAsync(overwrite);
            }

            throw new InvalidOperationException($"{name} already exists!");
        }

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        public bool DeleteFile(string name)
        {
            var client = GetClient(name);
            return client.DeleteIfExists().Value;
        }

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="name">the name of the file to delete</param>
        /// <returns>a value indicating whether the file was successfully deleted</returns>
        public async Task<bool> DeleteFileAsync(string name)
        {
            var client = GetClient(name);
            return (await client.DeleteIfExistsAsync()).Value;
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

        private BlobClient GetClient(string blobId)
        {
            return Container.GetBlobClient(blobId);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
