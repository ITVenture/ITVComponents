using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ITVComponents.EFRepo.AutoValues
{
    public static class AutoValueHelper
    {
        /// <summary>
        /// Applies Auto-Values on a DBEntity-Entry before it is safed
        /// </summary>
        /// <param name="entry">the entry for which to get auto-values</param>
        /// <param name="context">the db-context that owns the entry</param>
        /// <param name="getSequenceValue">a callback that provides generating auto-values.</param>
        public static void ApplyAutoValues(this EntityEntry entry, DbContext context, Func<string, string> getSequenceValue)
        {
            if (entry.Entity == null)
            {
                throw new ArgumentNullException(nameof(entry.Entity));
            }

            Type t = entry.Entity.GetType();
            var props =
                (from p in
                    t.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.GetProperty |
                                    BindingFlags.SetProperty | BindingFlags.Public)
                    where
                        Attribute.IsDefined(p, typeof(AutoValueAttribute)) && p.GetIndexParameters().Length == 0 &&
                        p.PropertyType == typeof(string)
                    select new {Proprety= p, Attribute = (AutoValueAttribute)Attribute.GetCustomAttribute(p,typeof(AutoValueAttribute))}).ToArray();
            foreach (var prop in props)
            {
                string schema;
                string tableName = context.GetTableName(t, out schema);
                string existingValue = (string) prop.Proprety.GetValue(entry.Entity);
                if (string.IsNullOrEmpty(prop.Attribute.TriggerValue) || prop.Attribute.TriggerValue == existingValue)
                {
                    string propertyName = $"[{schema}].[{tableName}].[{prop.Proprety.Name}]";
                    string value = getSequenceValue(propertyName);
                    if (!string.IsNullOrEmpty(value))
                    {
                        prop.Proprety.SetValue(entry.Entity, value);
                    }
                }
            }
        }
    }
}
