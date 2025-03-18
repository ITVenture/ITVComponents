using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ITVComponents.Json.Contracts;

namespace ITVComponents.ParallelProcessing.TaskSchedulers.Requests
{
    public class DeferredScheduleRequest:TaskScheduler.ScheduleRequest
    {
        /// <summary>
        /// Initializes a new instance of the DeferredScheduleRequest class
        /// </summary>
        /// <param name="schedulerName">the name of the responsible Scheduler</param>
        /// <param name="targetProcessor">the target processor</param>
        /// <param name="task">the task to process</param>
        /// <param name="instruction">the instruction for the scheduler</param>
        /// <param name="lastExecution">the last execution date</param>
        public DeferredScheduleRequest(string schedulerName, ParallelTaskProcessor targetProcessor, ITask task, DateTime? lastExecution = null) : base(schedulerName, targetProcessor, task, lastExecution)
        {
        }

        /// <summary>
        /// Gets the next execution time for this request
        /// </summary>
        public DateTime NextExecution { get; private set; }

        /// <summary>
        /// Adds an instruction to the list of instructions for this schedule request
        /// </summary>
        /// <param name="instruction">the instruction to be executed when checking this request</param>
        protected override void AddInstruction(string instruction, bool setRemarks)
        {
            bool setBaseRemarks = setRemarks;
            DateTime tmp = GetNextExecutionTime(instruction);
            if (tmp < NextExecution || NextExecution == DateTime.MinValue)
            {
                NextExecution = tmp;
                if (setRemarks)
                {
                    Remarks = string.Format("Next Execution: {0:dd.MM.yyyy HH:mm:ss}", NextExecution);
                    setBaseRemarks = false;
                }
            }

            base.AddInstruction(instruction, setBaseRemarks);
        }

        /// <summary>
        /// Gets the next execution time for a specific scheduler instruction
        /// </summary>
        /// <param name="schedulerInstruction">the scheduler instruction that was provided for a specific task</param>
        /// <returns>the next execution time for the task</returns>
        private DateTime GetNextExecutionTime(string schedulerInstruction)
        {
            int seconds;
            if (int.TryParse(schedulerInstruction, out seconds))
            {
                return LastExecution.AddSeconds(seconds);
            }

            return DateTime.Now;
        }

        public override void GetObjectData()
        {
            base.GetObjectData();
            Data.Add(ManualSerializationData.FromValue("NextExecution", NextExecution));
        }

        public override void ApplyObjectData()
        {
            base.ApplyObjectData();
            NextExecution = Data.GetDeserializedValue<DateTime>("NextExecution");
        }
    }
}
