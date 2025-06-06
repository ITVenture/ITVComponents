using System;
using System.Security.Principal;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    /// <summary>
    /// Contains all relevant information about a specific Server call
    /// </summary>
    public class RequestContext
    {
        public RequestContext(object requestMessage, IIdentity user, IServiceProvider services)
        {
            RequestMessage = requestMessage;
            User = user;
            Services = services;
        }

        public IPluginFactory ScopeFactory { get; set; }
        public object RequestMessage { get; }
        public IIdentity User { get; }
        public IServiceProvider Services { get;}
    }
}
