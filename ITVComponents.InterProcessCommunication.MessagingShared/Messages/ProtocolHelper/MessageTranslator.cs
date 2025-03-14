using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Json;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages.ProtocolHelper
{
    public static class MessageTranslator
    {
        internal static void RegisterMessages()
        {
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(TypedArray), "TypedArray");
        }

        /// <summary>
        /// Tests the received message and returns it, if its the expected type or throws an exception otherwise
        /// </summary>
        /// <typeparam name="TExpectedType">the expected incoming type</typeparam>
        /// <param name="message">the received message</param>
        /// <returns>the parsed message</returns>
        public static async Task<TExpectedType> TestServerMessage<TExpectedType>(this Task<string> msgFunc, Action<SerializedException> testConnection = null, Action<Exception> testConnectionX = null) where TExpectedType : class, IServerResponse
        {
            IServerResponse tmp = null;
            object error = null;
            string message = null;
            try
            {
                message = await msgFunc.ConfigureAwait(false);
                tmp = TestProtocolMessage<IServerResponse>(message);
                if (tmp is TExpectedType ret)
                {
                    return ret;
                }

                if (tmp is ErrorResponse err)
                {
                    error = err.SerializedException;
                }
            }
            catch (Exception exx)
            {
                LogEnvironment.LogDebugEvent(null, $"Error processing message: {exx.OutlineException()}",
                    (int)LogSeverity.Error, "ITVComponents.IPC.MS.MessageClient");
                error = exx;
            }

            if (error is SerializedException ex)
            {
                testConnection?.Invoke(ex);
                throw new InterProcessException("Server-Operation failed!", ex);
            }

            if (error is Exception inex)
            {
                testConnectionX?.Invoke(inex);
                throw new InterProcessException("Server-Operation failed!", inex);
            }

            throw new InterProcessException($"Unexpected Response: {message}", null);
        }

        public static async Task<TExpectedType> TestClientMessage<TExpectedType>(this Task<string> msgFunc,
            Action<Exception> testConnectionX = null) where TExpectedType: class, IRequestMessage
        {
            IRequestMessage tmp = null;
            object error = null;
            string message = null;
            try
            {
                message = await msgFunc.ConfigureAwait(false);
                tmp = TestProtocolMessage<IRequestMessage>(message);
                if (tmp is TExpectedType ret)
                {
                    return ret;
                }
            }
            catch (Exception exx)
            {
                LogEnvironment.LogDebugEvent(null, $"Error processing message: {exx.OutlineException()}",
                    (int)LogSeverity.Error, "ITVComponents.IPC.MS.MessageClient");
                error = exx;
            }

            if (error is Exception inex)
            {
                testConnectionX?.Invoke(inex);
                throw new InterProcessException("Server-Operation failed!", inex);
            }

            throw new InterProcessException($"Unexpected Response: {message}", null);
        }

        private static T TestProtocolMessage<T>(string message)
        {
            return JsonHelper.FromJsonString<T>(message, SerializationTypingMode.NativePolymorphism, true);
        }
    }
}
