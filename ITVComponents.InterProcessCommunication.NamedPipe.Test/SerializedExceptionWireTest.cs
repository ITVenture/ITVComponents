using System;
using System.Linq;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Test
{
    /// <summary>
    /// A remote error keeps its collected details on the way back (the error response of the message server).
    /// </summary>
    [TestClass]
    public class SerializedExceptionWireTest
    {
        [TestMethod]
        public void The_Collected_Details_Of_A_Remote_Error_Arrive_With_Their_Names()
        {
            SerializedException original = new ArgumentOutOfRangeException("amount", "must be positive");
            Assert.AreEqual("amount", original["ParamName"], "precondition: the detail is collected");

            var json = JsonHelper.ToJson(new ErrorResponse { SerializedException = original }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
            var back = (ErrorResponse)JsonHelper.FromJsonString<IServerResponse>(json, SerializationTypingMode.NativePolymorphism, true);

            Assert.AreEqual("amount", back.SerializedException["ParamName"], json);
            CollectionAssert.AreEquivalent(original.InformationKeys, back.SerializedException.InformationKeys, json);
            Assert.AreEqual(original.Message, back.SerializedException.Message);
        }

        [TestMethod]
        public void Details_Of_Inner_Errors_Arrive_As_Well()
        {
            SerializedException original = new InvalidOperationException("outer", new ArgumentException("inner", "name"));

            var json = JsonHelper.ToJson(new ErrorResponse { SerializedException = original }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
            var back = (ErrorResponse)JsonHelper.FromJsonString<IServerResponse>(json, SerializationTypingMode.NativePolymorphism, true);

            Assert.AreEqual("name", back.SerializedException.InnerException.Single()["ParamName"], json);
        }
    }
}
