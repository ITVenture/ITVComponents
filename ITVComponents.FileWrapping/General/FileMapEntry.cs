using System.IO;

namespace ITVComponents.FileWrapping.General
{
    /// <summary>
    /// Represents a FileMap-Entry
    /// </summary>
    public class FileMapEntry
    {
        /// <summary>
        /// Initializes a new instance of the FileMapEntry class
        /// </summary>
        /// <param name="archiveFileName">the name of the FileName in the archive file</param>
        public FileMapEntry(string archiveFileName) : this()
        {
            ArchiveFileName = archiveFileName;
        }

        /// <summary>
        /// Initializes a new instance of the FileMapEntry class
        /// </summary>
        /// <param name="locationInFileSystem">the location of the original file</param>
        /// <param name="archiveFileName">the target file name in the archive file</param>
        public FileMapEntry(string locationInFileSystem, string archiveFileName) : this(archiveFileName)
        {
            LocationInFileSystem = locationInFileSystem;
            Direction = FileMap.MapDirection.FileToArchive;
        }

        /// <summary>
        /// Prevents a default instance of the FileMapEntry class from being created
        /// </summary>
        private FileMapEntry()
        {
        }

        /// <summary>
        /// Gets the Location of the targetfile inside the FileSystem
        /// </summary>
        public string LocationInFileSystem { get; internal set; }

        /// <summary>
        /// Gets the Archive FileName inside the archive
        /// </summary>
        public string ArchiveFileName { get; private set; }

        /// <summary>
        /// Gets the direction of this Entry
        /// </summary>
        public FileMap.MapDirection Direction { get; internal set; }

        /// <summary>
        /// Opens the file in the fileSystem for either writing or reading depending on the Direction of this item
        /// </summary>
        /// <returns>a stream supporting the used operations</returns>
        public Stream Open()
        {
            string directoryName = Path.GetDirectoryName(LocationInFileSystem);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            return new FileStream(LocationInFileSystem,
                                  (Direction == FileMap.MapDirection.FileToArchive) ? FileMode.Open : FileMode.Create,
                                  (Direction == FileMap.MapDirection.FileToArchive) ? FileAccess.Read : FileAccess.Write);
        }

        /// <summary>
        /// Bestimmt, ob das angegebene <see cref="T:System.Object"/> und das aktuelle <see cref="T:System.Object"/> gleich sind.
        /// </summary>
        /// <returns>
        /// true, wenn das angegebene <see cref="T:System.Object"/> gleich dem aktuellen <see cref="T:System.Object"/> ist, andernfalls false.
        /// </returns>
        /// <param name="obj">Das <see cref="T:System.Object"/>, das mit dem aktuellen <see cref="T:System.Object"/> verglichen werden soll. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            bool retVal = false;
            if (obj is FileMapEntry)
            {
                FileMapEntry target = obj as FileMapEntry;
                retVal = target.LocationInFileSystem == LocationInFileSystem;
            }

            return retVal;
        }

        /// <summary>
        /// Fungiert als Hashfunktion für einen bestimmten Typ. 
        /// </summary>
        /// <returns>
        /// Ein Hashcode für das aktuelle <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            int retVal = GetHashCode();
            if (LocationInFileSystem != null)
            {
                retVal = LocationInFileSystem.GetHashCode();
            }

            return retVal;
        }
    }
}
