using System;

namespace ITVComponents.DataAccess.Parallel
{
    /// <summary>
    /// Database Container class containing a Connection and its last usage by a consumer
    /// </summary>
    internal class DatabaseContainer
    {
        /// <summary>
        /// Gets or sets the Database connection presented by this Container
        /// </summary>
        public IDbWrapper Database { get; set; }

        /// <summary>
        /// Gets or sets the Last usage of the contained connection
        /// </summary>
        public DateTime LastUsage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the inner database is still Active
        /// </summary>
        public bool Disposed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a specific connection is currently in use
        /// </summary>
        public bool InUse { get; set; }

        /// <summary>
        /// Gets or sets the ThreadId for which this Container was initially created
        /// </summary>
        public string ThreadId{ get;set;}

        /// <summary>
        /// Gibt einen <see cref="T:System.String"/> zurück, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.String"/>, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return string.Format("T:{0},U:{1}, LU: {2:dd.MM.yyyy HH:mm:ss}, D: {3}", ThreadId, InUse, LastUsage,
                                 Disposed);
        }
    }
}
