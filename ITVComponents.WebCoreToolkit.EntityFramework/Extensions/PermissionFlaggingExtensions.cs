using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.PermissionFlagging;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class PermissionFlaggingExtensions
    {
        public static string[] GetPermissionGroupName(this PropertyInfo prop)
        {
            return prop.GetPermissionGroupName(typeof(DbSet<>));
        }
    }
}
