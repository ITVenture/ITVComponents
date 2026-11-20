using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Client;
using ITVComponents.InterProcessCommunication.InMemory.Hub;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Factory;
using ITVComponents.InterProcessCommunication.InMemory.Server;
using ITVComponents.InterProcessCommunication.MessagingShared;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.NamedPipe.Client;
using ITVComponents.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Test
{
    /// <summary>
    /// A service that registers through the address of an InMemory hub (bug PRE278): the hub answers the registration
    /// and the ticks with messages that must serialize as the payload of a channel response.
    /// </summary>
    [TestClass]
    public class InMemoryHubTest
    {
        private const string ServiceName = "calc";
        private const string ObjectName = "calculator";

        private string hubAddress = null!;
        private MessageServiceHub hub = null!;
        private ServiceHubChannel endPoint = null!;
        private Calculator calculator = null!;
        private InMemoryServer server = null!;
        private InMemoryClient client = null!;

        [TestInitialize]
        public void Setup()
        {
            hubAddress = $"itv-im-test-{Guid.NewGuid():N}";
            hub = new MessageServiceHub();
            endPoint = new ServiceHubChannel(hubAddress, new OpenHubFactory(), hub);
            endPoint.Initialize();
            calculator = new Calculator();
            server = new InMemoryServer(hubAddress, new Dictionary<string, object> { { ObjectName, calculator } }, new OpenHubFactory(), ServiceName);
            server.Initialize();
            client = new InMemoryClient(hubAddress, new OpenHubFactory(), ServiceName, false);
            client.Initialize();
        }

        [TestCleanup]
        public void Cleanup()
        {
            client.Dispose();
            server.Dispose();
            endPoint.Dispose();
            hub.Dispose();
        }

        [TestMethod]
        public void A_Service_That_Registers_Through_The_Hub_Address_Can_Be_Called()
        {
            Assert.AreEqual(5, CallWhenReady(() => client.CallRemoteMethod(ObjectName, nameof(Calculator.Add), new object[] { 2, 3 })));
        }

        [TestMethod]
        public void A_Property_Is_Read_And_Written_With_Its_Type()
        {
            calculator.Counter = 48;

            Assert.AreEqual(48, CallWhenReady(() => client.GetProperty(ObjectName, nameof(Calculator.Counter), Array.Empty<object>())));

            client.SetProperty(ObjectName, nameof(Calculator.Counter), Array.Empty<object>(), 7);
            Assert.AreEqual(7, calculator.Counter);
            Assert.AreEqual(7, client.GetProperty(ObjectName, nameof(Calculator.Counter), Array.Empty<object>()));

            client.SetProperty(ObjectName, nameof(Calculator.Label), Array.Empty<object>(), "till 1");
            Assert.AreEqual("till 1", client.GetProperty(ObjectName, nameof(Calculator.Label), Array.Empty<object>()));
        }

        [TestMethod]
        public void InMemory_And_NamedPipe_Agree_On_The_Hub_Answers_In_One_Process()
        {
            RuntimeHelpers.RunClassConstructor(typeof(MemoryServiceChannel).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(NamedPipeClient).Assembly.GetType("ITVComponents.InterProcessCommunication.NamedPipe.Channels.PipeMessageChannel", true)!.TypeHandle);

            var json = JsonHelper.ToJson(new RegisterServiceResponseMessage { Ok = true, SessionTicket = "ticket" }, SerializationTypingMode.NativePolymorphism, typeof(IProtocolMessage), true);
            StringAssert.Contains(json, "\"RegisterServiceResponse\"");
            var back = JsonHelper.FromJsonString<IProtocolMessage>(json, SerializationTypingMode.NativePolymorphism, true);
            Assert.AreEqual("ticket", ((RegisterServiceResponseMessage)back).SessionTicket);

            json = JsonHelper.ToJson(new ServiceTickResponseMessage { Ok = true, PendingOperationsCount = 2 }, SerializationTypingMode.NativePolymorphism, typeof(IProtocolMessage), true);
            StringAssert.Contains(json, "\"ServiceTickResponse\"");
            back = JsonHelper.FromJsonString<IProtocolMessage>(json, SerializationTypingMode.NativePolymorphism, true);
            Assert.AreEqual(2, ((ServiceTickResponseMessage)back).PendingOperationsCount);
        }

        /// <summary>
        /// The service registers in the background; calls before that fail.
        /// </summary>
        private static object CallWhenReady(Func<object> call)
        {
            var until = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                try
                {
                    return call();
                }
                catch (Exception ex) when (DateTime.UtcNow < until)
                {
                    Trace.WriteLine($"The service is not ready yet: {ex.OutlineException()}");
                    Thread.Sleep(250);
                }
            }
        }

        public sealed class Calculator
        {
            public int Counter { get; set; }

            public string? Label { get; set; }

            public int Add(int a, int b)
            {
                return a + b;
            }
        }
    }
}
