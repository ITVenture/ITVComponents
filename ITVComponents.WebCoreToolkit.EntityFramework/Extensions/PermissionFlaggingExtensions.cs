using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.PermissionFlagging;
using ITVComponents.WebCoreToolkit.Security.PermissionFlagging.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class PermissionFlaggingExtensions
    {
        public static PermissionFlag[] GetPermissionFlags(this PropertyInfo prop)
        {
            return prop.GetPermissionGroupName(typeof(DbSet<>));
        }

        public static string[] GetPermissionGroupNames(this PropertyInfo prop, string category = "General")
        {
            return prop.GetPermissionFlags().FirstOrDefault(n => n.Category == category)?.PermissionGroups ?? ["Default"];
        }
    }
}
