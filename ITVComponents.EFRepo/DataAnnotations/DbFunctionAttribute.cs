using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Method,Inherited = true)]
    public class DbFunctionAttribute:Attribute
    {
        public string FunctionName { get; }

        public DbFunctionAttribute(string functionName)
        {
            FunctionName = functionName;
        }
    }
}
