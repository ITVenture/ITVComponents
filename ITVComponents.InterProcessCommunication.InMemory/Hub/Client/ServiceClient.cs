using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Factory;
using ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Client
{
    internal class ServiceClient : IServiceHubClientChannel
    {
        private const int reconnectInterval = 3000;
        private readonly IMemoryChannel channel;
        private readonly IMemoryChannel baseChannel;
        private readonly IHubFactory hubFactory;
        private readonly AsyncBackStream backStream;

        public ServiceClient(IMemoryChannel channel, IMemoryChannel baseChannel, IHubFactory hubFactory, AsyncBackStream backStream)
        {
            this.channel = channel;
            this.baseChannel = baseChannel;
            this.hubFactory = hubFactory;
            this.backStream = backStream;
            channel.ObjectReceived += IncomingChannelData;
            channel.ConnectionStatusChanged += LosingConnection;
        }

        public void CommitServiceOperation(ServiceOperationResponseMessage ret)
        {
            channel.Write(ret);
        }

        public ServiceOperationResponseMessage ConsumeService(ServerOperationMessage serverOperationMessage)
        {
            return ConsumeServiceAsync(serverOperationMessage).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<ServiceOperationResponseMessage> ConsumeServiceAsync(ServerOperationMessage serverOperationMessage)
        {
            var ret = await channel.Request(serverOperationMessage);
            return (ServiceOperationResponseMessage)ret;
        }

        public ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage serviceDiscoverMessage)
        {
            try
            {
                var ret = channel.Request(serviceDiscoverMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                return (ServiceDiscoverResponseMessage)ret;
            }
            catch(Exception ex)
            {
                LogEnvironment.LogDebugEvent(ex.OutlineException(), LogSeverity.Error);
                throw;
            }
        }

        public void Dispose()
        {
            baseChannel.Dispose();
            channel.Dispose();
            backStream.Dispose();
        }

        public RegisterServiceResponseMessage RegisterService(RegisterServiceMessage mySvc)
        {
            var ret = channel.Request(mySvc).ConfigureAwait(false).GetAwaiter().GetResult();
            return (RegisterServiceResponseMessage)(ret);
        }

        public void ServiceReady(ServiceSessionOperationMessage session)
        {
            channel.Write(session);
        }

        public void ServiceTick(ServiceSessionOperationMessage session)
        {
            try
            {
                channel.Request(session).GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                LogEnvironment.LogDebugEvent($"Error sending Tick: {ex.Message}", LogSeverity.Error);
            }
        }

        protected virtual void OnBroken()
        {
            Broken?.Invoke(this, EventArgs.Empty);
        }

        private void LosingConnection(object sender, EventArgs e)
        {
            if (!channel.Connected)
            {
                OnBroken();
            }
        }

        private void IncomingChannelData(object sender, ObjectReceivedEventArgs e)
        {
            if (e.Value is ServerOperationMessage som)
            {
                backStream.PushMessage(som);
            }
            else if (e.Value is ConnectionDispose)
            {
                OnBroken();
            }
            else
            {
                LogEnvironment.LogDebugEvent($"Unexpected message: {e.Value.GetType()}", LogSeverity.Warning);
            }

            //throw new InvalidOperationException($"Message is not supposed to arrive by event! Type: {e.Value.GetType()}");
        }

        public event EventHandler Broken;
    }
}
