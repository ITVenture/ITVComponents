using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication.Native;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32.SafeHandles;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Communication
{
    internal class GlobalMemoryMappedFile:IDisposable
    {
        private readonly int size;
        private SafeMemoryMappedFileHandle memBuffer;
        private SafeMemoryMappedViewHandle accessor;
        private UnmanagedMemoryStream ums;
        public GlobalMemoryMappedFile(string name, int size)
        {
            this.size = size;
            var secAttribs = CreateSecAttribs();
            try
            {
                memBuffer = NativeMethods.CreateFileMapping(
                    new IntPtr(-1),
                    ref secAttribs,
                    (int)NativeMethods.PageOptions.PAGE_READWRITE,
                    0,
                    size,
                    name);

                if (memBuffer.IsInvalid)
                {
                    //Console.WriteLine(memBuffer.DangerousGetHandle());
                    int lasterror = Marshal.GetLastWin32Error();
                    memBuffer.Dispose();
                    if (lasterror == 5)
                    {
                        throw new IOException("Access denied!");
                    }

                    memBuffer = NativeMethods.OpenFileMapping((int)NativeMethods.FileMapOptions.Full, false, name);

                }
                if (memBuffer.IsInvalid)
                {
                    int lasterror = Marshal.GetLastWin32Error();
                    memBuffer.Dispose();
                    throw new Win32Exception(lasterror,
                        string.Format(CultureInfo.InvariantCulture, "Error creating shared memory. Errorcode is {0}",
                            lasterror));
                }

                accessor = NativeMethods.MapViewOfFile(memBuffer, (uint)NativeMethods.ViewAccess.FILE_MAP_ALL_ACCESS, 0,
                    0, (uint)size);
                accessor.Initialize((uint)size);
                if (accessor.IsInvalid)
                {
                    accessor.Dispose();
                    memBuffer.Dispose();
                    int lasterror = Marshal.GetLastWin32Error();
                    throw new Win32Exception((int)lasterror,
                        string.Format(CultureInfo.InvariantCulture,
                            "Error creating shared memory view. Errorcode is {0}", lasterror));
                }
            }
            finally
            {
                NativeMethods.SecHelper.Destroy(secAttribs);
            }
        }

        public UnmanagedMemoryStream ViewStream =>
            ums ??= new UnmanagedMemoryStream(accessor, 0, size, FileAccess.ReadWrite);

        private static NativeMethods.SECURITY_ATTRIBUTES CreateSecAttribs()
        {
            //Create the descriptor with a null DACL --> Everything is granted.
            RawSecurityDescriptor sec = new RawSecurityDescriptor(ControlFlags.DiscretionaryAclPresent, null, null, null, null);

            //return NativeMethods.SecHelper.GetEmpty();
            return NativeMethods.SecHelper.CreateSecurity(sec);
        }

        public void Dispose()
        {
            ums?.Dispose();
            memBuffer?.Dispose();
            accessor?.Dispose();
        }
    }
}
