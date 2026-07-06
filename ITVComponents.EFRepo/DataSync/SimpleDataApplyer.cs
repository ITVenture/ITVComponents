using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace ITVComponents.EFRepo.DataSync
{
    public static class SimpleDataApplyer
    {
        public static void ApplyData(this DbContext db, Change[] changes, StringBuilder messages, Action<string, Dictionary<string,object>> extendQuery,
            Action<Dictionary<string, object>> extendQueryVariables)
        {
            Dictionary<string, int> inserts = new Dictionary<string, int>();
            Dictionary<string, int> deletes = new Dictionary<string, int>();
            Dictionary<string, int> updates = new Dictionary<string, int>();
            Dictionary<string, int> modifications = new Dictionary<string, int>();
            Dictionary<string, int> skipped = new Dictionary<string, int>();
            Dictionary<Type, IDbSet> typeSets = new Dictionary<Type, IDbSet>();
            try
            {
                foreach (var change in changes.Where(n => n.Apply))
                {
                    // Best-effort per change: a single failing change (e.g. an FK-resolution expression that
                    // references a record which was not migrated) is logged and skipped, its partially-tracked
                    // entity detached so it cannot corrupt the final SaveChanges, and the remaining changes are
                    // still applied — instead of aborting the whole data-apply.
                    object entity = null;
                    try
                    {
                        IDbSet targetSet;
                        var rawEntity = ExpressionParser.Parse(change.EntityName, db);
                        var rawType = rawEntity.GetType();
                        var dbsetType = typeof(DbSet<>);
                        var obj = typeof(object);
                        var success = false;
                        if (rawType.IsGenericType)
                        {
                            while (rawType != obj && rawType != null)
                            {
                                if (rawType.IsGenericType)
                                {
                                    var tmp = rawType.GetGenericTypeDefinition();
                                    if (tmp == dbsetType)
                                    {
                                        rawType = rawType.GetGenericArguments()[0];
                                        success = true;
                                        break;
                                    }
                                }

                                rawType = rawType.BaseType;
                            }
                        }

                        if (!success)
                        {
                            throw new InvalidOperationException($"Unable to extract entity-type of {change.EntityName}.");
                        }

                        targetSet = typeSets.GetOrInsert(rawType, db.Set);
                        switch (change.ChangeType)
                        {
                            case ChangeType.Insert:
                                entity = targetSet.New();
                                break;
                            case ChangeType.Update:
                            {
                                var rawQuery = BuildRawKey(db, change, extendQueryVariables);
                                extendQuery?.Invoke(change.EntityName, rawQuery);
                                entity = targetSet.FindWithQuery(rawQuery, false);
                                var id = targetSet.GetIndex(entity);
                                LogEnvironment.LogDebugEvent(null,
                                    $"Fetched record {id} of Type '{rawType.FullName}' for {change.ChangeType}.",
                                    (int)LogSeverity.Report, "EFRepo:SimpleDataApplyer");
                                break;
                            }
                            case ChangeType.Delete:
                            {
                                var rawQuery = BuildRawKey(db, change, extendQueryVariables);
                                extendQuery?.Invoke(change.EntityName, rawQuery);
                                entity = targetSet.FindWithQuery(rawQuery, true);
                                if (entity != null)
                                {
                                    var id = targetSet.GetIndex(entity);
                                    deletes.AddOrUpdate(change.EntityName, 1, n => n.Value + 1);
                                    LogEnvironment.LogDebugEvent(null,
                                        $"Fetched record {id} of Type '{rawType.FullName}' for {change.ChangeType}.",
                                        (int)LogSeverity.Report, "EFRepo:SimpleDataApplyer");
                                }
                                else
                                {
                                    messages.AppendLine(
                                        $"Entity of {change.EntityName} for {change.ChangeType} was not found.");
                                    LogEnvironment.LogDebugEvent(null,
                                        $"No Entity was found for ChangeType {change.ChangeType}.",
                                        (int)LogSeverity.Report, "EFRepo:SimpleDataApplyer");
                                }

                                break;
                            }
                            default:
                                entity = null;
                                messages.AppendLine($"Ignored Change of Type {change.ChangeType}");
                                break;
                        }

                        if (entity != null)
                        {
                            if (change.ChangeType != ChangeType.Delete)
                            {
                                LogEnvironment.LogDebugEvent(null,
                                    $"Updating the Entity of type '{rawType.FullName}' in {change.ChangeType}-Mode.\r\nProperties:\r\n{JsonHelper.ToJson(change.Details,SerializationTypingMode.StaticTyping, null)}",
                                    (int)LogSeverity.Report, "EFRepo:SimpleDataApplyer");
                                bool any = false;
                                foreach (var detail in change.Details.Where(n => n.Apply))
                                {
                                    var xp = string.IsNullOrEmpty(detail.ValueExpression)
                                        ? $"Entity.{detail.TargetProp}=ChangeType(NewValueRaw,Type)"
                                        : detail.ValueExpression;
                                    try
                                    {
                                        ExpressionParser.Parse(xp, BuildContext(
                                            entity: entity,
                                            db: db,
                                            change: change,
                                            newValue: detail.NewValue,
                                            propertyType: entity.GetValueType(detail.TargetProp),
                                            extendContext: extendQueryVariables));
                                        any = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        LogEnvironment.LogDebugEvent(null,
                                            $@"Assignment-Expression failed!. ({ex.OutlineException()})
Expression: {xp}",
                                            (int)LogSeverity.Error, "EFRepo:SimpleDataApplyer");
                                        throw;
                                    }
                                }

                                if (change.ChangeType == ChangeType.Insert && any)
                                {
                                    targetSet.Add(entity);
                                    inserts.AddOrUpdate(change.EntityName, 1, n => n.Value + 1);
                                }
                                else if (change.ChangeType == ChangeType.Insert)
                                {
                                    messages.AppendLine(
                                        $"An Insert of an entity with no properties was ignored ({change.EntityName}.");
                                }
                                else
                                {
                                    updates.AddOrUpdate(change.EntityName, 1, n => n.Value + 1);
                                }

                                modifications.AddOrUpdate(change.EntityName, change.Details.Count,
                                    n => n.Value + change.Details.Count);
                            }
                            else
                            {
                                targetSet.Remove(entity);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        skipped.AddOrUpdate(change.EntityName, 1, n => n.Value + 1);
                        messages.AppendLine(
                            $"Change on Entity {change.EntityName} ({change.ChangeType}) failed and was skipped: {ex.Message}");
                        LogEnvironment.LogDebugEvent(null,
                            $"Best-effort data-apply skipped a change on '{change.EntityName}' ({change.ChangeType}): {ex.OutlineException()}",
                            (int)LogSeverity.Warning, "EFRepo:SimpleDataApplyer");

                        // Drop the partially-applied entity from the change-tracker so it is not persisted by the
                        // final SaveChanges (and cannot make that whole save fail on its incomplete state).
                        if (entity != null)
                        {
                            try
                            {
                                db.Entry(entity).State = EntityState.Detached;
                            }
                            catch (Exception detachEx)
                            {
                                LogEnvironment.LogDebugEvent(null,
                                    $"Failed to detach a skipped entity of {change.EntityName}: {detachEx.OutlineException()}",
                                    (int)LogSeverity.Warning, "EFRepo:SimpleDataApplyer");
                            }
                        }
                    }
                }
            }
            finally
            {
                db.SaveChanges();
            }

            foreach (var insert in inserts)
            {
                messages.AppendLine($"Inserted {insert.Value} Rows on Entity {insert.Key}");
            }

            foreach (var update in updates)
            {
                messages.AppendLine($"Updated {update.Value} Rows on Entity {update.Key}");
            }

            foreach (var delete in deletes)
            {
                messages.AppendLine($"Deleted {delete.Value} Rows on Entity {delete.Key}");
            }

            foreach (var modification in modifications)
            {
                messages.AppendLine($"{modification.Value} values were updated on Entity {modification.Key} in total");
            }

            foreach (var skip in skipped)
            {
                messages.AppendLine($"Skipped {skip.Value} failing change(s) on Entity {skip.Key} (see log for details)");
            }
        }

        private static Dictionary<string, object> BuildRawKey(DbContext db, Change change, Action<Dictionary<string,object>> extendVariables)
        {
            var retVal = new Dictionary<string, object>();
            foreach (var k in change.Key)
            {
                string keyExpression = null;
                if (change.KeyExpression != null && change.KeyExpression.ContainsKey(k.Key))
                {
                    keyExpression = change.KeyExpression[k.Key];
                }

                var key = !string.IsNullOrEmpty(keyExpression)
                    ? ExpressionParser.Parse(keyExpression, BuildContext(db:db, value:k.Value, extendContext:extendVariables))
                    : k.Value;
                retVal.Add(k.Key, key);
            }

            return retVal;
        }

        public static Dictionary<string, object> BuildContext(DbContext db, Change change = null, object entity = null,
            string newValue = null, object propertyType = null, string value = null,
            Action<Dictionary<string, object>> extendContext = null)
        {
            var retVal = new Dictionary<string, object>
            {
                { "Db", db }
            };

            if (change != null)
            {
                retVal.Add("Change", change);
            }

            if (entity != null)
            {
                retVal.Add("Entity", entity);
            }

            if (newValue != null)
            {
                retVal.Add("NewValueRaw", newValue);
            }

            if (propertyType != null)
            {
                retVal.Add("Type", propertyType);
            }

            if (value != null)
            {
                retVal.Add("Value", value);
            }

            extendContext?.Invoke(retVal);
            return retVal;
        }
    }
}
