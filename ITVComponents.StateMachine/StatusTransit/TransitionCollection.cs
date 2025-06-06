using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Extensions;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Interfaces;

namespace ITVComponents.StateMachine.StatusTransit
{
    public class TransitionCollection<TStatus,TStatusTarget>
    where TStatusTarget:class
    where TStatus : Status<TStatus, TStatusTarget>
    {
        private List<ITransition<TStatus, TStatusTarget>> allTransitions = new List<ITransition<TStatus, TStatusTarget>>();
        private Dictionary<string, List<string>> statusGroups = new Dictionary<string, List<string>>();
        private bool locked = false;

        //private Dictionary<Type, List<Type>> peerList = new Dictionary<Type, List<Type>>();

        /*public bool IsPeerOf(Type statusA, Type statusB)
        {
            return peerList[statusA].Contains(statusB);
        }*/

        public ITransition<TStatus, TStatusTarget> FirstOrDefault(Func<ITransition<TStatus, TStatusTarget>, bool> filter)
        {
            return allTransitions.FirstOrDefault(filter);
        }

        public IEnumerable<ITransition<TStatus, TStatusTarget>> Where(
            Func<ITransition<TStatus, TStatusTarget>, bool> filter)
        {
            return allTransitions.Union((from t in allTransitions join g in statusGroups on t.FromStatus equals g.Key select g.Value.Select(nt => t.WithAlternativeSource(nt))).SelectMany(n => n)).Where(filter);
        }

        public void AddGroupMember(string groupName, string statusName)
        {
            var l = statusGroups.GetOrInsert(groupName, c => new List<string>());
            l.Add(statusName);
        }

        public void Add(ITransition<TStatus, TStatusTarget> transition)
        {
            if (locked)
            {
                throw new InvalidOperationException("Can not add a transition to a locked transitionCollection.");
            }
            /*bool validTransition = false;
            var fromHasPeers = peerList.TryGetValue(transition.FromStatus, out var fromPeers);
            if (!fromHasPeers)
            {
                fromPeers = new List<Type>();
                peerList[transition.FromStatus] = fromPeers;
                fromPeers.Add(transition.FromStatus);
                fromHasPeers = true;
                validTransition = true;
            }

            var toHasPeers = peerList.TryGetValue(transition.ToStatus, out var toPeers);

            if (!toHasPeers && transition.TransitionType == TransitionType.NormalTransition)
            {
                peerList[transition.ToStatus] = fromPeers;
                fromPeers.Add(transition.ToStatus);
                toHasPeers = true;
                toPeers = fromPeers;
                validTransition = true;
            }

            if (!toHasPeers && transition.TransitionType == TransitionType.SubStatusTransition)
            {
                toPeers = new List<Type>();
                peerList[transition.ToStatus] = toPeers;
                toPeers.Add(transition.ToStatus);
                toHasPeers = true;
                validTransition = true;
            }

            if (!validTransition)
            {
                throw new InvalidOperationException("Invalid Transition route detected!");
            }*/

            allTransitions.Add(transition);
        }

        public void Lock()
        {
            locked = true;
        }
    }
}
