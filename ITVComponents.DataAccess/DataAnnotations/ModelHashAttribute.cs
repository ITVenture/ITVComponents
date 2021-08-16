using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Helpers;

namespace ITVComponents.DataAccess.DataAnnotations
{
    public class ModelHashAttribute : CustomValueSourceAttribute
    {
        private readonly string hashAlgorithm;

        public ModelHashAttribute(string hashAlgorithm)
        {
            this.hashAlgorithm = hashAlgorithm;
        }
        protected internal override object GetCustomValueFor(object originalObject, Func<Type, object> requestInstance)
        {
            if (originalObject is string s)
            {
                return HashHelper.CalculateHash(s, hashAlgorithm);
            }

            return null;
        }
    }
}