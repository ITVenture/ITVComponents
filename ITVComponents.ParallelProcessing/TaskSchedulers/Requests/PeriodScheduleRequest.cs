using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;

namespace ITVComponents.ParallelProcessing.TaskSchedulers.Requests
{
    public class PeriodScheduleRequest : TaskScheduler.ScheduleRequest
    {
        [NonSerialized] private List<string> deferredInstructions = new List<string>();

        /// <summary>
        /// Initializes a new instance of the PeriodScheduleRequest class
        /// </summary>
        /// <param name="schedulerName">the name of the responsible Scheduler</param>
        /// <param name="targetProcessor">the task processor that will process the request</param>
        /// <param name="task"></param>
        /// <param name="instruction"></param>
        /// <param name="parent"></param>
        /// <param name="lastExecution"></param>
        public PeriodScheduleRequest(string schedulerName, ParallelTaskProcessor targetProcessor, ITask task,
                                     PeriodScheduler parent, DateTime? lastExecution = null)
            : base(schedulerName, targetProcessor, task, lastExecution)
        {
            deferredInstructions.ForEach(AddInstruction);
            deferredInstructions.Clear();
        }

        /// <summary>
        /// Gets or sets the next execution of this schedule-Request
        /// </summary>
        public DateTime? NextExecution { get; private set; }

        /// <summary>
        /// Adds the instruction to the list of schedule-instructions and sets the remarks if requested
        /// </summary>
        /// <param name="instruction">the instruction that is used for this schedule-request</param>
        /// <param name="setRemarks">indicates whether to use the default-remarks</param>
        protected override void AddInstruction(string instruction, bool setRemarks)
        {
            bool setBaseRemarks = setRemarks;
            if (!string.IsNullOrEmpty(instruction))
            {
                TimeTable tmp = PeriodScheduler.GetTimeTable(instruction);
                DateTime? nx = tmp.GetNextExecutionTime(LastExecution);
                if (nx != null &&
                    (NextExecution == null || nx.Value < NextExecution.Value ||
                     NextExecution.Value == DateTime.MinValue))
                {
                    LogEnvironment.LogDebugEvent(string.Format("Next Execution: {0:dd.MM.yyyy HH:mm:ss}", nx.Value),
                        LogSeverity.Report);
                    //LogEnvironment.LogEvent(new StackTrace().ToString(), LogSeverity.Report);
                    NextExecution = nx.Value;
                    if (setRemarks)
                    {
                        Remarks = string.Format("Next Execution: {0:dd.MM.yyyy HH:mm:ss}", NextExecution);
                        setBaseRemarks = false;
                    }
                }
            }
            else
            {
                LogEnvironment.LogEvent($"Ignored empty Scheduler instruction...", LogSeverity.Warning);
            }

            base.AddInstruction(instruction, setBaseRemarks);
        }

        public override void GetObjectData()
        {
            base.GetObjectData();
            Data.Add(ManualSerializationData.FromValue("NextExecution", NextExecution));
        }

        public override void ApplyObjectData()
        {
            base.ApplyObjectData();
            NextExecution = Data.GetDeserializedValue<DateTime?>("NextExecution");
        }
    }
}
