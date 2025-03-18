using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.EFRepo.Helpers
{
    internal class DbFacadeConnectionLock:IResourceLock
    {
        private readonly DatabaseFacade db;

        private bool doClose = false;
        
        /// <summary>
        /// Initializes a new instance of the DbFascadeConnectionLock class
        /// </summary>
        /// <param name="inner"></param>
        /// <param name="db"></param>
        public DbFacadeConnectionLock(DatabaseFacade db)
        {
            this.db = db;
            Connection = db.GetDbConnection();
            doClose = Connection.State == ConnectionState.Closed;
            if (doClose)
            {
                db.OpenConnection();
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            if (doClose)
            {
                db.CloseConnection();
            }
            //InnerLock?.Dispose();
        }

        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        public IResourceLock InnerLock { get; }

        public void Exclusive(bool autoLock, Action action)
        {
            action();
        }

        public T Exclusive<T>(bool autoLock, Func<T> action)
        {
            return action();
        }

        public void SynchronizeContext()
        {
            InnerLock?.SynchronizeContext();
        }

        public void LeaveSynchronizeContext()
        {
            InnerLock?.LeaveSynchronizeContext();
        }

        public IDisposable PauseExclusive()
        {
            return new ExclusivePauseHelper(() => InnerLock?.PauseExclusive());
        }

        /// <summary>
        /// Gets the Connection object of the provided db-facade object
        /// </summary>
        public DbConnection Connection { get; }
    }
}
