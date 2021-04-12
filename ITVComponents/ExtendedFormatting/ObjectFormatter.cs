using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.ExtendedFormatting
{
    public abstract class ObjectFormatter:IDisposable
    {
        /// <summary>
        /// Holds a list of available Formatters
        /// </summary>
        private static List<ObjectFormatter> initializedFormatters = new List<ObjectFormatter>();

        /// <summary>
        /// Initializes a new instance of the ObjectFormatter class
        /// </summary>
        /// <param name="targetType">the Target Type that can be formatted using this Formatter implementation</param>
        protected ObjectFormatter(Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            TargetType = targetType;
            RegisterFormatter(this);
        }

        /// <summary>
        /// Gets the TargetType for this Formatter
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// Formats an object using the appropriate ObjectFormatter if available. If there is none available, the ToString method of the targetObject is used.
        /// </summary>
        /// <param name="targetObject">the targetObject that must be formatted</param>
        /// <returns>the string-value representing the formatted object</returns>
        public static string FormatObject(object targetObject)
        {
            string retVal = targetObject?.ToString() ?? string.Empty;
            Type targetType = targetObject?.GetType();
            if (targetType != null)
            {
                ObjectFormatter targetFormatter;
                lock (initializedFormatters)
                {
                    targetFormatter =
                        initializedFormatters.FirstOrDefault(n => n.TargetType.IsAssignableFrom(targetType));
                }

                if (targetFormatter != null)
                {
                    retVal = targetFormatter.Format(targetObject);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Formats the given object using this formatter
        /// </summary>
        /// <param name="targetObject">the target object to format</param>
        /// <returns>a formatted string representing the given object</returns>
        public abstract string Format(object targetObject);

        /// <summary>Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.</summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            UnregisterFormatter(this);
        }

        /// <summary>
        /// Adds a formatter to the list of available formatters
        /// </summary>
        /// <param name="formatter">the new initialized formatter</param>
        private static void RegisterFormatter(ObjectFormatter formatter)
        {
            lock (initializedFormatters)
            {
                if (initializedFormatters.Any(n => n.TargetType == formatter.TargetType))
                {
                    throw new InvalidOperationException(
                        $"Can only have one Formatter that formats {formatter.TargetType.FullName}");
                }

                var nextItem =
                    initializedFormatters.Select((o, i) => new {Formatter = o, Index = i})
                        .FirstOrDefault(n => n.Formatter.TargetType.IsAssignableFrom(formatter.TargetType));
                if (nextItem == null)
                {
                    initializedFormatters.Add(formatter);
                }
                else
                {
                    initializedFormatters.Insert(nextItem.Index, formatter);
                }
            }
        }

        /// <summary>
        /// Removes an objectFormatter from the list of supported formatters
        /// </summary>
        /// <param name="formatter">the formatter that is being disposed</param>
        private static void UnregisterFormatter(ObjectFormatter formatter)
        {
            lock (initializedFormatters)
            {
                if (initializedFormatters.Contains(formatter))
                {
                    initializedFormatters.Remove(formatter);
                }
            }
        }
    }
}
