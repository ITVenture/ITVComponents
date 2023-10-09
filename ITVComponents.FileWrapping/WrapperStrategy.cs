using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.Annotations;
using ITVComponents.FileWrapping.General;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.FileWrapping
{
    /// <summary>
    /// Abstract representation of an unwrapper class for the pollstep
    /// </summary>
    public abstract class WrapperStrategy: IDisposable
    {
        /// <summary>
        /// A List containing available unwrapper objects. The Key identifies the file-format
        /// </summary>
        private static List<WrapperStrategy> availableWrappers;

        /// <summary>
        /// Performs static initializations on the Unwrapper class
        /// </summary>
        static WrapperStrategy()
        {
            availableWrappers = new List<WrapperStrategy>();
        }

        /// <summary>
        /// Initializes a new instance of the Unwrapper class
        /// </summary>
        protected WrapperStrategy()
        {
            lock (availableWrappers)
            {
                availableWrappers.Add(this);
            }
        }

        /// <summary>
        /// Gets the processable filetype for this unwrapper
        /// </summary>
        public abstract string UnwrapperType { get; }

        /// <summary>
        /// Gets the fileExtension for the current wrapper type
        /// </summary>
        public abstract string FileExtension { get; }

        /// <summary>
        /// Gets an Unwrapper that fits the given creteria
        /// </summary>
        /// <param name="wrapperType">the filetype that needs to be processed by an unwrapper instance</param>
        /// <returns>the unwrapper fitting the type crieteria</returns>
        public static WrapperStrategy GetWrapper(string wrapperType)
        {
            lock (availableWrappers)
            {
                return
                    (from t in availableWrappers
                        where t.UnwrapperType.Equals(wrapperType, StringComparison.OrdinalIgnoreCase)
                        select t).FirstOrDefault();
            }
        }



        /// <summary>
        /// Unwraps the file and puts its content into a directory with the same name as the input file
        /// </summary>
        /// <param name="file">the input file that needs to be unwrapped</param>
        /// <param name="unwrappedDirectory">the directory containing the unwrapped files</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(string file, out string unwrappedDirectory, out FileMap unwrappedFiles);

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="file">the wrapped file</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(string file, Func<string, Stream> getStreamForFileCallback, out FileMap unwrappedFiles);

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="file">the wrapped file</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(string file, Func<FileMapEntry, Stream> getStreamForFileCallback, out FileMap unwrappedFiles);

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="targetDirectory">the directory in which to unwrap the files</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(Stream inputStream, string targetDirectory, out FileMap unwrappedFiles);

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="file">the wrapped file</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="ignoreNullStreams">indicates whether to ignore files for which the callback returns null. If this value is false, returning null from the callback will use default-location that was estimated from the file-map</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(string file, [NotNull]Func<FileMapEntry, Stream> getStreamForFileCallback, bool ignoreNullStreams, out FileMap unwrappedFiles);


        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(Stream inputStream, Func<string, Stream> getStreamForFileCallback, out FileMap unwrappedFiles);

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(Stream inputStream, Func<FileMapEntry, Stream> getStreamForFileCallback, out FileMap unwrappedFiles);

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="ignoreNullStreams">indicates whether to ignore files for which the callback returns null. If this value is false, returning null from the callback will use default-location that was estimated from the file-map</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public abstract bool Unwrap(Stream inputStream, [NotNull]Func<FileMapEntry, Stream> getStreamForFileCallback, bool ignoreNullStreams, out FileMap unwrappedFiles);

        /// <summary>
        /// Unwraps a specific file from the given wrapper-file
        /// </summary>
        /// <param name="file">the wrapper file from which to extract a specific file</param>
        /// <param name="fileName">the name of the file that opened</param>
        /// <param name="fileStream">the stream representing the file in the given </param>
        /// <returns>a resource-lock object representing all open streams that require to be closed after reading the returned file</returns>
        public abstract IResourceLock Open(string file, string fileName, out Stream fileStream);

        /// <summary>
        /// Unwraps a specific file from the given wrapper-file
        /// </summary>
        /// <param name="inputStream">an opened stream of the wrapper-file containing the requested file</param>
        /// <param name="fileName">the name of the file that opened</param>
        /// <param name="fileStream">the stream representing the file in the given </param>
        /// <returns>a resource-lock object representing all open streams that require to be closed after reading the returned file</returns>
        public abstract IResourceLock Open(Stream inputStream, string fileName, out Stream fileStream);

        /// <summary>
        /// Wraps the given directory and the indexFile into a wrapper-file
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public abstract void WrapFiles(string directory, string wrappedName);

        /// <summary>
        /// Wraps the given IndexFile and all provided files into a wrapper file
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public abstract void WrapFiles(string[] files, string wrappedName);

        /// <summary>
        /// The FileMap that is used in order to create an archive
        /// </summary>
        /// <param name="map">the map that contains files which need to be added to an archive</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public abstract void WrapFiles(FileMap map, string wrappedName);

        /// <summary>
        /// Appends the content of a folder to an existing Archive
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public abstract void AppendFiles(string directory, string wrappedName);

        /// <summary>
        /// Appends the given Files to an existing Archive
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public abstract void AppendFiles(string[] files, string wrappedName);

        /// <summary>
        /// Appends the Files of a map to an existing Archive
        /// </summary>
        /// <param name="map">the map that contains files which need to be added to an archive</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public abstract void AppendFiles(FileMap map, string wrappedName);

        /// <summary>
        /// Creates a FileMap with the settings for this wrapper
        /// </summary>
        /// <returns>a new fileMap that has the same settings as the current wrapper</returns>
        public abstract FileMap CreateMap();

        /// <summary>
        /// Creates a FileMap reflection the content of a specific wrapper-file
        /// </summary>
        /// <param name="inputFile"></param>
        /// <returns></returns>
        public abstract FileMap CreateMap(string inputFile);

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            lock (availableWrappers)
            {
                availableWrappers.Remove(this);
            }

            Dispose(true);
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <param name="disposing">gibt an, ob nicht unmanaged resourcen ebenfalls freigegeben werden sollen</param>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose(bool disposing)
        {
        }
    }
}