using System.Collections.Generic;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// The targets that share one MSI, i.e. the same UpgradeCode (issue ModulePlugins-Instances, D). The first target of
    /// the configuration is the lead: only it installs the MSI and, before that, ends the applications of all members.
    /// </summary>
    /// <remarks>
    /// At most one member holds the group at a time: the lead for an installation of the MSI (the whole group), or any
    /// member for a pass of its own modules. A member whose group is held by another one skips its passes.
    /// </remarks>
    internal sealed class InstallGroup
    {
        private readonly object sync = new object();
        private readonly List<UpdateCoordinator> members = new List<UpdateCoordinator>();
        private UpdateCoordinator? holder;
        private bool whole;

        public InstallGroup(string upgradeCode)
        {
            UpgradeCode = upgradeCode;
        }

        /// <summary>Gets the UpgradeCode the members share.</summary>
        public string UpgradeCode { get; }

        /// <summary>Gets the members in the order of the configuration.</summary>
        public IReadOnlyList<UpdateCoordinator> Members => members;

        /// <summary>Gets the member that installs the MSI.</summary>
        public UpdateCoordinator Lead => members[0];

        internal void Add(UpdateCoordinator member)
        {
            members.Add(member);
        }

        /// <summary>
        /// Takes the group for a member, unless another one holds it.
        /// </summary>
        /// <param name="member">the member</param>
        /// <param name="wholeGroup">whether the applications of every member are to end (an installation of the MSI)</param>
        public bool TryHold(UpdateCoordinator member, bool wholeGroup)
        {
            lock (sync)
            {
                if (holder != null && holder != member)
                {
                    return false;
                }

                holder = member;
                whole |= wholeGroup;
                return true;
            }
        }

        /// <summary>
        /// Gives the group free when the member holds it, and wakes the others.
        /// </summary>
        /// <returns>whether the member held the group for all members</returns>
        public bool Release(UpdateCoordinator member)
        {
            bool wasWhole;
            lock (sync)
            {
                if (holder != member)
                {
                    return false;
                }

                wasWhole = whole;
                holder = null;
                whole = false;
            }

            foreach (var other in members)
            {
                if (other != member)
                {
                    other.WakeFromGroup();
                }
            }

            return wasWhole;
        }

        /// <summary>Gets the member that holds the group, when it is not the given one.</summary>
        public UpdateCoordinator? HeldByOther(UpdateCoordinator member)
        {
            lock (sync)
            {
                return holder != null && holder != member ? holder : null;
            }
        }

        /// <summary>Gets a value indicating whether another member keeps the applications of all members down.</summary>
        public bool WholeGroupHeldByOther(UpdateCoordinator member)
        {
            lock (sync)
            {
                return whole && holder != null && holder != member;
            }
        }
    }
}
