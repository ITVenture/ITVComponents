using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Communication.Native
{
#nullable enable
    internal static class NativeMethods
    {
        #region Structures
        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
        {
            internal uint nLength;
            internal IntPtr lpSecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)]
            internal bool bInheritHandle;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal static class SecHelper
        {
            public static SECURITY_ATTRIBUTES GetEmpty()
            {
                return new SECURITY_ATTRIBUTES
                {
                    nLength = (uint)Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)),
                    lpSecurityDescriptor = IntPtr.Zero,
                    bInheritHandle = false
                };
            }

            public static SECURITY_ATTRIBUTES CreateSecurity(RawSecurityDescriptor sec)
            {
                byte[] binDACL = new byte[sec.BinaryLength];
                sec.GetBinaryForm(binDACL, 0);

                var lpSecurityDescriptor = Marshal.AllocHGlobal(sec.BinaryLength);

                Marshal.Copy(binDACL, 0, lpSecurityDescriptor, binDACL.Length);
                return new SECURITY_ATTRIBUTES
                {
                    bInheritHandle = false,
                    lpSecurityDescriptor = lpSecurityDescriptor,
                    nLength = (uint)Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES))
                };
            }

            public static void Destroy(SECURITY_ATTRIBUTES attr)
            {
                if (attr.lpSecurityDescriptor != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(attr.lpSecurityDescriptor);
                }
            }
        }

        internal static class PageOptions
        {
            internal const int PAGE_READWRITE = 0x04;
            internal const int PAGE_READONLY = 0x02;
            internal const int PAGE_WRITECOPY = 0x08;
            internal const int PAGE_EXECUTE_READ = 0x20;
            internal const int PAGE_EXECUTE_READWRITE = 0x40;
        }

        internal static class FileMapOptions
        {
            internal const int FILE_MAP_COPY = 0x0001;
            internal const int FILE_MAP_WRITE = 0x0002;
            internal const int FILE_MAP_READ = 0x0004;
            internal const int FILE_MAP_EXECUTE = 0x0020;
            internal const int Full = FILE_MAP_EXECUTE | FILE_MAP_READ | FILE_MAP_WRITE;
        }

        [Flags]
        public enum ViewAccess : uint
        {
            STANDARD_RIGHTS_REQUIRED = 0x000F0000,
            SECTION_QUERY = 0x0001,
            SECTION_MAP_WRITE = 0x0002,
            SECTION_MAP_READ = 0x0004,
            SECTION_MAP_EXECUTE = 0x0008,
            SECTION_EXTEND_SIZE = 0x0010,
            SECTION_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | SECTION_QUERY |
            SECTION_MAP_WRITE |
            SECTION_MAP_READ |
            SECTION_MAP_EXECUTE |
            SECTION_EXTEND_SIZE),
            FILE_MAP_ALL_ACCESS = SECTION_ALL_ACCESS
        }

        #endregion

        #region General imports

        [DllImport("kernel32", EntryPoint = "CloseHandle", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int CloseHandle(IntPtr hHandle);

        #endregion

        #region Memory Mapped Files imports

        [DllImport("kernel32.dll", EntryPoint = "CreateFileMappingW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeMemoryMappedFileHandle CreateFileMapping(
            IntPtr hFile,
            ref SECURITY_ATTRIBUTES lpFileMappingAttributes,
            int flProtect,
            int dwMaximumSizeHigh,
            int dwMaximumSizeLow,
            string? lpName);

        [DllImport("kernel32.dll", EntryPoint = "OpenFileMappingW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeMemoryMappedFileHandle OpenFileMapping(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", EntryPoint = "MapViewOfFile", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeMemoryMappedViewHandle MapViewOfFile(SafeMemoryMappedFileHandle hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint /* UIntPtr */ dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", EntryPoint = "UnmapViewOfFile", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.VariantBool)]
        internal static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        #endregion
    }
}
