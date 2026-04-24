using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Events;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;
using ITVComponents.StateMachine.StatusTransit;
using ITVComponents.Threading;

namespace ITVComponents.StateMachine
{
    public class TransducerMachine<TStatus, TStatusTarget>:IDisposable where TStatusTarget : class
        where TStatus : Status<TStatus, TStatusTarget>
    {
        private bool ownsFactory;
        private IStatusFactory<TStatus,TStatusTarget> statusFactory;
        private TransitionCollection<TStatus, TStatusTarget> transitions;
        private Dictionary<string, ITransition<TStatus, TStatusTarget>[]> typeTransitions;
        private TStatus currentStatus;
        private readonly TStatusTarget target;
        private object locker = new object();
        private string currentStatusName;

        public TransducerMachine(IStatusFactory<TStatus,TStatusTarget> statusFactory, bool ownsFactory, TransitionCollection<TStatus, TStatusTarget> transitions,
            string initialStatus, TStatusTarget target)
        {
            this.ownsFactory = ownsFactory;
            this.statusFactory = statusFactory ?? throw new ArgumentNullException(nameof(statusFactory));
            this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
            var statusSrc = initialStatus ?? throw new ArgumentNullException(nameof(initialStatus));
            this.target = target;
            typeTransitions = BuildTypeTransitions();
            currentStatus = CreateStatus(initialStatus, null);
            AsyncHelpers.RunSync(EnterCurrentStatusAsync);
        }

        public async Task ExecuteAsync(RunArguments arguments)
        {
            var nextStatus = await currentStatus.RunAsync(arguments, this);
            TStatus prev = null;
            bool enterNextStatus;
            ITransition<TStatus, TStatusTarget> transition = null;
            if (nextStatus.UpStatus != null)
            {
                prev = nextStatus.UpStatus;
                enterNextStatus = false;
            }
            else
            {
                enterNextStatus = !nextStatus.KeepStatus;
                transition = nextStatus.Transition;
            }

            if (enterNextStatus)
            {
                //await NextStatusAsync(arguments);
                await SetNextStatus(transition.ToStatus, transition.TransitionType, arguments);
                await transition.ExecuteTransition(currentStatus, target, arguments);
            }
            else if (prev != null)
            {
                if (prev != currentStatus)
                {
                    await PrevStatusAsync(prev);
                }
            }
        }

        public void Execute(RunArguments arguments)
        {
            AsyncHelpers.RunSync(async () => await ExecuteAsync(arguments));
        }

        public TStatus Status => currentStatus;

        public string StatusName => currentStatusName;
        public int CurrentDepth { get; private set; }

        public async Task NextStatusAsync(RunArguments arguments)
        {
            ITransition<TStatus, TStatusTarget> transition;
            lock (locker)
            {
                var
                    selectedTransitions = //transitions.Where(n => n.FromStatus.IsAssignableFrom(currentStatus.GetType())
                        //&& n.Condition(arguments, currentStatus, target, this)).ToArray();
                        currentStatus.GetNextStatus(arguments);

                transition = selectedTransitions;
            }

            await SetNextStatus(transition.ToStatus, transition.TransitionType, arguments);
            await transition.ExecuteTransition(currentStatus, target, arguments);
        }

        public async Task SetNextStatus(string status, TransitionType transitionType, RunArguments arguments)
        {
            var nextStatus = CreateStatus(status, arguments, currentStatus);
            if (transitionType != TransitionType.SubStatusTransition)
            {
                await LeaveCurrentStatusAsync();
            }

            lock (locker)
            {
                currentStatus = nextStatus;
                currentStatusName = status;
            }

            await EnterCurrentStatusAsync();
            
        }

        public void Reset(string currentState)
        {
            if (!statusFactory.KnownStates.Contains(currentState))
            {
                throw new InvalidOperationException("Inavlid State selected.");
            }

            currentStatus = CreateStatus(currentState, null);
        }

        protected virtual void OnLeaveStatus()
        {
            StatusEventArgs<TStatus, TStatusTarget> ev;
            lock (locker)
            {
                ev = new StatusEventArgs<TStatus, TStatusTarget> { Status = currentStatus };
            }

            LeaveStatus?.Invoke(this, ev);
        }

        protected virtual void OnEnterStatus()
        {
            StatusEventArgs<TStatus, TStatusTarget> ev;
            lock (locker)
            {
                ev = new StatusEventArgs<TStatus, TStatusTarget> { Status = currentStatus };
            }

            EnterStatus?.Invoke(this, ev);
        }

        private async Task PrevStatusAsync(TStatus prevStatus)
        {
            await LeaveCurrentStatusAsync();
            currentStatus = prevStatus;
        }

        private async Task LeaveCurrentStatusAsync()
        {
            await currentStatus.ExitAsync();
            OnLeaveStatus();
        }

        private async Task EnterCurrentStatusAsync()
        {
            await currentStatus.EnterAsync();
            OnEnterStatus();
        }

        private TStatus CreateStatus(string statusType, RunArguments arguments, TStatus fromStatus = null)
        {
            var retVal = statusFactory.ConstructStatus(statusType, target, this, arguments, fromStatus);
            retVal.SetTransitions(typeTransitions[statusType], this);
            return retVal;
        }

        private Dictionary<string, ITransition<TStatus, TStatusTarget>[]> BuildTypeTransitions()
        {
            transitions.Lock();
            var retVal = new Dictionary<string, ITransition<TStatus, TStatusTarget>[]>();
            foreach (var status in statusFactory.KnownStates)
            {
                var tt = transitions.Where(n => n.FromStatus == status).ToArray();
                retVal.Add(status, tt);
            }

            return retVal;
        }

        public event EventHandler<StatusEventArgs<TStatus, TStatusTarget>> LeaveStatus;

        public event EventHandler<StatusEventArgs<TStatus, TStatusTarget>> EnterStatus;
        public void Dispose()
        {
            if (ownsFactory)
            {
                statusFactory.Dispose();
            }

            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {

        }
    }
}
