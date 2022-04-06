using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class MigrationBuilderExtensions
    {
        public static void IncludeToolkitPermissions(this MigrationBuilder migrationBuilder, string nameOfPermissionsTable = "Permissions", int minRevision = 0, int maxRevision = int.MaxValue)
        {
            if (minRevision <= 0 && maxRevision >= 0)
            {
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "ModuleHelp.View", "View the list of Module-Tutorials" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "ModuleHelp.Write", "Add and edit Tutorials" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "ModuleHelp.ViewTutorials", "View tutorials in the corresponding module." });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.View", "View the User-Table" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Write", "Edit the user-Table" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Properties.View", "Edit User-Properties" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Properties.Write", "Write User-Properties" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.AssignRole", "Assign user to a role in the Roles-View" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "AuthenticationTypes.View", "View the configured Authentication-Types" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "AuthenticationTypes.Write", "Edit the configured Authentication-Types" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Navigation.View", "View the Navigation-Configuration" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Navigation.Write", "Edit the Navigation" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Permissions.View", "View the available permissions." });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Permissions.Write", "Edit available permissions." });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Permissions.Assign", "Assign a Permission to a role in the roles-view" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Permissions.SelectFK", "Read Permissions-Table as Foreign-Key data" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Roles.View", "View available Roles" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Roles.Write", "Edit available Roles" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Roles.AssignPermission", "Assign a Role to a Permission in the permissions-view" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Roles.AssignUser", "Assign a Role to a user in the Users-View" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Tenants.View", "View the tenants-table" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Tenants.Write", "Edit the tenants-table" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Tenants.SelectFK", "Read Tenants-Table as Foreign-Key data" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Tenants.AssignUser", "Assign a User to a Tenant in the User-View" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Tenants.WriteSettings", "Edit Tenant-Settings" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Tenants.ViewSettings", "View Tenant-Settings" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "DashboardWidgets.View", "View configured Dashboard-Widgets" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "DashboardWidgets.Write", "Configure Dashboard-Widgets" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "DiagnosticsQueries.View", "View Diagnostics-Queries" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "DiagnosticsQueries.Write", "Define Diagnostics-Queries" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "GlobalSettings.View", "View Global settings" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "GlobalSettings.Write", "Edit Global settings" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "PlugInConstants.View", "View PlugIn-Constants" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "PlugInConstants.Write", "Edit PlugIn-Constants" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "PlugIns.View", "View PlugIns" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "PlugIns.Write", "Edit PlugIns" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "SystemLog.View", "View the System-Log" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "AssemblyDiagnostics.View", "View Assembly-Diagnostics view" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Sysadmin", "View Global items Views that normally use Query-Filters" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "BrowseFs", "Browse the Server-File-System" });
            }

            if (minRevision <= 1 && maxRevision >= 1)
            {
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Tenants.AssignNav", "Assign a Tenant to a navigation-Menu" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Logins.View", "View additional Logins of Users" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Logins.Write", "Manage additional Logins of Users" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Claims.View", "View additional User-claims" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Claims.Write", "Manage additional User-claims" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Tokens.View", "View issued User-Tokens" });
                migrationBuilder.InsertData(nameOfPermissionsTable, new string[] { "PermissionName", "Description" },
                    new object[] { "Users.Tokens.Write", "Manage issued User-Tokens" });
            }
        }
    }
}
