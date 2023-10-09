using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.Plugins;

namespace ITVComponents.EFRepo.DataSync
{
    public interface IConfigurationHandler
    {
        /// <summary>
        /// Provides a list of Permissions that a user must have any of, to perform a specific task
        /// </summary>
        /// <param name="reason">the reason why this component is being invoked</param>
        /// <returns>a list of required permissions</returns>
        string[] PermissionsForReason(string reason);

        /// <summary>
        /// Performs the comparison between the uploaded data and the current system-state
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="fileType">the specific part of the System-configuration that is covered by the current upload</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>

        IEnumerable<Change> PerformCompare(string name, string fileType, byte[] content, IIdentity uploadingIdentity);

        /// <summary>
        /// Describes a configuration that was requested from the client
        /// </summary>
        /// <param name="fileType">the system-part to describe</param>
        /// <param name="filterDic">further information about what exactly needs to be described</param>
        /// <param name="name">the name of the generated description. this name is used as download-filename (do not provide an extension)</param>
        /// <returns>the generated configuration-description</returns>
        object DescribeConfig(string fileType, IDictionary<string, int> filterDic, out string name);

        /// <summary>
        /// Applies changes that were generated during a comparison between two systems
        /// </summary>
        /// <param name="changes">the changes to apply on the target system</param>
        /// <param name="messages">a stringbuilder that collects all generated messages</param>
        /// <param name="extendQuery">an action that can provide query extensions if required</param>
        void ApplyChanges(IEnumerable<Change> changes, StringBuilder messages, Action<string, Dictionary<string, object>> extendQuery = null);
    }
}
