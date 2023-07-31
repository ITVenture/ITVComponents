using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Threading;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DataSync
{
    public abstract class ConfigurationHandlerBase: IConfigurationHandler, IDeferredInit
    {
        /// <summary>
        /// Holds all current changes in a list
        /// </summary>
        private List<Change> changes = new List<Change>();

        private List<Change> deletes = new List<Change>();

        protected ConfigurationHandlerBase()
        {
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => true;

        /// <summary>
        /// Provides a list of Permissions that a user must have any of, to perform a specific task
        /// </summary>
        /// <param name="reason">the reason why this component is being invoked</param>
        /// <returns>a list of required permissions</returns>
        public abstract string[] PermissionsForReason(string reason);

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            RunInit();
            Initialized = true;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            Dispose(true);
            OnDisposed();
        }

        /// <summary>
        /// Performs the comparison between the uploaded data and the current system-state
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="fileType">the specific part of the System-configuration that is covered by the current upload</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        public IEnumerable<Change> PerformCompare(string name, string fileType, byte[] content, IIdentity uploadingIdentity)
        {
            try
            {
                PerformCompareInternal(name, fileType, content, uploadingIdentity);
                var retVal = changes.Concat(deletes.OrderBy(n => n.DeletePriority)).ToArray();
                return retVal;
            }
            finally
            {
                changes.Clear();
                deletes.Clear();
            }
        }

        /// <summary>
        /// Describes a configuration that was requested from the client
        /// </summary>
        /// <param name="fileType">the system-part to describe</param>
        /// <param name="filterDic">further information about what exactly needs to be described</param>
        /// <param name="name">the name of the generated description. this name is used as download-filename (do not provide an extension)</param>
        /// <returns>the generated configuration-description</returns>
        public abstract object DescribeConfig(string fileType, IDictionary<string, int> filterDic, out string name);

        /// <summary>
        /// Applies changes that were generated during a comparison between two systems
        /// </summary>
        /// <param name="changes">the changes to apply on the target system</param>
        /// <param name="messages">a stringbuilder that collects all generated messages</param>
        /// <param name="extendQuery">an action that can provide query extensions if required</param>
        public abstract void ApplyChanges(IEnumerable<Change> changes, StringBuilder messages, Action<string, Dictionary<string, object>> extendQuery = null);

        /// <summary>
        /// Creates a new ChangeDetail with the given settings
        /// </summary>
        /// <param name="columnName">the Name of the target property</param>
        /// <param name="value">the value of the target property</param>
        /// <param name="valueExpression">the expression to execute in order to apply the value on the target record</param>
        /// <param name="currentValue">the current value of the record</param>
        /// <param name="multiline">indicates whether this is a multiline-detail</param>
        /// <param name="apply">indicates whether to apply this change</param>
        /// <returns>a detail record that can be added to a change</returns>
        protected ChangeDetail MakeDetail(string columnName, string value, string valueExpression = null, string currentValue = null, bool multiline = false, bool apply = true)
        {
            return new ChangeDetail
            {
                Name = columnName,
                NewValue = value,
                Apply = apply,
                TargetProp = columnName,
                ValueExpression = valueExpression,
                CurrentValue = currentValue,
                MultilineContent = multiline
            };
        }

        /// <summary>
        /// Creates a Script-Linq query that will resolve a foreign-key value for a dependent entity
        /// </summary>
        /// <param name="targetProperty">the target-property to assign the value to</param>
        /// <param name="sourceEntity">the source-entity that contains the pk-value</param>
        /// <param name="filterProperty">the property that is used to filter the entity</param>
        /// <param name="additionalWhere">an additional where clause that can be used when the uniqueness of a query requires further filtering. Reference the source-entity with 'n'</param>
        /// <param name="ignoreFail">indicates whether to return null when the pk-value was not found</param>
        /// <param name="managedFilterType">the managed filter-type</param>
        /// <param name="scriptedFilterType">the scripted filter-type</param>
        /// <returns></returns>
        protected string MakeLinqAssign<TContext>(string targetProperty, string sourceEntity, string filterProperty, string additionalWhere = null, bool ignoreFail = false, string managedFilterType = null, string scriptedFilterType = null)
        where TContext:DbContext
        {
            if (!string.IsNullOrEmpty(additionalWhere))
            {
                additionalWhere = additionalWhere.Replace(@"""", @"""""");
            }

            return
                @$"Entity.{targetProperty} = (!'System.String'.IsNullOrEmpty(NewValueRaw))?`E(Db as Db->SysQry{UniqueName})::@""{managedFilterType ?? "string"} filterVal = Global.filterValue; {typeof(TContext).Name} db = Global.Db; return db.{sourceEntity}.Local.ToArray().FirstOrDefault(n => n.{filterProperty} == filterVal{(additionalWhere == null ? "" : $" && {additionalWhere}")})??db.{sourceEntity}.First{(ignoreFail ? "OrDefault" : "")}(n => n.{filterProperty} == filterVal{(additionalWhere == null ? "" : $" && {additionalWhere}")});"" with {{filterValue:{(scriptedFilterType == null ? "NewValueRaw" : $"ChangeType(NewValueRaw,{scriptedFilterType})")}}}:null";
        }

        /// <summary>
        /// Registers a change that describes a difference between the source and the target system
        /// </summary>
        /// <param name="change">an object describing the differences of a specific record between two systems</param>
        protected void RegisterChange(Change change)
        {
            if (change.ChangeType != ChangeType.Delete || change.DeletePriority == -1)
            {
                changes.Add(change);
            }
            else
            {
                deletes.Add(change);
            }
        }


        /// <summary>
        /// Creates a Script-Linq query that will resolve a foreign-key value for a dependent entity
        /// </summary>
        /// <param name="targetProperty">the target-property to assign the value to</param>
        /// <param name="sourceEntity">the source-entity that contains the pk-value</param>
        /// <param name="filterProperty">the property that is used to filter the entity</param>
        /// <param name="additionalWhere">an additional where clause that can be used when the uniqueness of a query requires further filtering. Reference the source-entity with 'n'</param>
        /// <param name="ignoreFail">indicates whether to return null when the pk-value was not found</param>
        /// <param name="managedFilterType">the managed filter-type</param>
        /// <param name="scriptedFilterType">the scripted filter-type</param>
        /// <returns></returns>
        protected string MakeLinqQuery<TContext>(string sourceEntity, string filterProperty, string additionalWhere = null, bool ignoreFail = false, string managedFilterType = null, string scriptedFilterType = null, string filterValueVariable = "Value")
            where TContext:DbContext
        {
            if (!string.IsNullOrEmpty(additionalWhere))
            {
                additionalWhere = additionalWhere.Replace(@"""", @"""""");
            }

            return @$"`E(Db as Db->SysQry{UniqueName})::@""{managedFilterType ?? "string"} filterVal = Global.filterValue; {typeof(TContext).Name} db = Global.Db; return db.{sourceEntity}.Local.ToArray().FirstOrDefault(n => n.{filterProperty} == filterVal{(additionalWhere == null ? "" : $" && {additionalWhere}")})??db.{sourceEntity}.First{(ignoreFail ? "OrDefault" : "")}(n => n.{filterProperty} == filterVal{(additionalWhere == null ? "" : $" && {additionalWhere}")});"" with {{filterValue:{(scriptedFilterType == null ? filterValueVariable : $"ChangeType({filterValueVariable},{scriptedFilterType})")}}}";
        }


        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Enables derived classes to perform custom initialization tasks
        /// </summary>
        protected virtual void RunInit()
        {
            NativeScriptHelper.SetAutoReferences($"SysQry{UniqueName}", true);
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }


        /// <summary>
        /// Performs the comparison between the uploaded data and the current system-state
        /// </summary>
        /// <param name="name">the file-name</param>
        /// <param name="content">the file-content</param>
        /// <param name="fileType">the specific part of the System-configuration that is covered by the current upload</param>
        /// <param name="ms">a model-state dictionary that is used to reflect eventual invalidities back the the file-endpointhandler</param>
        /// <param name="uploadingIdentity">the identity that is uploading the given file</param>
        protected virtual void PerformCompareInternal(string name, string fileType, byte[] content, IIdentity uploadingIdentity)
        {
            PerformCompareInternal(fileType, content);
        }
        /// <summary>
        /// Performs the comparison between the uploaded data and the current system-state
        /// </summary>
        /// <param name="content">the file-content</param>
        /// <param name="fileType">the specific part of the System-configuration that is covered by the current upload</param>
        protected virtual void PerformCompareInternal(string fileType, byte[] content)
        {
            throw new NotImplementedException("One PerformCompare overload needs to be implemented");
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
