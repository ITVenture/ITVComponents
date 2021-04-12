using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ITVComponents.EFRepo.Alerting
{
    public static class AlertingHelper
    {
        //private Action<>

        /// <summary>
        /// Sends an alert for the provided entity
        /// </summary>
        /// <param name="entry">the entity that has changed</param>
        /// <param name="context">the db-context that is saving the changes</param>
        public static EntityChangedEventArgs ToEntityChangedEventArgs(this EntityEntry entry, DbContext context)
        {
            if (entry.State != EntityState.Unchanged && entry.State != EntityState.Detached)
            {
                string actionSuffix = entry.State.ToString();
                string schema;
                string table = context.GetTableName(entry.Entity.GetType(), out schema);
                List<string> names = (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    ? entry.CurrentValues.Properties.Select(n => n.Name).ToList()
                    : new List<string>();
                var originalValues = new Dictionary<string, object>();
                if (entry.State == EntityState.Modified)
                {
                    var equals = names.Where(n => (entry.OriginalValues[n]?.Equals(entry.CurrentValues[n]) ?? false))
                        .ToArray();
                    equals.ForEach(n => names.Remove(n));
                    names.ForEach(n => originalValues.Add(n, entry.OriginalValues?[n]));
                }

                return new EntityChangedEventArgs
                {
                    Action = actionSuffix,
                    Schema = schema,
                    TableName = table,
                    ChangedColumns = names,
                    Entity = entry.Entity,
                    OriginalValues = originalValues
                };
            }

            return null;
        }

        /// <summary>
        /// Sends a manual alert using the default alerting-path
        /// </summary>
        /// <param name="e">the information about the changed entity</param>
        public static void Alert(EntityChangedEventArgs e)
        {
            try
            {
                OnEntityChanged(e);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Error when trying to alert an entity-change: {ex.OutlineException()}",LogSeverity.Error);
            }
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        /// <param name="e">the arguments describing the change</param>
        private static void OnEntityChanged(EntityChangedEventArgs e)
        {
            EntityChanged?.Invoke(e);
        }

        /// <summary>
        /// Notifies consumer objects when an entity has changed
        /// </summary>
        public static event EntityChangedEventHandler EntityChanged;
    }

    /// <summary>
    /// Event-Handler delegate for the EntityChanged event
    /// </summary>
    /// <param name="e">the arguments describing the changed entity</param>
    public delegate void EntityChangedEventHandler(EntityChangedEventArgs e);

    /// <summary>
    /// EntityChanged event args
    /// </summary>
    public class EntityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Enables a sender-object to provide additional information about the environment that has genrated the event
        /// </summary>
        public AlertingContext AlertingContext { get; } = new AlertingContext();

        /// <summary>
        /// The action that was performed
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// the table-schema
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// the table-name
        /// </summary>
        public string TableName { get; set;}

        /// <summary>
        /// the list of changed columns
        /// </summary>
        public List<string> ChangedColumns { get; set; }

        /// <summary>
        /// The entity that was changed
        /// </summary>
        public object Entity { get; set; }

        /// <summary>
        /// Gets the Original values that were stored in the entity before it was changed
        /// </summary>
        public object OriginalValues { get; set; }
    }
}
