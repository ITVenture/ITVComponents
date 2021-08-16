using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Factory;
using ITVComponents.InterProcessCommunication.Grpc.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Server;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ITVComponents.InterProcessCommunication.Grpc.Server
{
    public class GrpcServer:MessageServer
    {
        public GrpcServer(string hubAddress, PluginFactory factory, IHubClientConfigurator configurator, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):
            base(new GrpcHubConnectionFactory(hubAddress, serviceName, configurator, security), factory, useExtendedProxying, useSecurity, security)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(serviceHub, factory, serviceName, useExtendedProxying, useSecurity, security)
        {
        }

        public GrpcServer(string hubAddress, PluginFactory factory, IHubClientConfigurator configurator, string serviceName):this(hubAddress, factory, configurator, serviceName, false,false,null)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName):this(serviceHub, factory, serviceName, false,false,null)
        {
        }

        public GrpcServer(string hubAddress, PluginFactory factory, IHubClientConfigurator configurator, string serviceName, bool useExtendedProxying):this(hubAddress, factory, configurator, serviceName, useExtendedProxying,false,null)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName, bool useExtendedProxying):this(serviceHub, factory, serviceName, useExtendedProxying,false,null)
        {
        }

        public GrpcServer(string hubAddress, IDictionary<string,object> exposedObjects, IHubClientConfigurator configurator, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(new GrpcHubConnectionFactory(hubAddress, serviceName, configurator, security), exposedObjects, useExtendedProxying, useSecurity, security)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, IDictionary<string,object> exposedObjects, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(serviceHub, exposedObjects, serviceName, useExtendedProxying, useSecurity, security)
        {
        }

        public GrpcServer(string hubAddress, IDictionary<string,object> exposedObjects, IHubClientConfigurator configurator, string serviceName):this(hubAddress, exposedObjects, configurator, serviceName, false,false,null)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, IDictionary<string,object> exposedObjects, string serviceName):this(serviceHub, exposedObjects, serviceName, false,false,null)
        {
        }

        public GrpcServer(string hubAddress, IDictionary<string,object> exposedObjects, IHubClientConfigurator configurator, string serviceName, bool useExtendedProxying):this(hubAddress, exposedObjects, configurator, serviceName, useExtendedProxying,false,null)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, IDictionary<string,object> exposedObjects, string serviceName, bool useExtendedProxying):this(serviceHub, exposedObjects, serviceName, useExtendedProxying,false,null)
        {
        }
    }
}
