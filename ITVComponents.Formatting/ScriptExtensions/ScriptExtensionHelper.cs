using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core.ExternalMethods;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;

namespace ITVComponents.Formatting.ScriptExtensions
{
    public static class ScriptExtensionHelper
    {
        static ScriptExtensionHelper()
        {
            ExternalMethodHelper.RegisterClass(typeof(ScriptExtensionHelper));
        }

        public static void Register()
        {
        }

        [ExternalMethod(MappedMethodName = "$$")]
        public static string ScriptFormat([DefaultParameter(FixtureName = "session")]
            IDisposable context, string format)
        {
            return TextFormat.FormatText(context, format);
        }
    }
}
