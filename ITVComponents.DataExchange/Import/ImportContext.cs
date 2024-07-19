using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.DataAccess;
using ITVComponents.Threading;

namespace ITVComponents.DataExchange.Import
{
    public class ImportContext
    {
        /// <summary>
        /// Holds all local import contexts
        /// </summary>
        private static ThreadLocal<ImportContext> current = new ThreadLocal<ImportContext>();

        /// <summary>
        /// Describes the current Data of the current import session
        /// </summary>
        private Dictionary<string, DynamicResult> importingData;

        /// <summary>
        /// Gets the Current Import-Context
        /// </summary>
        public static ImportContext Current
        {
            get
            {
                ImportContext retVal = null;
                if (current.IsValueCreated)
                {
                    retVal = current.Value;
                }

                return retVal;
            }
        }

        /// <summary>
        /// Starts a new Import-Process and provides a context for runtime values
        /// </summary>
        /// <returns>a lock that enables a caller to drop the created context</returns>
        public static IResourceLock BeginImport()
        {
            ImportContext retVal = new ImportContext();
            current.Value = retVal;
            return new ImportContextLock(retVal);
        }

        /// <summary>
        /// Finishes the current import
        /// </summary>
        private static void CloseImport()
        {
            current.Value = null;
        }

        /// <summary>
        /// Prevents a default instance of the ImportContext class from being created
        /// </summary>
        private ImportContext()
        {
            importingData = new Dictionary<string, DynamicResult>();
        }

        /// <summary>
        /// Gets the current data of a specific resultset-name
        /// </summary>
        /// <param name="name">the name for which to get the current value</param>
        /// <returns>a dynamicresult object containing the importer-data for a specific resultset</returns>
        public DynamicResult this[string name]
        {
            get
            {
                DynamicResult retVal = null;
                if (importingData.ContainsKey(name))
                {
                    retVal = importingData[name];
                }

                return retVal;
            }
        }

        /// <summary>
        /// Sets the current importdata
        /// </summary>
        /// <param name="name">the name of the dataset that</param>
        /// <param name="data">the last result of a parser</param>
        internal void SetCurrent(string name, DynamicResult data)
        {
            importingData[name] = data;
        }

        /// <summary>
        /// Cleans up this import-context
        /// </summary>
        private void Clear()
        {
            importingData.Clear();
            importingData = null;
        }

        private class ImportContextLock : IResourceLock
        {
            private ImportContext innerContext;

            public ImportContextLock(ImportContext innerContext)
            {
                this.innerContext = innerContext;
            }

            #region Implementation of IDisposable

            public void Dispose()
            {
                innerContext.Clear();
                CloseImport();
            }

            #endregion

            #region Implementation of IResourceLock

            public IResourceLock InnerLock { get { return null; } }
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

            #endregion
        }
    }
}
