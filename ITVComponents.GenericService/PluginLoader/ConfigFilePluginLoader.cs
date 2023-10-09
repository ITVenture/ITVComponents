using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.Model;
using ITVComponents.Scripting.CScript.Core;

namespace ITVComponents.GenericService.PluginLoader
{
    internal class ConfigFilePluginLoader:IDynamicLoader
    {
        public IEnumerable<PluginInfoModel> GetStartupPlugins(PluginInitializationPhase startupPhase)
        {
            return from t in ServiceConfigHelper.PlugIns where !t.Disabled && (t.InitializationPhase & startupPhase) != 0 select new PluginInfoModel
            {
                Buffer = (t.InitializationPhase & PluginInitializationPhase.NoTracking) == 0, 
                ConstructorString = t.ConstructionString,
                UniqueName = t.Name
            };
        }

        public IEnumerable<PluginInfoModel> GetPreInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase)
        {
            return from t in ServiceConfigHelper.PlugIns.FirstOrDefault(n => !n.Disabled && n.Name == uniqueName
                        && (n.InitializationPhase & eligibleInitializationPhase) != 0)
                    ?.InitializeBefore ?? Array.Empty<string>()
                let tp = new { Ok = GetPluginInfo(eligibleInitializationPhase, t, out var df), Def = df }
                where tp.Ok
                select tp.Def;

        }

        public IEnumerable<PluginInfoModel> GetPostInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase)
        {
            return from t in ServiceConfigHelper.PlugIns.FirstOrDefault(n => !n.Disabled && n.Name == uniqueName
                    && (n.InitializationPhase & eligibleInitializationPhase) != 0)
                    ?.InitializeAfter ?? Array.Empty<string>()
                let tp = new { Ok = GetPluginInfo(eligibleInitializationPhase, t, out var df), Def = df }
                where tp.Ok
                select tp.Def;
        }

        public bool HasParamsFor(string uniqueName)
        {
            return ServiceConfigHelper.GenericTypeInformation.TryGetValue(uniqueName, out var t) && t.Any();
        }

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables,
            IStringFormatProvider formatter, out bool knownTypeUsed)
        {
            knownTypeUsed = false;
            var plug = ServiceConfigHelper.PlugIns.FirstOrDefault(n => !n.Disabled && n.Name == uniqueName);
            if (plug != null && ServiceConfigHelper.GenericTypeInformation.TryGetValue(uniqueName, out var l) && l.Length != 0)
            {
                var data = l;
                var joined = from t in genericTypeArguments
                    join d in data on t.GenericTypeName equals d.TypeParameterName
                    select new { Target = t, Type = d.TypeExpression };
                Dictionary<string, object> dic = new Dictionary<string, object>();
                customVariables ??= new Dictionary<string, object>();
                bool kt = knownTypeUsed;
                customVariables.ForEach(n => dic.Add(n.Key, new SmartProperty
                {
                    GetterMethod = t =>
                    {
                        kt = true;
                        return n.Value;
                    }
                }));
                knownTypeUsed = kt;
                foreach (var j in joined)
                {
                    j.Target.TypeResult = (Type)ExpressionParser.Parse(j.Type.ApplyFormat(formatter), dic);
                }
            }
        }

        public bool GetPluginInfo(PluginInitializationPhase currentPhase, string uniqueName, out PluginInfoModel definition)
        {
            var tmp = ServiceConfigHelper.PlugIns.FirstOrDefault(n => !n.Disabled &&
                                                                      (currentPhase & n.InitializationPhase) != 0 &&
                                                                      n.Name == uniqueName);
            bool retVal = tmp != null;
            definition = retVal?new PluginInfoModel
            {
                Buffer = (tmp.InitializationPhase & PluginInitializationPhase.NoTracking) == 0,
                ConstructorString = tmp.ConstructionString,
                UniqueName = tmp.Name
            }:null;
            return retVal;
        }
    }
}
