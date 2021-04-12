using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
 
namespace ITVComponents.FileWrapping.General
{
    /// <summary>
    /// Represents the wrapped directory with its whole content
    /// </summary>
    public class FileMap:ICollection<FileMapEntry>
    {
        /// <summary>
        /// the entries that were found inside a directory
        /// </summary>
        private List<FileMapEntry> entries;
 
        /// <summary>
        /// the direction of this FileMap
        /// </summary>
        private MapDirection direction;
 
        /// <summary>
        /// Indicates whether to flatten the hirarchy
        /// </summary>
        private bool flattenStructure;
 
        /// <summary>
        /// the Transformer used to translate one name to an other
        /// </summary>
        private INameTransform transformer;
 
        /// <summary>
        /// The root directory of this fileMap
        /// </summary>
        private string rootDir;
 
        /// <summary>
        /// Initializes a new instance of the rootDirectory class
        /// </summary>
        /// <param name="rootDirectory">the root directory of all items</param>
        /// <param name="flattenStructure">indicates whether to put all files into the root of the directory/the archive</param>
        public FileMap(string rootDirectory, bool flattenStructure)
            : this(rootDirectory, MapDirection.ArchiveToFile, flattenStructure)
        {
        }
 
        /// <summary>
        /// Initializes a new instance of the FileMap class
        /// </summary>
        /// <param name="files">the files that will be added to the archive file</param>
        public FileMap(string[] files)
            : this()
        {
            RootDir = GetTopMostDirectory(files);
            direction = MapDirection.FileToArchive;
            flattenStructure = false;
            Prepare(false);
            entries.AddRange((from t in files select new FileMapEntry(Transform(t)){LocationInFileSystem=t, Direction = direction}));
        }
 
        /// <summary>
        /// Initializes a new instance of the rootDirectory class
        /// </summary>
        /// <param name="rootDirectory">the root directory of all items</param>
        /// <param name="direction">the direction used by this FileMap</param>
        /// <param name="flattenStructure">indicates whether to put all files into the root of the directory/the archive</param>
        public FileMap(string rootDirectory,  MapDirection direction, bool flattenStructure)
            : this()
        {
            RootDir = rootDirectory;
            this.direction = direction;
            this.flattenStructure = flattenStructure;
            Prepare(true);
        }
 
        /// <summary>
        /// Initializes a new instance of the rootDirectory class
        /// </summary>
        /// <param name="flattenStructure">indicates whether to put all files into the root of the directory/the archive</param>
        public FileMap(bool flattenStructure)
            : this()
        {
            direction = MapDirection.FileToArchive;
            this.flattenStructure = flattenStructure;
            Prepare(false);
        }
 
        /// <summary>
        /// Prevents a default instance of the FileMap class from being created
        /// </summary>
        private FileMap()
        {
            entries = new List<FileMapEntry>();
        }
 
        /// <summary>
        /// Gets the file at a specific index
        /// </summary>
        /// <param name="index">the index of the desired file</param>
        /// <returns>a fileMapEntry that is located at the specified index</returns>
        public FileMapEntry this[int index]
        {
            get { return entries[index]; }
        }
 
        /// <summary>
        /// Gets the root directory of the filemap
        /// </summary>
        public string RootDir
        {
            get { return rootDir; }
            private set
            {
                if (entries.Count != 0)
                {
                    throw new InvalidOperationException("Root directory can only be set on empty maps");
                }
 
                rootDir = value;
            }
        }
 
        /// <summary>
        /// Ruft die Anzahl der Elemente ab, die in <see cref="T:System.Collections.Generic.ICollection`1"/> enthalten sind.
        /// </summary>
        /// <returns>
        /// Die Anzahl der Elemente, die in <see cref="T:System.Collections.Generic.ICollection`1"/> enthalten sind.
        /// </returns>
        public int Count
        {
            get { return entries.Count; }
        }
 
        /// <summary>
        /// Ruft einen Wert ab, der angibt, ob <see cref="T:System.Collections.Generic.ICollection`1"/> schreibgeschützt ist.
        /// </summary>
        /// <returns>
        /// true, wenn <see cref="T:System.Collections.Generic.ICollection`1"/> schreibgeschützt ist, andernfalls false.
        /// </returns>
        public bool IsReadOnly
        {
            get { return false; }
        }
 
        /// <summary>
        /// Gibt einen Enumerator zurück, der die Auflistung durchläuft.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.Collections.Generic.IEnumerator`1"/>, der zum Durchlaufen der Auflistung verwendet werden kann.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<FileMapEntry> GetEnumerator()
        {
            return entries.GetEnumerator();
        }
 
        /// <summary>
        /// Gibt einen Enumerator zurück, der eine Auflistung durchläuft.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.Collections.IEnumerator"/>-Objekt, das zum Durchlaufen der Auflistung verwendet werden kann.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
 
        /// <summary>
        /// Fügt der <see cref="T:System.Collections.Generic.ICollection`1"/> ein Element hinzu.
        /// </summary>
        /// <param name="item">Das Objekt, das <see cref="T:System.Collections.Generic.ICollection`1"/> hinzugefügt werden soll.</param><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> ist schreibgeschützt.</exception>
        public void Add(FileMapEntry item)
        {
            if (string.IsNullOrEmpty(item.LocationInFileSystem))
            {
                item.LocationInFileSystem = Transform(item.ArchiveFileName.Replace("..",""));
                item.Direction = direction;
            }
            else
            {
                if (item.Direction != direction)
                {
                    throw new InvalidOperationException(string.Format("The Item has a wrong direction. Expected:{0}, Actual{1}",direction,item.Direction));
                }
            }
 
            if (entries.Contains(item))
            {
                throw new Exception("There already is a file with the given Filesystem-path");
            }
 
            entries.Add(item);
        }
 
        /// <summary>
        /// Adds a list of files to this array
        /// </summary>
        /// <param name="files">the array that contains files that need to be added to an archive</param>
        public void AddRange(string[] files)
        {
            entries.AddRange(from t in files select new FileMapEntry(Transform(t)) {LocationInFileSystem = t, Direction = direction});
        }

        /// <summary>
        /// Adds a list of files to this array
        /// </summary>
        /// <param name="files">the array that contains file-mappings that need to be added to an archive</param>
        public void AddRange(FileMapEntry[] files)
        {
            entries.AddRange(files);
        }
 
        /// <summary>
        /// Entfernt alle Elemente aus <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> ist schreibgeschützt. </exception>
        public void Clear()
        {
            entries.Clear();
        }
 
        /// <summary>
        /// Bestimmt, ob <see cref="T:System.Collections.Generic.ICollection`1"/> einen bestimmten Wert enthält.
        /// </summary>
        /// <returns>
        /// true, wenn sich <paramref name="item"/> in <see cref="T:System.Collections.Generic.ICollection`1"/> befindet, andernfalls false.
        /// </returns>
        /// <param name="item">Das im <see cref="T:System.Collections.Generic.ICollection`1"/> zu suchende Objekt.</param>
        public bool Contains(FileMapEntry item)
        {
            return entries.Contains(item);
        }

        /// <summary>
        /// Gets a value indicating whether a specific internal name exists in this map
        /// </summary>
        /// <param name="path">the name to check for existence</param>
        /// <returns>a value indicating whether the given path is part of this map</returns>
        public bool Contains(string path)
        {
            return entries.Any(e => e.ArchiveFileName.Equals(path, StringComparison.OrdinalIgnoreCase));
        }
 
        /// <summary>
        /// Kopiert die Elemente von <see cref="T:System.Collections.Generic.ICollection`1"/> in ein <see cref="T:System.Array"/>, beginnend bei einem bestimmten <see cref="T:System.Array"/>-Index.
        /// </summary>
        /// <param name="array">Das eindimensionale <see cref="T:System.Array"/>, das das Ziel der aus <see cref="T:System.Collections.Generic.ICollection`1"/> kopierten Elemente ist.Für <see cref="T:System.Array"/> muss eine nullbasierte Indizierung verwendet werden.</param><param name="arrayIndex">Der nullbasierte Index in <paramref name="array"/>, an dem das Kopieren beginnt.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> hat den Wert null.</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> ist kleiner als 0.</exception><exception cref="T:System.ArgumentException"><paramref name="array"/> ist mehrdimensional.- oder -Die Anzahl der Elemente in der Quelle <see cref="T:System.Collections.Generic.ICollection`1"/> ist größer als der verfügbare Speicherplatz ab <paramref name="arrayIndex"/> bis zum Ende des <paramref name="array"/>, das als Ziel festgelegt wurde.- oder -Typ <paramref name="T"/> kann nicht automatisch in den Typ des Ziel-<paramref name="array"/> umgewandelt werden.</exception>
        public void CopyTo(FileMapEntry[] array, int arrayIndex)
        {
            entries.CopyTo(array, arrayIndex);
        }
 
        /// <summary>
        /// Entfernt das erste Vorkommen eines bestimmten Objekts aus <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true, wenn <paramref name="item"/> erfolgreich aus <see cref="T:System.Collections.Generic.ICollection`1"/> gelöscht wurde, andernfalls false.Diese Methode gibt auch dann false zurück, wenn <paramref name="item"/> nicht in der ursprünglichen <see cref="T:System.Collections.Generic.ICollection`1"/> gefunden wurde.
        /// </returns>
        /// <param name="item">Das aus dem <see cref="T:System.Collections.Generic.ICollection`1"/> zu entfernende Objekt.</param><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> ist schreibgeschützt.</exception>
        public bool Remove(FileMapEntry item)
        {
            return entries.Remove(item);
        }

        /// <summary>
        /// Switches all items in this map to the desired direction
        /// </summary>
        /// <param name="targetDirection">the desired direction for this map</param>
        public void SwitchMap(MapDirection targetDirection)
        {
            direction = targetDirection;
            foreach (FileMapEntry entry in entries)
            {
                entry.Direction = targetDirection;
            }
        }
 
        /// <summary>
        /// Prepares this FileMap for further use
        /// </summary>
        /// <param name="fill">indicates whether to fill the entries</param>
        private void Prepare(bool fill)
        {
            if (direction == MapDirection.FileToArchive)
            {
                transformer = new ZipNameTransform(RootDir);
                if (fill && Directory.Exists(RootDir))
                {
                    GetAllFiles(RootDir, entries);
                }
            }
            else
            {
                transformer = new WindowsNameTransform(RootDir);
            }
        }
 
        /// <summary>
        /// Fills the list of entries from the rootdirectoryin the filesystem
        /// </summary>
        /// <param name="rootDirectory">the directory from which to fill the items</param>
        /// <param name="entries">a list containing all known items</param>
        private void GetAllFiles(string rootDirectory, List<FileMapEntry> entries)
        {
            string[] directories = Directory.GetDirectories(rootDirectory);
            string[] files = Directory.GetFiles(rootDirectory);
                files.Select(
                    file => new FileMapEntry(Transform(file)) {LocationInFileSystem = file, Direction = direction}).ToList().ForEach(
                        n =>
                            {
                                if (!entries.Contains(n))
                                {
                                    entries.Add(n);
                                }
                            });
            foreach (string directory in directories)
            {
                GetAllFiles(directory, entries);
            }
        }
 
        /// <summary>
        /// Transforms the provided filename into the appropriate name in the filesystem/archive
        /// </summary>
        /// <param name="fileName">the filename of the file either in the archive or in the filesystem</param>
        /// <returns>the name of the file in the direction of this fileMap (archive->fs/fs->archive)</returns>
        private string Transform(string fileName)
        {
            if (!flattenStructure)
            {
                return transformer.TransformFile(fileName);
            }
 
            if (direction == MapDirection.ArchiveToFile)
            {
                int id = fileName.LastIndexOf("/");
                if (id != -1)
                {
                    fileName = fileName.Substring(id + 1);
                }
 
                return string.Format(@"{0}\{1}", RootDir, fileName);
            }
 
            return Path.GetFileName(fileName);
        }
 
        /// <summary>
        /// Gets the root directory depending on a set of files.
        /// </summary>
        /// <param name="files">the set of files from which to get the root</param>
        /// <returns>the topmost directory containing all given files</returns>
        private string GetTopMostDirectory(IEnumerable<string> files)
        {
            var roots = (from f in files select (from t in f.ToLower().Split('\\') where t.Trim() != "" select t).ToArray()).ToArray();
            StringBuilder bld = new StringBuilder();
            int min = (from t in roots orderby t.Length ascending select t.Length).First() - 1;
            bool ok = true;
            for (int i = 0; ok && i < min; i++)
            {
                string s = roots[0][i];
                for (int a = 1; ok && a < roots.Length; a++)
                {
                    ok &= roots[a][i] == s;
                }
 
                bld.AppendFormat("{0}\\", s);
            }
 
            return bld.ToString(0, bld.Length - 1);
        }
 
        /// <summary>
        /// Enumeration used to determine the direction of a FileMap
        /// </summary>
        public enum MapDirection
        {
            /// <summary>
            /// Indicates that a mapper provides the paths used to put files from an archive into the filesystem
            /// </summary>
            ArchiveToFile,
 
            /// <summary>
            /// Indicates that a mapper provides the paths used to put files from the archive into an archive
            /// </summary>
            FileToArchive
        }
    }
}