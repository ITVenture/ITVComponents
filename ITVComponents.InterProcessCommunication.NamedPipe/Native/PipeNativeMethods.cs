using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Native
{
    /// <summary>
    /// What the kernel tells about the other end of a named pipe.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class PipeNativeMethods
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint TokenQuery = 0x0008;
        private const uint TokenDuplicate = 0x0002;

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(SafeProcessHandle process, uint desiredAccess, out SafeAccessTokenHandle token);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe, out uint clientSessionId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder exeName, ref uint size);

        /// <summary>
        /// The id of the process at the client end of the pipe.
        /// </summary>
        public static uint ClientProcessId(SafePipeHandle pipe)
        {
            if (!GetNamedPipeClientProcessId(pipe, out var pid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetNamedPipeClientProcessId failed.");
            }

            return pid;
        }

        /// <summary>
        /// The Windows session of the process at the client end of the pipe.
        /// </summary>
        public static uint ClientSessionId(SafePipeHandle pipe)
        {
            if (!GetNamedPipeClientSessionId(pipe, out var sessionId))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetNamedPipeClientSessionId failed.");
            }

            return sessionId;
        }

        /// <summary>
        /// The account a process runs under, read from its primary token.
        /// </summary>
        public static System.Security.Principal.WindowsIdentity ProcessUser(uint processId)
        {
            using var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenProcess failed for process {processId}.");
            }

            if (!OpenProcessToken(process, TokenQuery | TokenDuplicate, out var token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenProcessToken failed for process {processId}.");
            }

            using (token)
            {
                return new System.Security.Principal.WindowsIdentity(token.DangerousGetHandle());
            }
        }

        /// <summary>
        /// The full path of a process image.
        /// </summary>
        public static string ProcessImagePath(uint processId)
        {
            using var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenProcess failed for process {processId}.");
            }

            var buffer = new StringBuilder(1024);
            uint size = (uint)buffer.Capacity;
            if (!QueryFullProcessImageName(process, 0, buffer, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"QueryFullProcessImageName failed for process {processId}.");
            }

            return buffer.ToString(0, (int)size);
        }
    }
}
