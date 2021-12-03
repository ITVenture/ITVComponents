using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.DataAccess.DataAnnotations;
 
namespace ITVComponents.DataAccess.Extensions
{
    /// <summary>
    /// Extension methods used to cast a list of DynamicResult records to a datatable
    /// </summary>
    public static class TableExtensions
    {
        /// <summary>
        /// Converts a list of DynamicResult records into a DataTable
        /// </summary>
        /// <param name="data">the list of dynamicresult that is being converted into a datatable</param>
        /// <param name="extensionForRowInResult">when provided adds a new field in the DynamicResult and maps back the generated data-row to each Record</param>
        /// <returns>the datatable containing the items</returns>
        public static DataTable ToDataTable(this IEnumerable<DynamicResult> data, string extensionForRowInResult = null)
        {
            DataTable retVal = new DataTable();
            DynamicResult first = data.FirstOrDefault();
            if (first != null)
            {
                string[] names = first.GetDynamicMemberNames().ToArray();
                foreach (string s in names)
                {
                    retVal.Columns.Add(s, GetTableType(first.GetType(s)));
                }
 
                foreach (DynamicResult res in data)
                {
                    DataRow row = retVal.NewRow();
                    foreach (string s in names)
                    {
                        row[s] = res[s]??DBNull.Value;
                    }

                    if (extensionForRowInResult != null)
                    {
                        res.Extendable = true;
                        res[extensionForRowInResult] = row;
                    }

                    retVal.Rows.Add(row);
                }
            }
 
            return retVal;
        }
 
        /// <summary>
        /// Converts any data into a DataTable as long as its marked with the TableTypeAttribute attribute
        /// </summary>
        /// <typeparam name="T">the source type to convert into a DataTable object</typeparam>
        /// <param name="data">the structured data to convert into a DataTable</param>
        /// <param name="tableTypeName">the tableType Name that is declared in the database</param>
        /// <returns>a DataTable containing the provided data</returns>
       public static DataTable ToDataTable(this IEnumerable data, out string tableTypeName)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
 
            object[] arrayObject = data as object[] ?? Enumerable.Cast<object>(data).ToArray();
            Type t = arrayObject.First().GetType();
            if (t == typeof (DynamicResult))
            {
                throw new InvalidOperationException("Wrong usage of ToDataTable. Please use the overload ToDataTable(IEnumerable<DynamicResult>)");
            }
 
            DataTable retVal = new DataTable();
            TableTypeAttribute attr = (TableTypeAttribute)Attribute.GetCustomAttribute(t, typeof (TableTypeAttribute));
            if (attr == null)
            {
                throw new ArgumentException(
                    "the passed array does not contain data that is marked with a  TableTypeAttribute", "data");
            }
 
            tableTypeName = attr.TableTypeName ?? t.Name;
            var properties =
                (from n in
                     t.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance |
                                     BindingFlags.GetProperty)
                 select
                     new
                         {
                             Property = n,
                             Attribute =
                     (TypeColumnAttribute) Attribute.GetCustomAttribute(n, typeof (TypeColumnAttribute), true)
                         });
            if (attr.Layout == TableTypeLayout.Explicit)
            {
                properties = (from n in properties orderby n.Attribute.OrdinalPosition select n);
            }
 
            var propArr = properties.ToArray();
            propArr.ForEach(
                n =>
                retVal.Columns.Add(n.Attribute != null ? (n.Attribute.ColumnName ?? n.Property.Name) : n.Property.Name,
                    GetTableType(n.Attribute != null
                                       ? (n.Attribute.ColumnType ?? n.Property.PropertyType)
                                       : n.Property.PropertyType)));
            (from n in arrayObject
             select (from u in propArr
                     select
                         new
                             {
                                 Value = u.Property.GetValue(n, null),
                                 Name = u.Attribute != null ? (u.Attribute.ColumnName ?? u.Property.Name) : u.Property.Name,
                             }
                    )).ForEach(n =>
                                   {
                                       DataRow row = retVal.NewRow();
                                       n.ForEach(d => row[d.Name] = d.Value ?? DBNull.Value);
                                       retVal.Rows.Add(row);
                                   });
            return retVal;
        }

        private static Type GetTableType(Type rawType)
        {
            return Nullable.GetUnderlyingType(rawType) ?? rawType;
        }
    }
}