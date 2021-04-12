using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess.Extensions
{
    public static class ParallelExtensions
    {
        /// <summary>
        /// Extracts the transaction from a DB-ResourceLock object
        /// </summary>
        /// <param name="resourceLock">the lock that was recieved from a ConnectionBuffer - Object</param>
        /// <returns>the transaction that is associated with the current resourcelock</returns>
        public static ITransaction FindTransaction(this IResourceLock resourceLock)
        {
            IResourceLock tmp = resourceLock;
            while (tmp != null && !(tmp is ITransaction))
            {
                tmp = tmp.InnerLock;
            }

            return tmp as ITransaction;
        }
    }
}
