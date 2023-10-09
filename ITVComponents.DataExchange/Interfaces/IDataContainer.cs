using ITVComponents.DataAccess;
using ITVComponents.Plugins;

namespace ITVComponents.DataExchange.Interfaces
{
    public interface IDataContainer
    {
        /// <summary>
        /// Gets the root-collection of the collected data
        /// </summary>
        DynamicResult[] RootCollection { get; }

        /// <summary>
        /// Registers a Dataset in this datacontainer
        /// </summary>
        /// <param name="name">the name of the dataset</param>
        /// <param name="data">the data to register for the provided name</param>
        void RegisterData(string name, DynamicResult[] data);

        /// <summary>
        /// Clears this container
        /// </summary>
        void Clear();
    }
}
