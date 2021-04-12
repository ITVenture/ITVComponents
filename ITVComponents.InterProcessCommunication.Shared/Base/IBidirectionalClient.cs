using System;

namespace ITVComponents.InterProcessCommunication.Shared.Base
{
    public interface IBidirectionalClient:IBaseClient
    {
        /// <summary>
        /// Subscribes for a specific event
        /// </summary>
        /// <param name="objectName">the name of the event-providing object</param>
        /// <param name="eventName">the name of the event</param>
        /// <param name="handler">the handler to be added to the list</param>
        void SubscribeEvent(string objectName, string eventName, Delegate handler);

        /// <summary>
        /// Removes a specific handler from the subscription list
        /// </summary>
        /// <param name="objectName">the name of the event-providing object</param>
        /// <param name="eventName">the name of the event</param>
        /// <param name="handler">the handler to be removed from the subscription list</param>
        void UnSubscribeEvent(string objectName, string eventName, Delegate handler);

        /// <summary>
        /// Removes all Event Subscriptions from the serverobject
        /// </summary>
        /// <param name="objectName">the objectname from which to remove all subscriptions</param>
        void RemoveAllSubscriptions(string objectName);
    }
}
