using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.Logging;

namespace ITVComponents.ParallelProcessing.TaskSchedulers.Requests
{
    public class PeriodScheduleRequest : TaskScheduler.ScheduleRequest
    {
        [NonSerialized] private PeriodScheduler parent;
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
            this.parent = parent;
            deferredInstructions.ForEach(AddInstruction);
            deferredInstructions.Clear();
        }

        /// <summary>
        /// Initializes a new instance of the PeriodScheduleRequest
        /// </summary>
        /// <param name="info">the serialization info</param>
        /// <param name="context">the serialization context</param>
        public PeriodScheduleRequest(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            NextExecution = (DateTime?) info.GetValue("NextExecution", typeof (DateTime?));
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
            if (parent != null)
            {
                if (!string.IsNullOrEmpty(instruction))
                {
                    TimeTable tmp = parent.GetTimeTable(instruction);
                    DateTime? nx = tmp.GetNextExecutionTime(LastExecution);
                    if (nx != null &&
                        (NextExecution == null || nx.Value < NextExecution.Value || NextExecution.Value == DateTime.MinValue))
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
            else
            {
                deferredInstructions.Add(instruction);
            }
        }

        /// <summary>
        /// Füllt eine <see cref="T:System.Runtime.Serialization.SerializationInfo"/> mit den Daten, die zum Serialisieren des Zielobjekts erforderlich sind.
        /// </summary>
        /// <param name="info">Die mit Daten zu füllende <see cref="T:System.Runtime.Serialization.SerializationInfo"/>. </param><param name="context">Das Ziel (siehe <see cref="T:System.Runtime.Serialization.StreamingContext"/>) dieser Serialisierung. </param><exception cref="T:System.Security.SecurityException">Der Aufrufer verfügt nicht über die erforderliche Berechtigung. </exception>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("NextExecution", NextExecution);
        }

        /// <summary>
        /// Integrates this schedulerequest after deserialization
        /// </summary>
        /// <param name="parent">the parent scheduler that processes this request</param>
        protected override void IntegrateRequest(TaskScheduler parent)
        {
            base.IntegrateRequest(parent);
            this.parent = (PeriodScheduler)parent;
        }
    }
}
