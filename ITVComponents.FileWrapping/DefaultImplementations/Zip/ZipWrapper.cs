using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using ITVComponents.Annotations;
using ITVComponents.FileWrapping.General;
using ITVComponents.FileWrapping.Helpers;
using ITVComponents.Logging;
using ITVComponents.Threading;

namespace ITVComponents.FileWrapping.DefaultImplementations.Zip
{
    /// <summary>
    /// A ZipWrapper used to unwrap zip files
    /// </summary>
    public class ZipWrapper : WrapperStrategy
    {
        /// <summary>
        /// the unwrapper identifyer of this unwrapper
        /// </summary>
        private string unwrapperType;

        /// <summary>
        /// Indicates whether to put all files into the direct output root regardless of the zip's directory structure
        /// </summary>
        private bool flattenStructure;

        /// <summary>
        /// the compressionlevel for this object
        /// </summary>
        private int compressionLevel;

        /// <summary>
        /// Initializes a new instance of the ZipWrapper class
        /// </summary>
        /// <param name="unwrapperType">the type identifier for this unwrapper</param>
        /// <param name="compressionLevel">the compression level for wrapping actions</param>
        public ZipWrapper(string unwrapperType, int compressionLevel)
            : this(unwrapperType, false)
        {
            this.compressionLevel = compressionLevel;
        }

        /// <summary>
        /// Initializes a new instance of the ZipWrapper class
        /// </summary>
        /// <param name="unwrapperType">the type identifier for this unwrapper</param>
        public ZipWrapper(string unwrapperType):this(unwrapperType, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ZipWrapper class
        /// </summary>
        /// <param name="unwrapperType">the type identifier for this unwrapper</param>
        /// <param name="flattenStructure">Indicates whether to put all files into the direct output root regardless of the zip's directory structure</param>
        /// <param name="compressionLevel">the compression level for wrapping actions</param>
        public ZipWrapper(string unwrapperType, bool flattenStructure, int compressionLevel)
            : this(unwrapperType, flattenStructure)
        {
            this.compressionLevel = compressionLevel;
        }

        /// <summary>
        /// Initializes a new instance of the ZipWrapper class
        /// </summary>
        /// <param name="unwrapperType">the type identifier for this unwrapper</param>
        /// <param name="flattenStructure">Indicates whether to put all files into the direct output root regardless of the zip's directory structure</param>
        public ZipWrapper(string unwrapperType,bool flattenStructure):this()
        {
            this.unwrapperType = unwrapperType;
            this.flattenStructure = flattenStructure;
        }

        /// <summary>
        /// Prevents a default instance of the ZipWrapper class from being created
        /// </summary>
        private ZipWrapper()
        {
            compressionLevel = 5;
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
            get { return ".zip"; }
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
            string fileName = Path.GetFileNameWithoutExtension(file);
            string path = Path.GetDirectoryName(file);
            unwrappedDirectory = string.Format(@"{0}\{1}", path, fileName);
            try
            {
                using (FileStream fst = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    unwrappedFiles = UnwrapZip(fst, unwrappedDirectory, true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }
            unwrappedFiles = null;
            return false;
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
            try
            {
                using (FileStream fst = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    if (getStreamForFileCallback != null)
                    {
                        unwrappedFiles = UnwrapZip(fst, ".", true, n => getStreamForFileCallback(n.ArchiveFileName));
                    }
                    else
                    {
                        unwrappedFiles = UnwrapZip(fst, ".", true);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }
            unwrappedFiles = null;
            return false;
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
            try
            {
                if (getStreamForFileCallback != null)
                {
                    unwrappedFiles = UnwrapZip(inputStream, ".", true, n => getStreamForFileCallback(n.ArchiveFileName));
                }
                else
                {
                    unwrappedFiles = UnwrapZip(inputStream, ".", true);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }

            unwrappedFiles = null;
            return false;
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
            try
            {
                unwrappedFiles = UnwrapZip(inputStream, targetDirectory, true);
                return true;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }

            unwrappedFiles = null;
            return false;
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

            try
            {
                unwrappedFiles = UnwrapZip(inputStream, ".", true, getStreamForFileCallback, null, ignoreNullStreams);
                return true;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }

            unwrappedFiles = null;
            return false;
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

            try
            {
                using (FileStream fst = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    unwrappedFiles = UnwrapZip(fst, ".", true, getStreamForFileCallback, null, ignoreNullStreams);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }
            unwrappedFiles = null;
            return false;
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
            try
            {
                unwrappedFiles = UnwrapZip(inputStream, ".", true, getStreamForFileCallback);
                return true;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }
            unwrappedFiles = null;
            return false;
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
            try
            {
                using (FileStream fst = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    unwrappedFiles = UnwrapZip(fst, ".", true, getStreamForFileCallback);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }
            unwrappedFiles = null;
            return false;
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
            ZipFile fl = new ZipFile(file);
            var entry = fl.GetEntry(fileName);
            fileStream = fl.GetInputStream(entry);
            return new DeferredDisposalHelper(fl, fileStream);
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
            ZipFile fl = new ZipFile(inputStream);
            var entry = fl.GetEntry(fileName);
            fileStream = fl.GetInputStream(entry);
            return new DeferredDisposalHelper(fl, fileStream);
        }

        /// <summary>
        /// Wraps the given directory and the indexFile into a wrapper-file
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(string directory, string wrappedName)
        {
            FileMap map = new FileMap(directory, FileMap.MapDirection.FileToArchive, flattenStructure);
            WrapFiles(map,wrappedName);
        }

        /// <summary>
        /// Wraps the given directory and the indexFile into a wrapper-file
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        /// <param name="password">the password that is used for encryption</param>
        public void WrapFiles(string directory, string wrappedName, string password)
        {
            FileMap map = new FileMap(directory, FileMap.MapDirection.FileToArchive, flattenStructure);
            WrapFiles(map, wrappedName, password);
        }

        /// <summary>
        /// Wraps the given IndexFile and all provided files into a wrapper file
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(string[] files, string wrappedName)
        {
            if (files == null || files.Length == 0)
            {
                throw new ArgumentException("No files selected for wrapping");
            }

            FileMap map = new FileMap(files);
            WrapFiles(map, wrappedName);
        }

        /// <summary>
        /// Wraps the given IndexFile and all provided files into a wrapper file
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        /// <param name="password">the password that is used for encryption</param>
        public void WrapFiles(string[] files, string wrappedName, string password)
        {
            if (files == null || files.Length == 0)
            {
                throw new ArgumentException("No files selected for wrapping");
            }

            FileMap map = new FileMap(files);
            WrapFiles(map, wrappedName, password);
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
            AppendFiles(map, wrappedName, null);
        }

        /// <summary>
        /// Appends the Files of a map to an existing Archive
        /// </summary>
        /// <param name="map">the map that contains files which need to be added to an archive</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        /// <param name="password">the password for the existing and the new zip</param>
        public void AppendFiles(FileMap map, string wrappedName, string password)
        {
            bool existing = false;
            if (File.Exists(wrappedName))
            {
                File.Move(wrappedName, $"{wrappedName}.lock");
                existing = true;
            }


            using (FileStream fst = new FileStream(wrappedName, FileMode.Create, FileAccess.ReadWrite))
            {
                var keySize = password != null ? 256 : 0;
                using (ZipOutputStream zos = new ZipOutputStream(fst))
                {
                    if (password != null)
                    {
                        zos.Password = password;
                    }

                    zos.SetLevel(compressionLevel);
                    ZipEntryFactory fac = new ZipEntryFactory {IsUnicodeText = true};
                    LogEnvironment.LogDebugEvent(null, (from t in map
                        select
                            WriteEntry(t,
                                fac.MakeFileEntry(t.LocationInFileSystem, t.ArchiveFileName, true),
                                zos, keySize)).Count().ToString(), (int) LogSeverity.Report, null);

                    if (existing)
                    {
                        using (FileStream fsti = new FileStream($"{wrappedName}.lock", FileMode.Open, FileAccess.Read))
                        {
                            UnwrapZip(fsti, ".", true, oent =>
                            {
                                if (!map.Contains(oent.ArchiveFileName))
                                {
                                    var net = fac.MakeFileEntry(oent.ArchiveFileName, false);
                                    if (keySize > 0)
                                    {
                                        net.AESKeySize = keySize;
                                    }

                                    zos.PutNextEntry(net);
                                    return zos;
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
                                    zos.CloseEntry();
                                }
                            }, true, null);
                        }
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
                return UnwrapZip(fst, ".", false);
            }
        }

        /// <summary>
        /// Wraps the given IndexFile and all provided files into a wrapper file
        /// </summary>
        /// <param name="map">the FileMap used to map the files from FileSystem to the archive file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        /// <param name="password">the password that is used for encryption</param>
        public void WrapFiles(FileMap map, string wrappedName, string password)
        {
            using (FileStream fst = new FileStream(wrappedName, FileMode.Create, FileAccess.ReadWrite))
            {
                using (ZipOutputStream zos = new ZipOutputStream(fst))
                {
                    if (password != null)
                    {
                        zos.Password = password;
                    }

                    zos.SetLevel(compressionLevel);
                    ZipEntryFactory fac = new ZipEntryFactory{IsUnicodeText = true};
                    LogEnvironment.LogDebugEvent(null, (from t in map
                        select
                            WriteEntry(t,
                                fac.MakeFileEntry(t.LocationInFileSystem, t.ArchiveFileName, true),
                                zos, password != null ? 256 : 0)).Count().ToString(), (int) LogSeverity.Report, null);
                }
            }
        }

        /// <summary>
        /// Wraps the given IndexFile and all provided files into a wrapper file
        /// </summary>
        /// <param name="map">the FileMap used to map the files from FileSystem to the archive file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(FileMap map, string wrappedName)
        {
            WrapFiles(map, wrappedName, null);
        }

        /// <summary>
        /// Writes an entry from an original file into the given zip file
        /// </summary>
        /// <param name="originalFile">the original file that is located in the filesystem</param>
        /// <param name="entry">an instance representing the zip-entry that is to be written</param>
        /// <param name="outputStream">the output stream in which to put the provided item</param>
        /// <param name="keySize">the keySize of the Zip Entry that is written</param>
        /// <returns>a value indicating whether the creation of the entry was successful(always true...)</returns>
        private bool WriteEntry(FileMapEntry originalFile, ZipEntry entry, ZipOutputStream outputStream, int keySize)
        {
            try
            {
                if (keySize > 0)
                {
                    entry.AESKeySize = keySize;
                }
                outputStream.PutNextEntry(entry);
                using (Stream inputStream = originalFile.Open())
                {
                    inputStream.CopyTo(outputStream);
                }
            }
            finally
            {
                outputStream.CloseEntry();
            }
            return true;
        }

        /// <summary>
        /// Unwraps a zip into a given directory
        /// </summary>
        /// <param name="zipFile">the zip-file to unwrap into the targetdirectory</param>
        /// <param name="targetDirectory">the targetdirectory in which to extract all files</param>
        /// <param name="performUnzip">indicates whether to perform the actual unpacking process</param>
        /// <param name="getStreamCallback">callback that is used to provide a stream for unwrapped content</param>
        /// <param name="copyAction">An action defining what happens while copying the content of the Stream to the target file</param>
        /// <param name="ignoreNullStreams">indicates whether to prevent files, for which the getStreamCallback method returns null, from being unwrapped</param>
        /// <param name="password">the password that is used to access the zip file</param>
        /// <returns>an array containing all extracted filenames</returns>
        private FileMap UnwrapZip(Stream zipFile, string targetDirectory, bool performUnzip, Func<FileMapEntry, Stream> getStreamCallback = null, Action<Stream, Stream> copyAction = null, bool ignoreNullStreams = false, string password = null)
        {
            FileMap map = new FileMap(targetDirectory, flattenStructure);
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
            using (ZipFile fl = new ZipFile(zipFile))
            {
                if (password != null)
                {
                    fl.Password = password;
                }

                foreach (ZipEntry ent in fl)
                {
                    if (!ent.IsDirectory)
                    {
                        FileMapEntry entry = new FileMapEntry(ent.Name);
                        map.Add(entry);
                        if (performUnzip)
                        {
                            using (Stream s = fl.GetInputStream(ent))
                            {

                                Stream fs = (getStreamCallback == null)
                                    ? entry.Open()
                                    : getStreamCallback(entry);

                                if (fs == null && !ignoreNullStreams)
                                {
                                    fs =  entry.Open();
                                }

                                if (fs != null)
                                {
                                    copyAction(s, fs);
                                }
                            }
                        }
                    }
                }
            }

            return map;
        }
    }
}
