using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model
{
    /// <summary>
    /// Defines valid states of an Application
    /// </summary>
    public enum UpState
    {
        /// <summary>
        /// Indicates that the application is in a healthy state
        /// </summary>
        [EnumMember(Value="UP")]
        Up,
        /// <summary>
        /// Indicates that the application is in an unhealthy state
        /// </summary>
        [EnumMember(Value="DOWN")]
        Down
    }
}
