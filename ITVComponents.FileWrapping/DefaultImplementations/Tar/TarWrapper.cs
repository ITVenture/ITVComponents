using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ITVComponents.Annotations;
using ITVComponents.FileWrapping.General;
using ITVComponents.FileWrapping.Helpers;
using ITVComponents.Threading;

namespace ITVComponents.FileWrapping.DefaultImplementations.Tar
{
    /// <summary>
    /// Wrapper class capable for unwrapping Tar and Tar.Gz - Files
    /// </summary>
    public class TarWrapper:WrapperStrategy
    {
        /// <summary>
        /// The Identification Name for this Tar-Unwrapper
        /// </summary>
        private string unwrapperType;

        /// <summary>
        /// Indicates whether to use the Gz format prior to the tar -format
        /// </summary>
        private bool useGz;

        /// <summary>
        /// Indicates whether to put all files into the direct output root regardless of the zip's directory structure
        /// </summary>
        private bool flattenStructure;

        /// <summary>
        /// Initializes a new instance of the TarWrapper class
        /// </summary>
        /// <param name="unwrapperType">the identifier for this Tar-Unwrapper instance</param>
        /// <param name="useGzFormat">indicates whether to use the Gz - Format</param>
        /// <param name="flattenStructure">Indicates whether to put all files into the direct output root regardless of the zip's directory structure</param>
        public TarWrapper(string unwrapperType, bool useGzFormat, bool flattenStructure)
            : this(unwrapperType, useGzFormat)
        {
            this.flattenStructure = flattenStructure;
        }

        /// <summary>
        /// Initializes a new instance of the TarWrapper class
        /// </summary>
        /// <param name="unwrapperType">the identifier for this Tar-Unwrapper instance</param>
        public TarWrapper(string unwrapperType) : this(unwrapperType, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the TarWrapper class
        /// </summary>
        /// <param name="unwrapperType">the identifier for this Tar-Unwrapper instance</param>
        /// <param name="useGzFormat">indicates whether to use the Gz - Format</param>
        /// <param name="compression">the compression level to be used when using gz</param>
        private TarWrapper(string unwrapperType, bool useGzFormat)
            : this()
        {
            this.unwrapperType = unwrapperType;
            useGz = useGzFormat;
        }

        /// <summary>
        /// Prevents a default instance of the TarWrapper class from being created
        /// </summary>
        private TarWrapper()
        {
        }

        /// <summary>
        /// Gets the processable filetype for this unwrapper
        /// </summary>
        public override string UnwrapperType
        {
            get { return unwrapperType; }
        }

        /// <summary>
        /// Gets the fileExtension for the current wrapper type
        /// </summary>
        public override string FileExtension
        {
            get { return $".tar{(useGz ? ".gz" : "")}"; }
        }

        /// <summary>
        /// Unwraps the file and puts its content into a directory with the same name as the input file
        /// </summary>
        /// <param name="file">the input file that needs to be unwrapped</param>
        /// <param name="unwrappedDirectory">the directory containing the unwrapped files</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(string file, out string unwrappedDirectory, out FileMap unwrappedFiles)
        {
            string dir = Path.GetDirectoryName(file);
            string fn = Path.GetFileName(file);
            int id = fn.IndexOf(".");
            fn = fn.Substring(0, id);
            dir = string.Format(@"{0}\{1}", dir, fn);
            Directory.CreateDirectory(dir);
            unwrappedDirectory = dir;
            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, file))
            {
                unwrappedFiles = UnTarFiles(unwrappedDirectory, helper, true);
            }

            return true;
        }

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="file">the wrapped file</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(string file, Func<string, Stream> getStreamForFileCallback, out FileMap unwrappedFiles)
        {
            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, file))
            {
                if (getStreamForFileCallback != null)
                {
                    unwrappedFiles = UnTarFiles(".", helper, true, n => getStreamForFileCallback(n.ArchiveFileName));
                }
                else
                {
                    unwrappedFiles = UnTarFiles(".", helper, true, null);
                }
            }

            return true;
        }

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(Stream inputStream, Func<string, Stream> getStreamForFileCallback, out FileMap unwrappedFiles)
        {
            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, inputStream))
            {
                if (getStreamForFileCallback != null)
                {
                    unwrappedFiles = UnTarFiles(".", helper, true, n => getStreamForFileCallback(n.ArchiveFileName));
                }
                else
                {
                    unwrappedFiles = UnTarFiles(".", helper, true, null);
                }
            }

            return true;
        }

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="file">the wrapped file</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="ignoreNullStreams">indicates whether to ignore files for which the callback returns null. If this value is false, returning null from the callback will use default-location that was estimated from the file-map</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(string file, [NotNull]Func<FileMapEntry, Stream> getStreamForFileCallback, bool ignoreNullStreams, out FileMap unwrappedFiles)
        {
            if (getStreamForFileCallback == null)
            {
                throw new ArgumentNullException(nameof(getStreamForFileCallback));
            }

            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, file))
            {
                unwrappedFiles = UnTarFiles(".", helper, true, getStreamForFileCallback, null, ignoreNullStreams);
            }

            return true;
        }

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="ignoreNullStreams">indicates whether to ignore files for which the callback returns null. If this value is false, returning null from the callback will use default-location that was estimated from the file-map</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(Stream inputStream, [NotNull]Func<FileMapEntry, Stream> getStreamForFileCallback, bool ignoreNullStreams, out FileMap unwrappedFiles)
        {
            if (getStreamForFileCallback == null)
            {
                throw new ArgumentNullException(nameof(getStreamForFileCallback));
            }

            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, inputStream))
            {
                unwrappedFiles = UnTarFiles(".", helper, true, getStreamForFileCallback, null, ignoreNullStreams);
            }

            return true;
        }

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="file">the wrapped file</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(string file, Func<FileMapEntry, Stream> getStreamForFileCallback, out FileMap unwrappedFiles)
        {
            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, file))
            {
                unwrappedFiles = UnTarFiles(".", helper, true, getStreamForFileCallback);
            }

            return true;
        }

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="getStreamForFileCallback">a callback that will be called for each entry in the wrapped file</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(Stream inputStream, Func<FileMapEntry, Stream> getStreamForFileCallback, out FileMap unwrappedFiles)
        {
            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, inputStream))
            {
                unwrappedFiles = UnTarFiles(".", helper, true, getStreamForFileCallback);
            }

            return true;
        }

        /// <summary>
        /// Unwraps a file and requests for each entry a new stream providing the inner entryname
        /// </summary>
        /// <param name="inputStream">the stream that contains the data that needs to be unwrapped</param>
        /// <param name="targetDirectory">the directory in which to unwrap the files</param>
        /// <param name="unwrappedFiles">a FileMap object containing all files that have been unwrapped</param>
        /// <returns>a value indicating whether the unwrapping of the file was successful</returns>
        public override bool Unwrap(Stream inputStream, string targetDirectory, out FileMap unwrappedFiles)
        {
            using (TarStreamHelper helper = new TarStreamHelper(false, useGz, inputStream))
            {
                unwrappedFiles = UnTarFiles(targetDirectory, helper,true);
            }

            return true;
        }

        /// <summary>
        /// Unwraps a specific file from the given wrapper-file
        /// </summary>
        /// <param name="file">the wrapper file from which to extract a specific file</param>
        /// <param name="fileName">the name of the file that opened</param>
        /// <param name="fileStream">the stream representing the file in the given </param>
        /// <returns>a resource-lock object representing all open streams that require to be closed after reading the returned file</returns>
        public override IResourceLock Open(string file, string fileName, out Stream fileStream)
        {
            var helper = new TarStreamHelper(false, useGz, file);
            TarEntry te;
            do
            {
                te = helper.InputStream.GetNextEntry();
            } while (!te.File.Equals(fileName,StringComparison.OrdinalIgnoreCase));

            fileStream = helper.InputStream;
            return new DeferredDisposalHelper(helper);
        }

        /// <summary>
        /// Unwraps a specific file from the given wrapper-file
        /// </summary>
        /// <param name="inputStream">an opened stream of the wrapper-file containing the requested file</param>
        /// <param name="fileName">the name of the file that opened</param>
        /// <param name="fileStream">the stream representing the file in the given </param>
        /// <returns>a resource-lock object representing all open streams that require to be closed after reading the returned file</returns>
        public override IResourceLock Open(Stream inputStream, string fileName, out Stream fileStream)
        {
            var helper = new TarStreamHelper(false, useGz, inputStream);
            TarEntry te;
            do
            {
                te = helper.InputStream.GetNextEntry();
            } while (!te.File.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            fileStream = helper.InputStream;
            return new DeferredDisposalHelper(helper);
        }

        /// <summary>
        /// Wraps the given directory and the indexFile into a wrapper-file
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(string directory, string wrappedName)
        {
            FileMap map = new FileMap(directory, FileMap.MapDirection.FileToArchive, flattenStructure);
            WrapFiles(map, wrappedName);
        }

        /// <summary>
        /// Wraps the given IndexFile and all provided files into a wrapper file
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(string[] files, string wrappedName)
        {
            FileMap map = new FileMap(files);
            WrapFiles(map, wrappedName);
        }

        /// <summary>
        /// Creates a FileMap with the settings for this wrapper
        /// </summary>
        /// <returns>a new fileMap that has the same settings as the current wrapper</returns>
        public override FileMap CreateMap()
        {
            return new FileMap(flattenStructure);
        }

        /// <summary>
        /// Creates a FileMap reflection the content of a specific wrapper-file
        /// </summary>
        /// <param name="inputFile"></param>
        /// <returns></returns>
        public override FileMap CreateMap(string inputFile)
        {
            using (FileStream fst = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
            {
                using (TarStreamHelper helper = new TarStreamHelper(false, useGz, fst))
                {
                    return UnTarFiles(".", helper, false);
                }
            }
        }

        /// <summary>
        /// Wrapps all files in the fileMap into a tar file
        /// </summary>
        /// <param name="map">the file-map containing all files that need to be wrapped</param>
        /// <param name="wrappedName">the name of the output tar file</param>
        public override void WrapFiles(FileMap map, string wrappedName)
        {
            using (TarStreamHelper helper = new TarStreamHelper(true, useGz, wrappedName))
            {
                foreach (FileMapEntry entry in map)
                {
                    TarEntry ent = TarEntry.CreateEntryFromFile(entry.LocationInFileSystem);
                    ent.Name = entry.ArchiveFileName;
                    helper.OutputStream.PutNextEntry(ent);
                    using (Stream fs = entry.Open())
                    {
                        fs.CopyTo(helper.OutputStream);
                    }

                    helper.OutputStream.CloseEntry();
                }
            }
        }

        /// <summary>
        /// Appends the given Files to an existing Archive
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void AppendFiles(string[] files, string wrappedName)
        {
            FileMap map = new FileMap(files);
            AppendFiles(map, wrappedName);
        }

        /// <summary>
        /// Appends the Files of a map to an existing Archive
        /// </summary>
        /// <param name="map">the map that contains files which need to be added to an archive</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void AppendFiles(FileMap map, string wrappedName)
        {
            bool existing = false;
            if (File.Exists(wrappedName))
            {
                File.Move(wrappedName,$"{wrappedName}.lock");
                existing = true;
            }

            using (TarStreamHelper helper = new TarStreamHelper(true, useGz, wrappedName))
            {
                foreach (FileMapEntry entry in map)
                {
                    TarEntry ent = TarEntry.CreateEntryFromFile(entry.LocationInFileSystem);
                    ent.Name = entry.ArchiveFileName;
                    helper.OutputStream.PutNextEntry(ent);
                    using (Stream fs = entry.Open())
                    {
                        fs.CopyTo(helper.OutputStream);
                    }

                    helper.OutputStream.CloseEntry();
                }

                if (existing)
                {

                    try
                    {
                        using (var exhelper = new TarStreamHelper(false, useGz, $"{wrappedName}.lock"))
                        {
                            UnTarFiles(".", exhelper, true, e =>
                            {
                                if (!map.Contains(e.ArchiveFileName))
                                {
                                    TarEntry ent = TarEntry.CreateTarEntry(e.ArchiveFileName);
                                    helper.OutputStream.PutNextEntry(ent);
                                    return helper.OutputStream;
                                }

                                return null;
                            }, (input, output) =>
                            {
                                try
                                {
                                    input.CopyTo(output);
                                }
                                finally
                                {
                                    helper.OutputStream.CloseEntry();
                                }
                            }, true);
                        }
                    }
                    finally
                    {

                        File.Delete($"{wrappedName}.lock");
                    }
                }
            }
        }


        /// <summary>
        /// Appends the content of a folder to an existing Archive
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void AppendFiles(string directory, string wrappedName)
        {
            FileMap map = new FileMap(directory, FileMap.MapDirection.FileToArchive, flattenStructure);
            AppendFiles(map, wrappedName);
        }

        /// <summary>
        /// Untars a file by either using the target directory or streams provided by the getStreamCallback function
        /// </summary>
        /// <param name="targetDirectory">the target directory into which to export the unwrapped files</param>
        /// <param name="tarStream">the tarstream that is used to export the files</param>
        /// <param name="unpack">indicates whether to actually perform the unpacking process</param>
        /// <param name="getStreamCallback">a callback that provides streams for each unwrapped file</param>
        /// <param name="copyAction">the action to execute for copying the content of the stream to the target</param>
        /// <param name="ignoreNullStreams">indicates whether to prevent using the default-directory when getStreamCallback does return null</param>
        /// <returns>a filemap that contains all unwrapped files and their result/source paths</returns>
        private FileMap UnTarFiles(string targetDirectory, TarStreamHelper tarStream,bool unpack,
                                  [InstantHandle] Func<FileMapEntry, Stream> getStreamCallback = null, [InstantHandle]Action<Stream, Stream> copyAction = null, bool ignoreNullStreams = false)
        {
            FileMap map = new FileMap(targetDirectory, flattenStructure);
            TarEntry entry;
            if (copyAction == null)
            {
                copyAction = (input, output) =>
                {
                    try
                    {
                        input.CopyTo(output);
                    }
                    finally
                    {
                        output.Dispose();
                    }
                };
            }
                
            while ((entry = tarStream.InputStream.GetNextEntry()) != null)
            {
                if (!entry.IsDirectory)
                {
                    FileMapEntry ent = new FileMapEntry(entry.Name);
                    map.Add(ent);
                    if (unpack)
                    {

                        Stream fout = (getStreamCallback == null)
                            ? ent.Open()
                            : getStreamCallback(ent);

                        if (fout == null && !ignoreNullStreams)
                        {
                            fout = ent.Open();
                        }

                        if (fout != null)
                        {
                            copyAction(tarStream.InputStream, fout);
                        }
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// Tarclass helper used to read or write tar files and to dispose all inner streams in the right sequence
        /// </summary>
        private class TarStreamHelper : IDisposable
        {
            /// <summary>
            /// A Stack object representing all inner streams
            /// </summary>
            private Stack<Stream> subStreams;

            /// <summary>
            /// Initializes a new instance of the TarStreamHelper class
            /// </summary>
            /// <param name="create">indicates whether to create or open an archive</param>
            /// <param name="useGz">indicates whether to use gzip compression</param>
            /// <param name="fileName">provides the filename where to save data or to read data from</param>
            public TarStreamHelper(bool create, bool useGz, string fileName)
                : this(create,useGz,new FileStream(fileName, create ? FileMode.Create : FileMode.Open,
                                                create ? FileAccess.ReadWrite : FileAccess.Read), true)
            {
            }

            /// <summary>
            /// Initializes a new instance of the TarStreamHelper class
            /// </summary>
            /// <param name="create">indicates whether to create or open an archive</param>
            /// <param name="useGz">indicates whether to use gzip compression</param>
            /// <param name="sourceStream">provides the stream where to save data or to read data from</param>
            public TarStreamHelper(bool create, bool useGz, Stream sourceStream)
                : this(create, useGz, sourceStream, false)
            {
            }

            /// <summary>
            /// Initializes a new instance of the TarStreamHelper class
            /// </summary>
            /// <param name="create">indicates whether to create or open an archive</param>
            /// <param name="useGz">indicates whether to use gzip compression</param>
            /// <param name="sourceStream">provides the stream where to save data or to read data from</param>
            /// <param name="ownsStream">indicates whether the sourceStream is owned by this object</param>
            private TarStreamHelper(bool create, bool useGz, Stream sourceStream, bool ownsStream)
                : this()
            {
                if (ownsStream)
                {
                    subStreams.Push(sourceStream);
                }

                Stream parentStream = sourceStream;
                if (useGz)
                {
                    parentStream = create
                                       ? (Stream) new GZipOutputStream(sourceStream)
                                       : new GZipInputStream(sourceStream);
                    subStreams.Push(parentStream);
                }

                if (create)
                {
                    OutputStream = new TarOutputStream(parentStream);
                    subStreams.Push(OutputStream);
                }
                else
                {
                    InputStream = new TarInputStream(parentStream);
                    subStreams.Push(InputStream);
                }
            }

            /// <summary>
            /// Prevents a default instance of the TarStreamHelper class from being created
            /// </summary>
            private TarStreamHelper()
            {
                subStreams = new Stack<Stream>();
            }

            /// <summary>
            /// Gets the Tar - Archive from which to read all entries
            /// </summary>
            public TarInputStream InputStream { get; private set; }

            /// <summary>
            /// Gets the Stream used to write into the Tar-Archive
            /// </summary>
            public TarOutputStream OutputStream { get; private set; }

            /// <summary>
            /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
            /// </summary>
            /// <filterpriority>2</filterpriority>
            public void Dispose()
            {
                if (OutputStream != null)
                {
                    OutputStream.Finish();
                }

                Stream stream;
                while (subStreams.Count != 0)
                {
                    stream = subStreams.Pop();
                    stream.Dispose();
                }
            }
        }
    }
}
