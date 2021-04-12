using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Models;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ITVComponents.EFRepo.Extensions
{
    public static class DbFacadeExtensions
    {
        public static List<T> SqlQuery<T>(this DatabaseFacade facade, string query, params DbParameter[] arguments) where T : new()
        {
            using (facade.UseConnection(out DbCommand cmd))
            {
                cmd.CommandText = query;
                cmd.Parameters.AddRange(arguments);
                return cmd.ExecuteReader().GetModelResult<T, T>().ToList();
            }
        }

        /// <summary>
        /// Creates a command object that will be executed throgh the connection inside the provided db-facade object
        /// </summary>
        /// <param name="facade">the db-facade object of an entity context</param>
        /// <returns>an executable command object</returns>
        public static IDisposable UseConnection(this DatabaseFacade facade, out DbCommand command)
        {
            var retVal = facade.UseConnection(out DbConnection connection);
            command = connection.CreateCommand();
            if (facade.CurrentTransaction != null)
            {
                var trans = facade.CurrentTransaction.GetDbTransaction();
                command.Transaction = trans;
            }
            return retVal;
        }

        /// <summary>
        /// Opens the connection if not opened and closes it when required
        /// </summary>
        /// <param name="facade">the db-facade object of an entity-context</param>
        /// <returns>an object that releases the underlaying connection when its no longer used</returns>
        public static IDisposable UseConnection(this DatabaseFacade facade, out DbConnection connection)
        {
            var retVal = new DbFacadeConnectionLock(facade);
            connection = retVal.Connection;
            return retVal;
        }
    }
}
