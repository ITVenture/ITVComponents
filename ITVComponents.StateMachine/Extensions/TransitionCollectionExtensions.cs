using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Models;
using ITVComponents.StateMachine.StatusTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.DefaultImplementations;

namespace ITVComponents.StateMachine.Extensions
{
    public static class TransitionCollectionExtensions
    {
        public static TransitionCollection<TStatus, TStatusTarget> Transition<TStatus, TStatusTarget>(
            this TransitionCollection<TStatus, TStatusTarget> collection,
            string fromStatus, string toStatus,
            Func<RunArguments, Status<TStatus,TStatusTarget>, TStatusTarget, TransducerMachine<TStatus, TStatusTarget>, bool> condition,
            Func<TStatus, TStatusTarget, RunArguments, Task> executeTransition = null,
            string description = null)
            where TStatusTarget : class where TStatus : Status<TStatus, TStatusTarget>
        {
            collection.Add(new Transition<TStatus, TStatusTarget>(fromStatus, toStatus, TransitionType.NormalTransition,
                description, executeTransition, condition));

            return collection;
        }

        public static TransitionCollection<TStatus, TStatusTarget> SubTransition<TStatus, TStatusTarget>(
            this TransitionCollection<TStatus, TStatusTarget> collection,
            string fromStatus, string toStatus,
            Func<RunArguments, Status<TStatus, TStatusTarget>, TStatusTarget, TransducerMachine<TStatus, TStatusTarget>, bool> condition,
            Func<TStatus, TStatusTarget, RunArguments, Task> executeTransition = null,
            string description = null)
            where TStatusTarget : class where TStatus : Status<TStatus, TStatusTarget>
        {
            collection.Add(new Transition<TStatus, TStatusTarget>(fromStatus, toStatus, TransitionType.SubStatusTransition,
                description, executeTransition, condition));

            return collection;
        }

        public static TransitionCollection<TStatus, TStatusTarget> GroupMember<TStatus, TStatusTarget>(
            this TransitionCollection<TStatus, TStatusTarget> collection,
            string groupName, string memberStatusName) where TStatus : Status<TStatus, TStatusTarget> where TStatusTarget : class
        {
            collection.AddGroupMember(groupName,memberStatusName);
            return collection;
        }
    }
}
