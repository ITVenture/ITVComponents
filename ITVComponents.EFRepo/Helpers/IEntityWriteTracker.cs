using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.Helpers
{
    /// <summary>
    /// Singleton service that records the last write-time for all entities.
    /// </summary>
    public interface IEntityWriteTracker<T>:IEntityWriteTracker where T : DbContext
    {
        
    }

    /// <summary>
    /// Singleton service that records the last write-time for all entities.
    /// </summary>
    public interface IEntityWriteTracker
    {
        /// <summary>
        /// Marks the given table as written to
        /// </summary>
        /// <param name="table">the name of the table that was written to</param>
        public void MarkWritten(string table);
        /// <summary>
        /// Gets the last write-time for the given table
        /// </summary>
        /// <param name="table">the name of the table to get the last write-time for</param>
        /// <returns>the last write-time for the given table</returns>
        public DateTime GetLastWrite(string table);
    }
}
