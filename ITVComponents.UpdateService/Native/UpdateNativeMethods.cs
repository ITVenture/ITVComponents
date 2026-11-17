using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ITVComponents.UpdateService.Native
{
    /// <summary>
    /// Authenticode, the MSI database and starting a process in a user's session.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class UpdateNativeMethods
    {
        // ----- Authenticode -----

        private static readonly Guid WintrustActionGenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdRevokeWholeChain = 1;
        private const uint WtdChoiceFile = 1;
        private const uint WtdRevocationCheckNone = 0x10;
        private const uint WtdRevocationCheckChain = 0x40;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WintrustFileInfo
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WintrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, ref WintrustData pWVTData);

        /// <summary>
        /// Checks the Authenticode signature of a file: present, intact, and its chain trusted by Windows.
        /// </summary>
        /// <returns>0 when trusted, else the HRESULT of WinVerifyTrust</returns>
        public static int VerifyAuthenticode(string path, bool checkRevocation)
        {
            var fileInfo = new WintrustFileInfo
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(),
                pcwszFilePath = path
            };
            var filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
            try
            {
                Marshal.StructureToPtr(fileInfo, filePointer, false);
                var data = new WintrustData
                {
                    cbStruct = (uint)Marshal.SizeOf<WintrustData>(),
                    dwUIChoice = WtdUiNone,
                    fdwRevocationChecks = checkRevocation ? WtdRevokeWholeChain : WtdRevokeNone,
                    dwUnionChoice = WtdChoiceFile,
                    pFile = filePointer,
                    dwProvFlags = checkRevocation ? WtdRevocationCheckChain : WtdRevocationCheckNone
                };
                return WinVerifyTrust(IntPtr.Zero, WintrustActionGenericVerifyV2, ref data);
            }
            finally
            {
                Marshal.DestroyStructure<WintrustFileInfo>(filePointer);
                Marshal.FreeHGlobal(filePointer);
            }
        }

        // ----- MSI database -----

        private const uint ErrorSuccess = 0;
        private const uint ErrorNoMoreItems = 259;
        private const uint ErrorMoreData = 234;

        [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiOpenDatabaseW")]
        private static extern uint MsiOpenDatabase(string szDatabasePath, IntPtr szPersist, out IntPtr phDatabase);

        [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiDatabaseOpenViewW")]
        private static extern uint MsiDatabaseOpenView(IntPtr hDatabase, string szQuery, out IntPtr phView);

        [DllImport("msi.dll")]
        private static extern uint MsiViewExecute(IntPtr hView, IntPtr hRecord);

        [DllImport("msi.dll")]
        private static extern uint MsiViewFetch(IntPtr hView, out IntPtr phRecord);

        [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiRecordGetStringW")]
        private static extern uint MsiRecordGetString(IntPtr hRecord, uint iField, StringBuilder szValueBuf, ref uint pcchValueBuf);

        [DllImport("msi.dll")]
        private static extern uint MsiCloseHandle(IntPtr hAny);

        /// <summary>
        /// Reads one value of the Property table of an MSI (read-only).
        /// </summary>
        /// <returns>the value, or null when the property is missing</returns>
        public static string? ReadMsiProperty(string msiPath, string property)
        {
            Check(MsiOpenDatabase(msiPath, IntPtr.Zero, out var database), $"MsiOpenDatabase({msiPath})");
            try
            {
                Check(MsiDatabaseOpenView(database, $"SELECT `Value` FROM `Property` WHERE `Property`='{property}'", out var view), "MsiDatabaseOpenView");
                try
                {
                    Check(MsiViewExecute(view, IntPtr.Zero), "MsiViewExecute");
                    var fetched = MsiViewFetch(view, out var record);
                    if (fetched == ErrorNoMoreItems)
                    {
                        return null;
                    }

                    Check(fetched, "MsiViewFetch");
                    try
                    {
                        uint length = 256;
                        var buffer = new StringBuilder((int)length);
                        var read = MsiRecordGetString(record, 1, buffer, ref length);
                        if (read == ErrorMoreData)
                        {
                            length++;
                            buffer = new StringBuilder((int)length);
                            read = MsiRecordGetString(record, 1, buffer, ref length);
                        }

                        Check(read, "MsiRecordGetString");
                        return buffer.ToString();
                    }
                    finally
                    {
                        MsiCloseHandle(record);
                    }
                }
                finally
                {
                    MsiCloseHandle(view);
                }
            }
            finally
            {
                MsiCloseHandle(database);
            }
        }

        private static void Check(uint result, string call)
        {
            if (result != ErrorSuccess)
            {
                throw new Win32Exception((int)result, $"{call} failed with {result}.");
            }
        }

        // ----- Start in a session -----

        private const uint TokenAllAccess = 0xF01FF;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const uint CreateUnicodeEnvironment = 0x00000400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, int impersonationLevel, int tokenType, out IntPtr phNewToken);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, [MarshalAs(UnmanagedType.Bool)] bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateProcessAsUserW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessAsUser(IntPtr hToken, string? lpApplicationName, StringBuilder lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref StartupInfo lpStartupInfo, out ProcessInformation lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Starts a program as the user logged on to a session, on that session's interactive desktop. Needs LocalSystem
        /// (<c>SeTcbPrivilege</c>).
        /// </summary>
        /// <returns>the process id</returns>
        public static int StartInSession(int sessionId, string applicationPath, string? arguments, string? workingDirectory)
        {
            if (!WTSQueryUserToken((uint)sessionId, out var userToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"No user token for session {sessionId} (nobody logged on, or the service is not LocalSystem).");
            }

            var primaryToken = IntPtr.Zero;
            var environment = IntPtr.Zero;
            try
            {
                if (!DuplicateTokenEx(userToken, TokenAllAccess, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out primaryToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx failed.");
                }

                if (!CreateEnvironmentBlock(out environment, primaryToken, false))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateEnvironmentBlock failed.");
                }

                var commandLine = new StringBuilder($"\"{applicationPath}\"" + (string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments));
                var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), lpDesktop = @"winsta0\default" };
                if (!CreateProcessAsUser(primaryToken, applicationPath, commandLine, IntPtr.Zero, IntPtr.Zero, false, CreateUnicodeEnvironment, environment, workingDirectory, ref startup, out var process))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateProcessAsUser failed for {applicationPath} in session {sessionId}.");
                }

                CloseHandle(process.hThread);
                CloseHandle(process.hProcess);
                return process.dwProcessId;
            }
            finally
            {
                if (environment != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(environment);
                }

                if (primaryToken != IntPtr.Zero)
                {
                    CloseHandle(primaryToken);
                }

                CloseHandle(userToken);
            }
        }
    }
}
