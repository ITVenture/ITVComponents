using System;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    /// <summary>
    /// Marker Attribute used to indicate on ServerObjects that calls invoked by clients need to be don in the client's usercontext instead of the ServiceUser context
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class UserDelegationAttribute: Attribute
    {
        /// <summary>
        /// The Local-Thread name on which to set the user that is calling
        /// </summary>
        /// <param name="threadSettingName">the name of the user in the current thread</param>
        public UserDelegationAttribute(string threadSettingName)
        {
            ThreadSettingName = threadSettingName;
        }

        /// <summary>
        /// Gets or sets the setting Name of the local thread into which the userdelegation object must be stored. If a name is provided the context is not switched automatically and must be switched by the method itself
        /// </summary>
        public string ThreadSettingName { get; private set; }
    }
}
