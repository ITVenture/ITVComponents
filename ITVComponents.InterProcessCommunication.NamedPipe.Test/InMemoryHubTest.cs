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
        /// Arguments and results travel in the envelope format (issue Json 01): derived types in base-typed parameters,
        /// generic lists as such, and the exact numeric type for overload resolution.
        /// </summary>
        [TestMethod]
        public void Arguments_And_Results_Keep_Their_Runtime_Types()
        {
            var scaled = CallWhenReady(() => client.CallRemoteMethod(ObjectName, nameof(Calculator.Scale), new object[] { new Circle { Radius = 2 }, 1.5 }));
            Assert.IsInstanceOfType<Circle>(scaled);
            Assert.AreEqual(3.0, ((Circle)scaled).Radius);

            var items = new List<Item> { new Item { Name = "a" }, new Item { Name = "b" } };
            var echoed = client.CallRemoteMethod(ObjectName, nameof(Calculator.Reverse), new object[] { items });
            Assert.IsInstanceOfType<List<Item>>(echoed);
            Assert.AreEqual("b", ((List<Item>)echoed)[0].Name);

            Assert.AreEqual("int", client.CallRemoteMethod(ObjectName, nameof(Calculator.Kind), new object[] { 5 }));
            Assert.AreEqual("long", client.CallRemoteMethod(ObjectName, nameof(Calculator.Kind), new object[] { 5L }));

            var guid = Guid.NewGuid();
            Assert.AreEqual<object>(guid, client.CallRemoteMethod(ObjectName, nameof(Calculator.Echo), new object[] { guid }));
            Assert.AreEqual<object>(1.25m, client.CallRemoteMethod(ObjectName, nameof(Calculator.Echo), new object[] { 1.25m }));
            Assert.IsNull(client.CallRemoteMethod(ObjectName, nameof(Calculator.Echo), new object?[] { null }));
        }

        [TestMethod]
        public void Restricted_Argument_Types_Accept_Registered_And_Refuse_Other_Types()
        {
            JsonHelper.RegisterManualType<Item>("ipctest.item");
            server.RestrictArgumentTypes = true;

            var echoed = CallWhenReady(() => client.CallRemoteMethod(ObjectName, nameof(Calculator.Echo), new object[] { new Item { Name = "ok" } }));
            Assert.AreEqual("ok", ((Item)echoed).Name);

            // Circle is not registered: the server does not load it, the argument arrives as null (§110.2).
            Assert.IsNull(client.CallRemoteMethod(ObjectName, nameof(Calculator.Echo), new object[] { new Circle { Radius = 2 } }));
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

            public Shape Scale(Shape shape, double factor)
            {
                return shape is Circle c ? new Circle { Radius = c.Radius * factor } : shape;
            }

            public List<Item> Reverse(List<Item> items)
            {
                return Enumerable.Reverse(items).ToList();
            }

            public string Kind(int value) => "int";

            public string Kind(long value) => "long";

            public object? Echo(object? value) => value;
        }

        public abstract class Shape
        {
        }

        public class Circle : Shape
        {
            public double Radius { get; set; }
        }

        public class Item
        {
            public string? Name { get; set; }
        }
    }
}
