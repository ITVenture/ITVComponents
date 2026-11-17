using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.UpdateService.Sessions
{
    /// <summary>
    /// What the service needs to know about the Windows sessions on the machine.
    /// </summary>
    public interface ISessionInfo
    {
        /// <summary>
        /// Gets the session attached to the physical console, or -1 if none.
        /// </summary>
        int ConsoleSessionId();

        /// <summary>
        /// Indicates whether a session is connected right now (console or remote desktop) - as opposed to disconnected.
        /// </summary>
        bool IsConnected(int sessionId);
    }

    /// <summary>
    /// The sessions as the terminal services report them.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsSessions : ISessionInfo
    {
        private const int WtsConnectState = 8;
        private const int WtsActive = 0;
        private const uint NoConsoleSession = 0xFFFFFFFF;

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WTSQuerySessionInformation(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytesReturned);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr memory);

        /// <inheritdoc />
        public int ConsoleSessionId()
        {
            var id = WTSGetActiveConsoleSessionId();
            return id == NoConsoleSession ? -1 : (int)id;
        }

        /// <inheritdoc />
        public bool IsConnected(int sessionId)
        {
            if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, WtsConnectState, out var buffer, out _))
            {
                LogEnvironment.LogEvent($"The state of session {sessionId} could not be read (error {Marshal.GetLastWin32Error()}); it counts as connected.", LogSeverity.Warning);
                return true;
            }

            try
            {
                return Marshal.ReadInt32(buffer) == WtsActive;
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }
    }
}
