using ITVComponents.Decisions;

namespace ITVComponents.DataExchange.Import.Diagnostics
{
    /// <summary>
    /// Basic Event-Dumper that provides Event-Filtering
    /// </summary>
    public abstract class ImportDiagnosticDataDumper:IParserEventDumper
    {
        /// <summary>
        /// Gets a Decider that needs to be executed before a single record may be dumped to its target
        /// </summary>
        public IDecider<ParserEventRecord> DumpDecider => new SimpleDecider<ParserEventRecord>(false);

        /// <summary>
        /// Initializes this dumper for writing events to a target
        /// </summary>
        public abstract void InitializeForEventDump();

        /// <summary>
        /// Finalizes the Event-Dump on the current target
        /// </summary>
        public abstract void FinalizeEventDump();

        /// <summary>
        /// Dumps a single event the target of this dumper
        /// </summary>
        /// <param name="record">The Event-Record representing the Parser-Event that must be logged to this dumpers Target</param>
        public void DumpEvent(ParserEventRecord record)
        {
            string msg;
            if ((DumpDecider.Decide(record, DecisionMethod.Simple, out msg) &
                 (DecisionResult.Success | DecisionResult.Acceptable)) != DecisionResult.None)
            {
                DumpEventRecord(record);
            }
        }

        /// <summary>
        /// Dumps a required event to the target of this dumper
        /// </summary>
        /// <param name="record">the record that has passed the dump-decider</param>
        protected abstract void DumpEventRecord(ParserEventRecord record);
    }
}
