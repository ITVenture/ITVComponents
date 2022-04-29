using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DataSync
{
    public static class SimpleDataApplyer
    {
        public static void ApplyData(this DbContext db, Change[] changes, StringBuilder messages, Action<string, Dictionary<string,object>> extendQuery)
        {
            foreach (var change in changes.Where(n => n.Apply))
            {
                object entity;
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

                targetSet = db.Set(rawType);
                switch (change.ChangeType)
                {
                    case ChangeType.Insert:
                        entity = targetSet.New();
                        break;
                    case ChangeType.Update:
                    {
                        var rawQuery = BuildRawKey(db, change);
                        extendQuery?.Invoke(change.EntityName, rawQuery);
                        entity = targetSet.FindWithQuery(rawQuery, false);
                        var id = targetSet.GetIndex(entity);
                        messages.AppendLine($"Fetched record {id} of {change.EntityName} for {change.ChangeType}.");
                        break;
                    }
                    case ChangeType.Delete:
                    {
                        var rawQuery = BuildRawKey(db, change);
                        extendQuery?.Invoke(change.EntityName, rawQuery);
                        entity = targetSet.FindWithQuery(rawQuery, true);
                        if (entity != null)
                        {
                            var id = targetSet.GetIndex(entity);
                            messages.AppendLine($"Fetched record {id} of {change.EntityName} for {change.ChangeType}.");
                        }
                        else
                        {
                            messages.AppendLine($"Entity of {change.EntityName} for {change.ChangeType} was not found.");
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
                        foreach (var detail in change.Details)
                        {
                            var xp = string.IsNullOrEmpty(detail.ValueExpression)?
                                $"Entity.{detail.TargetProp}=ChangeType(NewValueRaw,Type)"
                                :detail.ValueExpression;
                            ExpressionParser.Parse(xp, new
                            {
                                Entity = entity,
                                Change = change,
                                NewValueRaw = detail.NewValue,
                                Db = db,
                                Type = entity.GetValueType(detail.TargetProp)
                            });
                        }

                        if (change.ChangeType == ChangeType.Insert)
                        {
                            targetSet.Add(entity);
                        }

                        messages.AppendLine(
                            $"Performed {change.ChangeType} on {change.EntityName} with {change.Details.Count} values");
                    }
                    else
                    {
                        targetSet.Remove(entity);
                        messages.AppendLine(
                            $"Performed {change.ChangeType} on {change.EntityName}");
                    }
                }
            }

            db.SaveChanges();
        }

        private static Dictionary<string, object> BuildRawKey(DbContext db, Change change)
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
                    ? ExpressionParser.Parse(keyExpression, new {Value = k.Value, Db = db})
                    : k.Value;
                retVal.Add(k.Key, key);
            }

            return retVal;
        }
    }
}
