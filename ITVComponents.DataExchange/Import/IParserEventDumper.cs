using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.Decisions;

namespace ITVComponents.DataExchange.Import
{
    public interface IParserEventDumper
    {
        /// <summary>
        /// Gets a Decider that needs to be executed before a single record may be dumped to its target
        /// </summary>
        IDecider<ParserEventRecord> DumpDecider { get; }

        /// <summary>
        /// Initializes this dumper for writing events to a target
        /// </summary>
        void InitializeForEventDump();

        /// <summary>
        /// Finalizes the Event-Dump on the current target
        /// </summary>
        void FinalizeEventDump();

        /// <summary>
        /// Dumps a single event the target of this dumper
        /// </summary>
        /// <param name="record">The Event-Record representing the Parser-Event that must be logged to this dumpers Target</param>
        void DumpEvent(ParserEventRecord record);
    }
}
