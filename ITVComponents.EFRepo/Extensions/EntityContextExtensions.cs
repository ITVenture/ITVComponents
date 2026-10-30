using System;
using System.Linq;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.Extensions
{
    public static class EntityContextExtensions
    {
        /// <summary>
        /// Gets the TableName in the context-database for a specific type
        /// </summary>
        /// <typeparam name="T">the type for which to get the tablename</typeparam>
        /// <param name="context">the context in which to lookup the tablename</param>
        /// <param name="schema">the schema in which the table is defined</param>
        /// <returns>the name of the requested table</returns>
        public static string GetTableName<T>(this DbContext context, out string schema) where T : class
        {
            return GetTableName(context, typeof(T), out schema);
        }

        public static Type GetEntityType(this DbContext context, string schema, string tableName)
        {
            var effectiveSchema = string.IsNullOrEmpty(schema) ? MigrationSchema.DefaultSchema : schema;
            var t = context.Model.GetEntityTypes().FirstOrDefault(n =>
                (string.Equals(n.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase) && string.Equals(n.GetSchema() ?? MigrationSchema.DefaultSchema, effectiveSchema, StringComparison.OrdinalIgnoreCase))
                || (n.GetTableName() == null && string.Equals(n.GetViewName(), tableName, StringComparison.OrdinalIgnoreCase) && string.Equals(n.GetViewSchema() ?? MigrationSchema.DefaultSchema, effectiveSchema, StringComparison.OrdinalIgnoreCase)));
            return t?.ClrType;
        }

        /// <summary>
        /// Gets the effective database-schema of the given context (the default schema of its model, or dbo when the model has none)
        /// </summary>
        /// <param name="context">the context for which to get the schema</param>
        /// <returns>the schema that is used for unqualified objects of this context</returns>
        public static string GetDbSchema(this DbContext context)
        {
            return context.Model.GetDefaultSchema() ?? MigrationSchema.DefaultSchema;
        }

        /// <summary>
        /// Qualifies a database-object (table, view, function, procedure) with the effective schema of the given context in SqlServer-notation.
        /// Identifiers can not be passed as sql-parameters, so the result is meant to be put into the statement as literal.
        /// </summary>
        /// <param name="context">the context that provides the schema</param>
        /// <param name="objectName">the unqualified name of the object</param>
        /// <returns>the qualified object-name, e.g. [dbo].[Tenants]</returns>
        public static string QualifiedObjectName(this DbContext context, string objectName)
        {
            return $"[{context.GetDbSchema().Replace("]", "]]")}].[{objectName.Replace("]", "]]")}]";
        }

        /// <summary>
        /// Gets the TableName in the context-database for a specific type
        /// </summary>
        /// <param name="context">the context in which to lookup the tablename</param>
        /// <param name="type">the type for which to get the tablename</param>
        /// <param name="schema">the schema in which the table is defined</param>
        /// <returns>the name of the requested table</returns>
        public static string GetTableName(this DbContext context, Type type, out string schema)
        {
            return GetTableName(context, type, out _, out schema);
        }

        /// <summary>
        /// Gets the TableName in the context-database for a specific type
        /// </summary>
        /// <param name="context">the context in which to lookup the tablename</param>
        /// <param name="type">the type for which to get the tablename</param>
        /// <param name="entityBaseType">the raw-poco type of type</param>
        /// <param name="schema">the schema in which the table is defined</param>
        /// <returns>the name of the requested table</returns>
        public static string GetTableName(this DbContext context, Type type, out Type entityBaseType, out string schema)
        {
            var et = context.Model.FindRuntimeEntityType(type);
            schema = et.GetSchema();
            entityBaseType = et.ClrType; //et.BaseType?.ClrType??et.ClrType;
            return et.GetTableName();
        }

        /// <summary>
        /// Gets the Key-Property values for the given
        /// </summary>
        /// <param name="context">the dbcontext on which to check the key-properties of the given type</param>
        /// <param name="type">the type for which to get the key-columns</param>
        /// <returns>a string array containing the key-values</returns>
        public static string[] GetKeyProperties(this DbContext context, Type type)
        {
            var entitySet = context.Model.FindEntityType(type);
            return entitySet.GetKeys().SelectMany(n => n.Properties.Select(p => p.PropertyInfo.Name)).Distinct().ToArray();
            //return entitySet?.ElementType.KeyMembers.Select(n => n.Name).ToArray();
        }
    }
}
