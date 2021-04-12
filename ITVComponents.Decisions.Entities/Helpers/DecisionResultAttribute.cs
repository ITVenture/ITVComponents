using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.DataAnnotations;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Decisions.Entities.Models;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Decisions.Entities.Helpers
{
    public class DecisionResultAttribute:CustomValueSourceAttribute
    {
        public DecisionResultAttribute()
        {
        }

        /// <summary>
        /// Gets or sets the Name of the Decider that is invoked to get the result. Leave this value null, if you want to use TableDeciders
        /// </summary>
        public string DeciderName { get; set; }

        /// <summary>
        /// Gets or sets the Decider-Mode for this decider. If you want to use the provided Entity as Base-Value set DeciderMode to This, otherwise to Property.
        /// </summary>
        public DeciderMode DeciderMode { get; set; } = DeciderMode.This;

        /// <summary>
        /// Gets or sets the PropertyName that is used as base-value for the decider. This value is only used in DeciderMode = Property.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Gets or sets a message that is returned when the object that is used as decider-base is null
        /// </summary>
        public string EntityNullMessage { get; set; }

        /// <summary>
        /// Gets or sets the result to apply to the result when the used object is a null-value
        /// </summary>
        public DecisionResult EntityNullResult { get; set; } = DecisionResult.None;

        /// <summary>
        /// Gets the custom value for a Property that is decorated with a derived attribute
        /// </summary>
        /// <param name="originalObject">the original object that is applied to a viewmodel</param>
        /// <param name="requestInstance"></param>
        /// <returns>the value to assign to the target-property on a viewmodel</returns>
        protected override object GetCustomValueFor(object originalObject, Func<Type, object> requestInstance)
        {
            var dbContext = ContextHelper.GetDbContextFromEntity(originalObject);
            if (dbContext == null)
            {
                dbContext = (DbContext) requestInstance(typeof(DbContext));
            }

            if (dbContext != null)
            {
                var src = originalObject;
                if (DeciderMode != DeciderMode.This)
                {
                    src = ExpressionParser.Parse(PropertyName, originalObject);
                }

                var deciderName = DeciderName;
                int deciderId = -1;
                Type entityType = typeof(object);
                string table=null, schema=null;
                if (string.IsNullOrEmpty(deciderName) && src != null)
                {
                    table = dbContext.GetTableName(src.GetType(), out entityType, out schema);
                    string fqName = $"[{schema}].[{table}]";
                    var td = dbContext.Set<TableDecider>().FirstOrDefault(n => n.TableName == fqName);
                    deciderId = td?.DeciderId??-1;
                }
                else
                {
                    deciderId = dbContext.Set<Decider>().FirstOrDefault(n => n.DisplayName == deciderName)?.DeciderId ?? -1;
                }

                if (deciderId != -1)
                {
                    IConstraintFactory factory = RepoConstraintsInitializer.GetFactory(dbContext);
                    var deciderRecord = dbContext.Set<Decider>().First(n => n.DeciderId == deciderId);
                    var decider = GetSimpleDecider(deciderRecord, entityType, factory, table, schema, (s, t) => { return null; });
                    DeciderResult retVal = new DeciderResult();
                    retVal.Result = decider.Decide(src, DecisionMethod.Full, out var msg);
                    retVal.Messages = msg;
                    return retVal;
                }

                if (src == null)
                {
                    return new DeciderResult {Messages = EntityNullMessage, Result = EntityNullResult};
                }

                LogEnvironment.LogDebugEvent(null, "No capable decider was found.", (int) LogSeverity.Warning, null);
                return null;
            }

            LogEnvironment.LogDebugEvent(null, "Unable to resolve the DbContext for provided model.", (int) LogSeverity.Warning, null);
            return null;
        }

        private IDecider GetSimpleDecider(Decider decider, Type entityType, IConstraintFactory factory, string tableName, string schema, Func<string,Type,object> unknownParameters)
        {
            IDecider deciderObject = factory.CreateDecider(entityType, decider.ContextDriven);
            decider.Constraints.ForEach(n => deciderObject.AddConstraint(factory.GetConstraint(entityType,n.Constraint.ConstraintIdentifier,
                (u, t) =>
                {
                    if (u.Equals("tablename", StringComparison.OrdinalIgnoreCase) && t == typeof(string))
                    {
                        return tableName;
                    }

                    if (u.Equals("schema", StringComparison.OrdinalIgnoreCase) && t == typeof(string))
                    {
                        return schema;
                    }

                    return unknownParameters(u, t);
                })));

            return deciderObject;
        }
    }

    public enum DeciderMode
    {
        This,
        Property
    }
}
