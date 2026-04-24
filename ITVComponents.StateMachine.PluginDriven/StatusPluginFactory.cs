using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ITVComponents.StateMachine.PluginDriven
{
    public class StatusPluginFactory<TStatus,TStatusTarget>:IStatusFactory<TStatus,TStatusTarget> where TStatus : Status<TStatus, TStatusTarget>, IPlugin where TStatusTarget : class
    {
        private readonly IPluginFactory factory;
        private readonly IDynamicLoader knownPluginProvider;
        private readonly string statusNamePattern;
        private readonly IDictionary<string, string> statusMapping;
        private PluginConfigurationItem[] konwnStatusPlugins;
        private ConcurrentBag<TStatus> createdStatuses = new ConcurrentBag<TStatus>();
        public IEnumerable<string> KnownStates => konwnStatusPlugins.Select(p => p.Name);

        public StatusPluginFactory(IPluginFactory factory, IDynamicLoader knownPluginProvider, string statusNamePattern, IDictionary<string,string> statusMapping)
        {
            this.factory = factory;
            this.knownPluginProvider = knownPluginProvider;
            this.statusNamePattern = statusNamePattern;
            this.statusMapping = statusMapping;
            konwnStatusPlugins = knownPluginProvider.GetScopedPluginNames().Where(n =>
                Regex.IsMatch(n.Name, statusNamePattern,
                    RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
                    RegexOptions.IgnorePatternWhitespace)).ToArray();
        }

        public TStatus ConstructStatus(string statusType, TStatusTarget target, TransducerMachine<TStatus, TStatusTarget> machine, RunArguments arguments,
            TStatus fromStatus)
        {
            var statusPluginName = statusMapping.ContainsKey(statusType) ? statusMapping[statusType] : statusType;
            var plug = konwnStatusPlugins.First(n => n.Name == statusPluginName);
            var retVal = factory.LoadPlugin<TStatus>($"{statusType}_{plug.Name}", plug.ConstructionString,
                new Dictionary<string, object>
                {
                    { "statusName", statusType },
                    { "target", target },
                    { "machine", machine },
                    { "arguments", arguments },
                    { "fromStatus", fromStatus }
                }, false);
            createdStatuses.Add(retVal);
            return retVal;
        }

        public void Dispose()
        {
            var bufferedStatuses = createdStatuses.ToArray();
            createdStatuses.Clear();
            foreach (var status in bufferedStatuses)
            {
                try
                {
                    status.Dispose();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Failed to dispose status: {ex.Message}", LogSeverity.Error);
                }
            }

            factory.Dispose();
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {

        }
    }
}
