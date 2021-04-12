using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Decisions.Entities.Models;
using ITVComponents.Decisions.FactoryHelpers;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Decisions.Entities.Helpers
{
    public static class RepoConstraintsInitializer
    {
        private static ConcurrentDictionary<DbContext, DeciderContext> context2Decisions = new ConcurrentDictionary<DbContext, DeciderContext>();
        private static ConcurrentDictionary<IConstraintFactory, DeciderContext> factory2Decisions = new ConcurrentDictionary<IConstraintFactory, DeciderContext>();
        public static void InitializeConstraints(IConstraintFactory constraintFactory, PluginFactory pluginFactory, DbContext entityContext)
        {
            var constraintTypes = entityContext.Set<ConstraintDefinition>();
            Dictionary<string, object> emptyDict = new Dictionary<string, object>
                {
                    {"Ask",Ask.Value},
                    {"AskType", typeof(Ask) }
                };
            foreach (var constraintType in constraintTypes)
            {
                Type targetType = (Type)ExpressionParser.Parse(constraintType.ConstraintType, emptyDict, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); });
                ConstraintConstructor cst = new ConstraintConstructor(targetType,
                    constraintType.ConstraintIdentifier);
                constraintType.Parameters.OrderBy(n => n.ParameterOrder)
                    .ForEach(
                        n =>
                            cst.Parameters.Add(
                                new ConstructorParameter(ExpressionParser.Parse(n.ParameterValue, emptyDict, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); }),
                                    (Type)ExpressionParser.Parse(n.ParameterType, emptyDict, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); }))));
                constraintFactory.RegisterType(cst);
            }

            var dc = new DeciderContext {PluginFactory = pluginFactory, ConstraintFactory = constraintFactory, DbContext = entityContext};
            factory2Decisions.TryAdd(constraintFactory, dc);
            context2Decisions.TryAdd(entityContext, dc);
            constraintFactory.Disposed += UnRegisterFactory;
        }

        private static void UnRegisterFactory(object sender, EventArgs e)
        {
            IConstraintFactory factory = sender as IConstraintFactory;
            if (factory2Decisions.TryRemove(factory, out var context))
            {
                context2Decisions.TryRemove(context.DbContext, out _);
            }
        }

        internal static IConstraintFactory GetFactory(DbContext context)
        {
            if (context2Decisions.TryGetValue(context, out var pack))
            {
                return pack.ConstraintFactory;
            }

            return null;
        }

        private class DeciderContext
        {
            public PluginFactory PluginFactory { get; set; }
            public DbContext DbContext { get; set; }
            public IConstraintFactory ConstraintFactory { get; set; }
        }
    }
}
