using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataExchange.Import
{
    public interface IImportSource<T, in TNotification> where TNotification:AcceptanceCallbackParameter<T>
                                                                                   where T:class
    {
        /// <summary>
        /// Registers a Textconsumer to the list of data recipients
        /// </summary>
        /// <param name="consumer">a consumer that must be triggered in order to process text that is being read from a specific source</param>
        void Register(IImportConsumer<T, TNotification> consumer);

        /// <summary>
        /// Starts the Consuption of data
        /// </summary>
        void StartConsumption();
    }
}
