using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Factory;
using ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.MessagingShared.Security.PrincipalProviders;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub
{
    public class ServiceHubProvider:IPlugin, IDeferredInit, IServiceHubProvider
    {
        private readonly string hubAddresses;
        private readonly IHubFactory hubFactory;
        private IServiceHub hub;
        private bool ownsBroker = true;

        private ConcurrentDictionary<string, IMemoryChannel> openChannels = new ConcurrentDictionary<string, IMemoryChannel>();

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = false;

        private IMemoryChannel baseChannel;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        public IEndPointBroker Broker { get; }

        private IServiceHub Hub => hub ??= hubFactory.CreateHub(this);

        /// <summary>
        /// Initializes a new instance of the ServiceHubProvider class
        /// </summary>
        /// <param name="hubAddresses">the hub-addresses for this serviceHub</param>
        /// <param name="factory">a factory that provides access to other plugins</param>
        public ServiceHubProvider(string hubAddresses, IHubFactory hubFactory)
        {
            this.hubAddresses = hubAddresses;
            this.hubFactory = hubFactory;
            Broker = new EndPointBroker();
        }

        /// <summary>
        /// Initializes a new instance of the ServiceHubProvider class
        /// </summary>
        /// <param name="parent">the parent hub-provider</param>
        /// <param name="hubAddresses">the hub-addresses for this serviceHub</param>
        /// <param name="hubFactory">the factory object that is used to create a service hub that handles incoming messages</param>
        /// <param name="factory">a factory that provides access to other plugins</param>
        public ServiceHubProvider(ITVComponents.InterProcessCommunication.MessagingShared.Hub.IServiceHubProvider parent, string hubAddresses, IHubFactory hubFactory)
        {
            this.hubAddresses = hubAddresses;
            this.hubFactory = hubFactory;
            Broker = parent.Broker;
            ownsBroker = false;
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                baseChannel = new MemoryServiceChannel(hubAddresses, false, MscMode.Server, 0, new IdentityFromWindowsProvider());
                baseChannel.ObjectReceived += ConnectionRequest;
                Initialized = true;
            }
        }

        private void ConnectionRequest(object sender, ObjectReceivedEventArgs e)
        {
            if (e.Value is ConnectionRequest crv)
            {
                var chan = openChannels.GetOrAdd(crv.ProposedGuid, s =>
                {
                    var ret = new MemoryServiceChannel(crv.ProposedGuid, true, MscMode.Server, crv.Ttl, new IdentityFromWindowsProvider());
                    ret.ObjectReceived += ClientComm;
                    ret.ConnectionStatusChanged += ClientConnectionChanged;
                    return ret;
                });
            }
        }

        private void ClientConnectionChanged(object? sender, EventArgs e)
        {
            var conn = (IMemoryChannel) sender;
            if (!conn.Connected)
            {
                TryRemoveChannel(conn);
            }
        }

        private void TryRemoveChannel(IMemoryChannel channel)
        {
            var ok = openChannels.TryRemove(channel.Name, out var mrConn);
            if (ok)
            {
                TryClose(mrConn);
            }

            var name = Broker.GetServiceByTag("ChannelName", channel.Name);
            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    Broker.UnsafeServerDrop(name);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Service drop failed:{ex.Message}", LogSeverity.Error);
                }
            }
        }

        private void ClientComm(object? sender, ObjectReceivedEventArgs e)
        {
            var conn = (IMemoryChannel) sender;
            if (conn.Connected)
            {
                if (e.Value is ConnectionDispose cdo)
                {
                    if (cdo.ConnectionClosing)
                    {
                        var ok = openChannels.TryRemove(conn.Name, out var mrConn);
                        if (ok)
                        {
                            TryClose(mrConn);
                        }
                    }
                }
                else if (e.Value is ServerOperationMessage som)
                {
                    try
                    {
                        e.Result = Hub.ConsumeService(som, e.Context).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        e.Result = new ServiceOperationResponseMessage
                        {
                            Ok = false,
                            OperationId = som.OperationId,
                            ResponsePayload = JsonHelper.ToJsonStrongTyped((SerializedException)ex),
                            TargetService = som.TargetService
                        };
                    }
                }
                else if (e.Value is ServiceOperationResponseMessage sorm)
                {
                    Hub.CommitServiceOperation(sorm, e.Context);
                }
                else if (e.Value is ServiceDiscoverMessage sdm)
                {
                    var dsres = Hub.DiscoverService(sdm, e.Context).ConfigureAwait(false).GetAwaiter().GetResult();
                    e.Result = dsres;
                }
                else if (e.Value is RegisterServiceMessage rsm)
                {
                    var resp = Hub.RegisterService(rsm, e.Context).ConfigureAwait(false).GetAwaiter().GetResult();
                    e.Result = resp;
                }
                else if (e.Value is ServiceSessionOperationMessage ssom)
                {
                    if (!ssom.Tick)
                    {
                        Hub.ServiceReady(ssom, conn, e.Context).ContinueWith((t,s) =>
                        {
                            TryRemoveChannel(conn);
                        },null,TaskContinuationOptions.None);
                    }
                    else
                    {
                        e.Result = Hub.ServiceTick(ssom, e.Context).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    LogEnvironment.LogDebugEvent($"Unexpected message: {e.Value.GetType()}", LogSeverity.Warning);
                }
            }
        }

        private void TryClose(IMemoryChannel channel)
        {
            try
            {
                channel.Write(new ConnectionDispose());
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent(ex.Message, LogSeverity.Warning);
            }
            finally
            {
                channel.ObjectReceived -= ClientComm;
                channel.ConnectionStatusChanged -= ClientConnectionChanged;
                channel.Dispose();
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            baseChannel.ObjectReceived -= ConnectionRequest;
            baseChannel.Dispose();
            if (ownsBroker)
            {
                Broker.Dispose();
            }

            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
