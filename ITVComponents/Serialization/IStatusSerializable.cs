using System;

namespace ITVComponents.Serialization
{
    /// <summary>
    /// Represents an interface that supports dumping the runtime status after its disposal
    /// </summary>
    [Obsolete("Not supported anymore!", true)]
    public interface IStatusSerializable : IDisposable
    {
        /// <summary>
        /// Gets the Runtime information required to restore the status when the application restarts
        /// </summary>
        /// <returns>an object serializer containing all required data for object re-construction on application reboot</returns>
        RuntimeInformation GetPostDisposalSerializableStaus();

        /// <summary>
        /// Applies Runtime information that was loaded from a file
        /// </summary>
        /// <param name="runtimeInformation">the runtime information describing the status of this object before the last shutdown</param>
        void LoadRuntimeStatus(RuntimeInformation runtimeInformation);

        /// <summary>
        /// Allows this object to do required initializations when no runtime status is provided by the calling object
        /// </summary>
        void InitializeWithoutRuntimeInformation();

        /// <summary>
        /// Is called when the runtime is completly available and ready to run
        /// </summary>
        void RuntimeReady();
    }
}
