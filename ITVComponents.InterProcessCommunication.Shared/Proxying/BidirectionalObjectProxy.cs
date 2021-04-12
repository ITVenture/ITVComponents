using System;
using ITVComponents.InterProcessCommunication.Shared.Base;

namespace ITVComponents.InterProcessCommunication.Shared.Proxying
{
    public class BidirectionalObjectProxy:ObjectProxy
    {
        /// <summary>
        /// the consumer object that is used to communicate with the target service
        /// </summary>
        private IBidirectionalClient consumer;

        /// <summary>
        /// the targetObject that is wrapped by this bidirectional proxy
        /// </summary>
        private string objectName;

        /// <summary>
        /// Initializes a new instance of the BidirectionalObjectProxy class
        /// </summary>
        /// <param name="serviceClient">the client object that wrapps the connection</param>
        /// <param name="objectName">the name of the wrapped object</param>
        /// <param name="expectedServiceType">the exptected object</param>
        public BidirectionalObjectProxy(IBidirectionalClient serviceClient, string objectName, Type expectedServiceType) : base(serviceClient, objectName, expectedServiceType)
        {
            consumer = serviceClient;
            this.objectName = objectName;
        }

        /// <summary>
        /// Enables a child object to add a specific event-subscription
        /// </summary>
        /// <param name="eventName">the name of the target-event</param>
        /// <param name="target">the delegate to add to the subscription</param>
        protected override void AddEventSubscription(string eventName, Delegate target)
        {
            consumer.SubscribeEvent(objectName, eventName, target);
        }

        /// <summary>
        /// Enables a child object to remove a specific event-subscription
        /// </summary>
        /// <param name="eventName">the name of the target-event</param>
        /// <param name="target">the deletage to remove from the subscription</param>
        protected override void RemoveEventSubscription(string eventName, Delegate target)
        {
            consumer.UnSubscribeEvent(objectName, eventName, target);
        }
    }
}
