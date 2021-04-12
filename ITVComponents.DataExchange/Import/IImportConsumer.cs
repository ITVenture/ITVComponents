using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Annotations;
using ITVComponents.Decisions;

namespace ITVComponents.DataExchange.Import
{
    public interface IImportConsumer<T, out TNotification> where TNotification:AcceptanceCallbackParameter<T>
                                                                                     where T: class
    {
        /// <summary>
        /// Gets further informations about when the importer will Accept data to import
        /// </summary>
        IDecider<T> AcceptanceConstraints { get; }

        /// <summary>
        /// Gets a value indicating whether any data has been consumed by this ImportConsumer
        /// </summary>
        bool ConsumedAnyData { get; }

        /// <summary>
        /// Consumes the provided text and returns the initial character position of recognition
        /// </summary>
        /// <param name="data">the text that was extracted from a TextSource</param>
        /// <param name="acceptanceCallback">A Callback that is used by the importer to commit the acceptance of a Part of the delivered data</param>
        /// <param name="logCallback">a callback that can be used to log events on the parser-process</param>
        /// <returns>a value indicating whether the data was successfully processed</returns>
        bool Consume(T data, AcceptanceCallback<TNotification,T> acceptanceCallback, LogParserEventCallback<T> logCallback);

        /// <summary>
        /// Is being called from the ImportSource when the Consuption of data is being started
        /// </summary>
        void ConsumptionStarts();

        /// <summary>
        /// Indicates that the End of the underlaying file was reached and therefore the current record has finished.
        /// </summary>
        void EndOfFile();
    }
}
