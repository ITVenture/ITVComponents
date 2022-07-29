using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using System.Text.Json.Serialization;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Formatters.Json;
using NJS=Newtonsoft.Json.JsonConverterAttribute;
using NSEC = Newtonsoft.Json.Converters.StringEnumConverter;
using TSEC = System.Text.Json.Serialization.JsonStringEnumConverter;
using TJS = System.Text.Json.Serialization.JsonConverterAttribute;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model
{
    /// <summary>
    /// Defines valid states of an Application
    /// </summary>
    [NJS(typeof(NSEC))]
    [TJS(typeof(StringEnumConverterEx))]
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
