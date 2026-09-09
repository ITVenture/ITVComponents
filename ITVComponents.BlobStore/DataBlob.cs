using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.BlobStore.Helpers;
using ITVComponents.BlobStore.Internal;
using ITVComponents.Helpers;
using ITVComponents.Json;
using File = ITVComponents.BlobStore.Model.File;

namespace ITVComponents.BlobStore
{
    public class DataBlob:IDisposable
    {
        private readonly bool writeFileListOnFlush;
        private readonly int blockSize;
        private Stream targetStream;
        private BlockBroker broker;
        private List<FileListModel> files = new List<FileListModel>();
        private List<FileListModel> dirtyFiles = new List<FileListModel>();
        public DataBlob(string fileName, bool writeFileListOnFlush = true, int blockSize = 4096)
        {
            this.writeFileListOnFlush = writeFileListOnFlush;
            this.blockSize = blockSize;
            targetStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            broker = new BlockBroker(targetStream);
            LoadFileList();
        }

        public File[] Files
        {
            get
            {
                lock (files)
                {
                    return (from t in files where !t.Deleted select new File(t))
                        .ToArray();
                }
            }
        }

        public IEnumerable<File> FilterFiles(Func<File, bool> filter)
        {
            lock (files)
            {
                return from t in files let tmp = new File(t) where !t.Deleted && filter(tmp) select tmp;
            }
        }

        public Stream OpenFile(Model.File file, OpenFileMode mode)
        {
            lock (files)
            {
                var fileRecord = files.First(n => n.Id == file.Id);
                while (fileRecord.LinkId != Guid.Empty)
                {
                    fileRecord = files.First(n => n.Id == fileRecord.LinkId);
                }

                var retVal = OpenBlobStream(fileRecord, blockSize, mode);
                return retVal;
            }
        }

        public Model.File NewFile(string name)
        {
            lock (files)
            {
                if (files.Any(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("File already exists in this archive");
                }
            }

            FileListModel newFile = null;
            lock (files)
            {
                newFile = files.FirstOrDefault(n => n.Deleted) ?? new FileListModel { Id = Guid.NewGuid() };
            }

            newFile.Name = name;
            newFile.CreatedUtc = DateTime.UtcNow;
            newFile.ModifiedUtc = DateTime.UtcNow;
            if (newFile.Deleted)
            {
                newFile.Deleted = false;
                newFile.LinkId = Guid.Empty;
                newFile.MarkDirty();
            }
            else
            {
                lock (files)
                {
                    files.Add(newFile);
                    dirtyFiles.Add(newFile);
                }

                newFile.Dirty += (sender, args) =>
                {
                    lock (files)
                    {
                        dirtyFiles.AddIfMissing((FileListModel)sender);
                    }
                };
            }

            return new File(newFile);
        }

        public Model.File LinkFile(File original, string name)
        {
            FileListModel newFile = null;
            lock (files)
            {
                newFile = files.FirstOrDefault(n => n.Deleted) ?? new FileListModel
                {
                    Id = Guid.NewGuid(), LinkId = original.LinkedFile == Guid.Empty ? original.Id : original.LinkedFile
                };
            }

            newFile.Name = name;
            if (newFile.Deleted)
            {
                newFile.Deleted = false;
                newFile.LinkId = original.LinkedFile == Guid.Empty ? original.Id : original.LinkedFile;
                newFile.MarkDirty();
            }
            else
            {
                lock (files)
                {
                    files.Add(newFile);
                    dirtyFiles.Add(newFile);
                }

                newFile.Dirty += (sender, args) =>
                {
                    lock (files)
                    {
                        dirtyFiles.AddIfMissing((FileListModel)sender);
                    }
                };
            }

            return new File(newFile);
        }

        public bool DropFile(Model.File file)
        {
            var innerFile = files.FirstOrDefault(n => n.Id == file.Id);
            if (innerFile != null)
            {
                innerFile.Deleted = true;
                innerFile.MarkDirty();
                return true;
            }

            return false;
        }

        private void LoadFileList()
        {
            bool success = true;
            using (var s = OpenBlobStream(0, 2048, OpenFileMode.Read))
            {
                using (BinaryReader rd = new BinaryReader(s, Encoding.Default, true))
                {
                    long numberOfFiles = 0;
                    try
                    {
                        numberOfFiles = rd.ReadInt64();
                    }
                    catch
                    {
                        numberOfFiles = 0;
                        success = false;
                    }

                    lock (files)
                    {
                        files.Clear();
                    }

                    for (long i = 0; i < numberOfFiles; i++)
                    {
                        var file = new FileListModel
                        {
                            Name = rd.ReadString(),
                            Size = rd.ReadInt64(),
                            FirstBlock = rd.ReadInt64(),
                            Id = new Guid(rd.ReadBytes(16)),
                            LinkId = new Guid(rd.ReadBytes(16)),
                            Deleted = rd.ReadBoolean(),
                            CreatedUtc =
                                DateTime.SpecifyKind(DateTime.FromBinary(rd.ReadInt64()), DateTimeKind.Utc),
                            ModifiedUtc = DateTime.SpecifyKind(DateTime.FromBinary(rd.ReadInt64()),
                                DateTimeKind.Utc),
                            Hash = rd.ReadBytes(32)
                        };
                        lock (files)
                        {
                            files.Add(file);
                        }

                        file.Dirty += (sender, args) =>
                        {
                            lock (files)
                            {
                                dirtyFiles.AddIfMissing((FileListModel)sender);
                            }
                        };
                    }
                }
                /*files = JsonHelper.ReadObject<List<FileListModel>>(dfs,
                    SerializationTypingMode.StaticTyping); //FileListModel.LoadFromStream(s).ToList();*/
            }

            if (!success)
            {
                WriteFileList(true);
            }
        }

        private void WriteFileList(bool force)
        {
            if (force || dirtyFiles.Count > 0)
            {
                using (var s = OpenBlobStream(0, blockSize / 2, OpenFileMode.Write))
                {

                    //JsonHelper.WriteObject(files, SerializationTypingMode.StaticTyping, dfs);
                    using (BinaryWriter wr = new BinaryWriter(s, Encoding.Default, true))
                    {
                        lock (files)
                        {
                            var tmp = files.Where(n => n.LinkId != Guid.Empty).ToArray();
                            foreach (var file in tmp)
                            {
                                var target = files.First(n => n.Id == file.LinkId);
                                while (target.LinkId != Guid.Empty)
                                {
                                    target = files.First(n => n.Id == target.LinkId);
                                }

                                file.CreatedUtc = target.CreatedUtc;
                                file.Hash = target.Hash;
                                file.Size = target.Size;
                                file.ModifiedUtc = target.ModifiedUtc;
                            }


                            wr.Write((long)files.Count);
                            foreach (var file in files)
                            {
                                wr.Write(file.Name);
                                wr.Write(file.Size);
                                wr.Write(file.FirstBlock);
                                wr.Write(file.Id.ToByteArray());
                                wr.Write(file.LinkId.ToByteArray());
                                wr.Write(file.Deleted);
                                wr.Write(file.CreatedUtc.ToBinary());
                                wr.Write(file.ModifiedUtc.ToBinary());
                                wr.Write(file.Hash ?? new byte[32]);
                            }
                        }

                        wr.Flush();
                    }

                    s.Flush();
                }

                dirtyFiles.Clear();
            }
        }

        private void WriteFileList()
        {
            if (writeFileListOnFlush)
                WriteFileList(false);
        }

        private Stream OpenBlobStream(FileListModel file, int blockSize, OpenFileMode mode)
        {
            Stream retVal = null;
            lock (files)
            {
                retVal = new BlobStream(file, broker, blockSize, mode == OpenFileMode.Write? WriteFileList: () => { },
                     mode == OpenFileMode.Write ? CalculateFileHash : (f) => { });
            }

            return WrapStream(retVal, mode);
        }

        private void CalculateFileHash(FileListModel obj)
        {
            byte[] hash;
            using (var fst = OpenBlobStream(obj, blockSize, OpenFileMode.Read))
            {
                hash = FileHasher.ComputeHash(fst);
            }

            obj.Hash = hash;
            obj.ModifiedUtc = DateTime.UtcNow;
            obj.MarkDirty();
        }

        private Stream OpenBlobStream(long start, int blockSize, OpenFileMode mode)
        {
            var retVal = new BlobStream(start, broker, blockSize, ()=>{});
            return WrapStream(retVal, mode);
        }

        private Stream WrapStream(Stream baseStream, OpenFileMode mode)
        {
            if (mode == OpenFileMode.Read)
                return new DeflateStream(baseStream, CompressionMode.Decompress);

            baseStream.SetLength(0);
            return new DeflateStream(baseStream, CompressionLevel.SmallestSize);
        }

        public void Dispose()
        {
            WriteFileList(false);
            broker.Dispose();
            targetStream.Dispose();
        }
    }
}
