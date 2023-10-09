using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client
{
    /// <summary>
    /// Enables the usage of multiple configurators
    /// </summary>
    public class CollectedClientInit:IHubClientConfigurator
    {
        /// <summary>
        /// Holds a list of inner-configurators that will be invoked when a new channel was created
        /// </summary>
        private List<IHubClientConfigurator> innerConfigurators;

        /// <summary>
        /// Initializes a new instance of the CollectedClientInit class
        /// </summary>
        public CollectedClientInit()
        {
            innerConfigurators = new List<IHubClientConfigurator>();
        }

        /// <summary>
        /// Registers a configurator that will be invoied when a configuration-method is called
        /// </summary>
        /// <param name="configurator"></param>
        public void RegisterConfigurator(IHubClientConfigurator configurator)
        {
            innerConfigurators.Add(configurator);
        }

        /// <summary>
        /// Configures a channel before a grpc-client is created
        /// </summary>
        /// <param name="options">the channel-options to configure</param>
        public void ConfigureChannel(GrpcChannelOptions options)
        {
            innerConfigurators.ForEach(n => n.ConfigureChannel(options));
        }

        /// <summary>
        /// Configures the options used for the next call
        /// </summary>
        /// <param name="optionsRaw">the current state of call-options value</param>
        /// <returns>the modified call-options for the next call</returns>
        public CallOptions ConfigureCallOptions(CallOptions optionsRaw)
        {
            var retVal = optionsRaw;
            foreach (var t in innerConfigurators)
            {
                retVal = t.ConfigureCallOptions(retVal);
            }

            return retVal;
        }

    }
}
