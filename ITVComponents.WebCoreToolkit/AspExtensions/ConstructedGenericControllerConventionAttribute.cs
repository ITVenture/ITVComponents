using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace ITVComponents.WebCoreToolkit.AspExtensions
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ConstructedGenericControllerConventionAttribute : Attribute, IControllerModelConvention
    {
        public string ControllerName { get; set; }

        public void Apply(ControllerModel controller)
        {
            
            if (!controller.ControllerType.IsGenericType)
            {
                return;
            }

            if (string.IsNullOrEmpty(ControllerName))
            {
                var id = controller.ControllerName.IndexOf("`");
                if (id != -1)
                {
                    controller.ControllerName = controller.ControllerName.Substring(0, id);
                }
            }
            else
            {
                controller.ControllerName = ControllerName;
            }

            if (controller.ControllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            {
                controller.ControllerName =
                    controller.ControllerName.Substring(0, controller.ControllerName.Length - "Controller".Length);
            }
        }
    }
}
