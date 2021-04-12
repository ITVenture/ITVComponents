using System;
using System.IO;
using ICSharpCode.SharpZipLib.GZip;
using ITVComponents.Annotations;
using ITVComponents.FileWrapping.General;
using ITVComponents.Threading;

namespace ITVComponents.FileWrapping.DefaultImplementations.GZ
{
    /// <summary>
    /// Unwrapper type capable for GZ - Files
    /// </summary>
    public class GZWrapper:WrapperStrategy
    {
        /// <summary>
        /// The unique Name of this GZ Unwrapper
        /// </summary>
        private string unwrapperType;

        /// <summary>
        /// the compression level fro this gzWrapper object
        /// </summary>
        private int compressionLevel;

        /// <summary>
        /// Initializes a new instance of the GZWrapper class
        /// </summary>
        /// <param name="compressionLevel">the compression level to use for this wrapper instance</param>
        public GZWrapper(string unwrapperType, int compressionLevel)
            : this()
        {
            this.unwrapperType = unwrapperType;
            this.compressionLevel = compressionLevel;
        }

        /// <summary>
        /// Initializes a new instance of the GZWrapperClass.
        /// </summary>
        public GZWrapper(string unwrapperType) : this(unwrapperType, 5)
        {
        }

        /// <summary>
        /// Prevents a default instance of the GZWrapper class from being created
        /// </summary>
        private GZWrapper()
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
            get { return ".gz"; }
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
            unwrappedFiles = new FileMap(Path.GetDirectoryName(file), true);
            string fileName = Path.GetFileName( file);
            int idx = fileName.LastIndexOf(".gz");
            if (idx != -1)
            {
                fileName = fileName.Substring(0, idx);
            }
            else
            {
                fileName += ".unpacked";
            }//Path.GetFileNameWithoutExtension(file);
            string path = Path.GetDirectoryName(file);
            unwrappedDirectory = string.Format(@"{0}\{1}", path, fileName);
            using (FileStream fst = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                return UnwrapFile(fst, unwrappedDirectory, unwrappedFiles, true);
            }
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
            unwrappedFiles = new FileMap(Path.GetDirectoryName(file), true);
            string fileName = Path.GetFileNameWithoutExtension(file);
            using (FileStream fst = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                return UnwrapFile(fst, fileName, unwrappedFiles, true, getStreamForFileCallback);
            }
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
            string file = "outputFile.dat.gz";
            unwrappedFiles = new FileMap(Path.GetDirectoryName(file), true);
            string fileName = Path.GetFileNameWithoutExtension(file);
            return UnwrapFile(inputStream, fileName, unwrappedFiles, true, getStreamForFileCallback);
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
            unwrappedFiles = new FileMap(Path.GetDirectoryName(targetDirectory), true);
            string fileName = Path.GetFileName(targetDirectory);
            return UnwrapFile(inputStream, fileName, unwrappedFiles, true);
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Wraps the given directory and the indexFile into a wrapper-file
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(string directory, string wrappedName)
        {
            directory = (!string.IsNullOrEmpty(directory)) ? directory : null;
            if (directory == null)
            {
                throw new ArgumentException("No File provided");
            }

            Wrap(directory, wrappedName);
        }

        /// <summary>
        /// The FileMap that is used in order to create an archive
        /// </summary>
        /// <param name="map">the map that contains files which need to be added to an archive</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(FileMap map, string wrappedName)
        {
            if (map.Count > 1)
            {
                throw new ArgumentException("Too many Files");
            }

            Wrap(map[0].LocationInFileSystem, wrappedName);
        }

        /// <summary>
        /// Wraps the given IndexFile and all provided files into a wrapper file
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void WrapFiles(string[] files, string wrappedName)
        {
            string file;
            if (files != null && files.Length > 1)
            {
                throw new ArgumentException("Too many Files");
            }
            else if (files != null && files.Length == 1)
            {
                file = files[0];
            }
            else
            {
                throw new ArgumentException("No File provided");
            }

            Wrap(file, wrappedName);
        }

        /// <summary>
        /// Appends the content of a folder to an existing Archive
        /// </summary>
        /// <param name="directory">the directory containing the items that need to be wrapped</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void AppendFiles(string directory, string wrappedName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Appends the Files of a map to an existing Archive
        /// </summary>
        /// <param name="map">the map that contains files which need to be added to an archive</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void AppendFiles(FileMap map, string wrappedName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Appends the given Files to an existing Archive
        /// </summary>
        /// <param name="files">the files that need to be wrapped into the provided file</param>
        /// <param name="wrappedName">the name of the taret wrapper-file</param>
        public override void AppendFiles(string[] files, string wrappedName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a FileMap with the settings for this wrapper
        /// </summary>
        /// <returns>a new fileMap that has the same settings as the current wrapper</returns>
        public override FileMap CreateMap()
        {
            return new FileMap(true);
        }


        /// <summary>
        /// Creates a FileMap reflection the content of a specific wrapper-file
        /// </summary>
        /// <param name="inputFile"></param>
        /// <returns></returns>
        public override FileMap CreateMap(string inputFile)
        {
            FileMap retVal = new FileMap(true);
            using (FileStream fst = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
            {
                UnwrapFile(fst, $@"{Path.GetDirectoryName(inputFile)}\{Path.GetFileNameWithoutExtension(inputFile)}", retVal, false);
                return retVal;
            }
        }

        /// <summary>
        /// Unwraps the provided file with either a callback or the target directory
        /// </summary>
        /// <param name="wrapperFile">the file that contains the wrapped content</param>
        /// <param name="targetFile">the target file into which the content is unwrapped</param>
        /// <param name="unwrappedFiles">a fileMap that contains the unwrapped item</param>
        /// <param name="performUnzip">indicates whether to actually perform the unpacking process</param>
        /// <param name="requestStream">a callback that is used to create a new stream if required</param>
        /// <returns>a value indicating whether the unwrapping was successful</returns>
        private bool UnwrapFile(Stream wrapperFile, string targetFile, FileMap unwrappedFiles, bool performUnzip,
                                Func<string, Stream> requestStream = null)
        {
            FileMapEntry ent;
            unwrappedFiles.Add(ent = new FileMapEntry(Path.GetFileName(targetFile)));
            if (performUnzip)
            {
                using (GZipInputStream gzis = new GZipInputStream(wrapperFile))
                {
                    using (
                        /*Stream fso = (requestStream == null)
                                     ? new FileStream(targetFile, FileMode.Create, FileAccess.ReadWrite)
                                     : (requestStream(targetFile) ??
                                        new FileStream(targetFile, FileMode.Create, FileAccess.ReadWrite)))*/
                        var fso = ent.Open())
                    {
                        gzis.CopyTo(fso);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Wraps a file into the target gz file
        /// </summary>
        /// <param name="inputFile">the original file</param>
        /// <param name="outputFile">the output file</param>
        private void Wrap(string inputFile, string outputFile)
        {
            using (FileStream fsi = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
            {
                using (FileStream fso = new FileStream(outputFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    using (GZipOutputStream gzo = new GZipOutputStream(fso))
                    {
                        gzo.SetLevel(compressionLevel);
                        fsi.CopyTo(gzo);
                    }
                }
            }
        }
    }
}
