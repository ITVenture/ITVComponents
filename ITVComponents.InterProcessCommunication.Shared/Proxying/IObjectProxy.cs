namespace ITVComponents.InterProcessCommunication.Shared.Proxying
{
    /// <summary>
    /// Marker Interface for ObjectProxy-Types
    /// </summary>
    public interface IObjectProxy
    {
        /// <summary>
        /// Gets the ObjectName of the remote original object
        /// </summary>
        string ObjectName { get; }
    }
}
