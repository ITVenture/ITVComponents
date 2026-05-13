using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class AssemblyPartTypeLoadBehaviorExtensions
    {
        public static bool ShouldLoadType(this AssemblyPartTypeLoadBehaviorOptions typeLoadConfig, Type typeToCheck)
        {
            var retVal = typeLoadConfig.DefaultBehavior == TypeRegisterBehavior.Use;
            if (typeLoadConfig.CustomLoadings != null)
            {
                var over = typeLoadConfig.CustomLoadings.FirstOrDefault(n => n.Type == typeToCheck);
                if (over != null && over.LoadBehavior != TypeRegisterBehavior.Default)
                {
                    retVal = over.LoadBehavior == TypeRegisterBehavior.Use;
                }
            }

            return retVal;
        }
    }
}
