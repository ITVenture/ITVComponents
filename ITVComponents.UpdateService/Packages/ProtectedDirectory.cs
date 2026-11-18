using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ITVComponents.UpdateService.Packages
{
    /// <summary>
    /// A folder only SYSTEM and the administrators may write to. Packages are downloaded, checked and installed there,
    /// so that no user can swap a file between the check and the installation.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class ProtectedDirectory
    {
        /// <summary>
        /// Creates the folder if needed and sets its ACL: inheritance cut off, full control for SYSTEM and the
        /// administrators, read for authenticated users (the logs may be read).
        /// </summary>
        /// <param name="path">the folder</param>
        /// <returns>the folder</returns>
        public static DirectoryInfo Ensure(string path)
        {
            var directory = new DirectoryInfo(path);
            if (!directory.Exists)
            {
                directory.Create();
            }

            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
            directory.SetAccessControl(security);
            return directory;
        }
    }
}
