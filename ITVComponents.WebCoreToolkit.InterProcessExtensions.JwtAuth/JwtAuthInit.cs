using ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth.Config;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth
{
    public class JwtAuthInit: CollectableClientInit, IDeferredInit
    {
        private JwtAuthConfig configuration;
        public JwtAuthInit(string configName)
        {
            configuration = JwtAuthenticationSection.Helper.JwtAuthSchemes[configName];
        }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = true;


        /// <summary>
        /// Configures the options used for the next call
        /// </summary>
        /// <param name="optionsRaw">the current state of call-options value</param>
        /// <returns>the modified call-options for the next call</returns>
        public override CallOptions ConfigureCallOptions(CallOptions optionsRaw)
        {
            var retVal = base.ConfigureCallOptions(optionsRaw);
            var ent = new Metadata.Entry("Authorization", $"Bearer {GetCurrentBearer()}");
            if (retVal.Headers == null)
            {
                retVal = retVal.WithHeaders(new Metadata { ent });
            }
            else
            {
                retVal.Headers.Add(ent);
            }

            return retVal;
        }

        /// <summary>
        /// Returns the current bearer-token for this connection
        /// </summary>
        /// <returns>the current bearer-token that enables this instance to access the remote hub service</returns>
        private string GetCurrentBearer()
        {
            return "";
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            Initialized = true;
        }
    }
}
