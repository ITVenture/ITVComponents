using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;

namespace ITVComponents.DataAccess.Models
{
    /// <summary>
    /// Mapping rule for type conversions from a Reader to an entity
    /// </summary>
    internal class MapRule
    {
        /// <summary>
        /// Informations about how to map the targetMember
        /// </summary>
        private DbColumnAttribute dataColumn;

        /// <summary>
        /// the name of the member that is covered by this mapping rule
        /// </summary>
        private string memberName;

        /// <summary>
        /// Holds mappings for types that differ from the metatype that was used to create the mapping
        /// </summary>
        private Dictionary<Type, MemberInfo> mappings;

        /// <summary>
        /// Initializes a new instance of the MapRule class
        /// </summary>
        /// <param name="targetMember">the targeetMember that is mapped</param>
        /// <param name="attribute">the attribute </param>
        public MapRule(MemberInfo targetMember, DbColumnAttribute attribute)
        {
            memberName = targetMember.Name;
            this.dataColumn = attribute;
            mappings = new Dictionary<Type, MemberInfo>();
            mappings.Add(targetMember.DeclaringType, targetMember);
        }

        /// <summary>
        /// Gets the FieldName that is applied for this MapRule
        /// </summary>
        public string FieldName
        {
            get { return dataColumn.ColumnName??memberName; }
        }

        /// <summary>
        /// Gets a value indicating whether to use a ValueResolving Expression for this MapRule
        /// </summary>
        public bool UseExpression { get { return dataColumn.ValueResolveExpression != null; } }

        /// <summary>
        /// Gets an Expression that is used to resolve the Value of this MapRule. The Expression will provide e variable called "value" that can be used to resolve the requested value
        /// </summary>
        public string ValueResolveExpression { get { return dataColumn.ValueResolveExpression; } }

        /// <summary>
        /// Gets or sets the value of this MapRule for a specific object
        /// </summary>
        /// <param name="targetObject">the object for which to obtain or set set a value</param>
        /// <returns>the value of this MapRule for the specified object</returns>
        public object this[object targetObject] { get { return GetValue(targetObject); } set { SetValue(targetObject, value); } }

        /// <summary>
        /// Gets the value of this MapRule for a specific object
        /// </summary>
        /// <param name="targetObject">the targetObject for which to get this MapRule's value</param>
        /// <returns>the value for this MapRule on the given object</returns>
        private object GetValue(object targetObject)
        {
            object retVal = null;
            MemberInfo targetMember = GetMember(targetObject);
            if (targetMember is FieldInfo)
            {
                retVal = (targetMember as FieldInfo).GetValue(targetObject);
            }
            else if (targetMember is PropertyInfo)
            {
                if ((targetMember as PropertyInfo).CanRead)
                {
                    retVal = (targetMember as PropertyInfo).GetValue(targetObject, null);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Sets the value of this MapRule for the given object
        /// </summary>
        /// <param name="targetObject">the target object on which to set the value for this MapRule</param>
        /// <param name="value">the value to assign on the property bound to this MapRule on the provided object</param>
        private void SetValue(object targetObject, object value)
        {
            MemberInfo targetMember = GetMember(targetObject);
            if (targetMember is FieldInfo)
            {
                FieldInfo info = (targetMember as FieldInfo);
                try
                {
                    if (!info.FieldType.IsAssignableFrom(value.GetType()))
                    {
                        value = ChangeType(value, info.FieldType);
                    }
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.Message, LogSeverity.Error, "DataAccess");
                }

                info.SetValue(targetObject, value);
            }
            else if (targetMember is PropertyInfo)
            {
                PropertyInfo info = (targetMember as PropertyInfo);
                try
                {
                    if (value != DBNull.Value && value != null)
                    {
                        if (!info.PropertyType.IsAssignableFrom(value.GetType()))
                        {
                            value = ChangeType(value, info.PropertyType);
                        }
                    }
                    else
                    {
                        value = null;
                    }
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error, "DataAccess");
                }
                info.SetValue(targetObject, value, null);
            }
        }

        /// <summary>
        /// Converts the given value into the target type. Supports basic types and Enums
        /// </summary>
        /// <param name="value">the value of the DB - Field</param>
        /// <param name="fieldType">the target type in the Model</param>
        /// <returns>a value that is assignable to the provided fieldType</returns>
        private object ChangeType(object value, Type fieldType)
        {
            if (!(value is string && fieldType.IsEnum))
            {
                return TypeConverter.Convert(value, fieldType);
            }

            return Enum.Parse(fieldType, value.ToString());
        }

        /// <summary>
        /// Gets the Specific Member of the current mapped instance
        /// </summary>
        /// <param name="target">the target on which to apply this mapping</param>
        /// <returns>a memberinfo that is capable to read/write data on the current data object</returns>
        private MemberInfo GetMember(object target)
        {
            MemberInfo retVal = null;
            if (target != null)
            {
                Type t = target.GetType();
                lock (mappings)
                {
                    if (!mappings.ContainsKey(t))
                    {
                        retVal = t.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic |
                                                         BindingFlags.Instance |
                                                         BindingFlags.GetField | BindingFlags.SetField |
                                                         BindingFlags.GetProperty |
                                                         BindingFlags.SetProperty |
                                                         BindingFlags.FlattenHierarchy).FirstOrDefault();
                        if (retVal != null)
                        {
                            mappings.Add(t, retVal);
                        }
                        else
                        {
                            LogEnvironment.LogDebugEvent(null, string.Format("no mapping found for {0}", memberName), (int) LogSeverity.Warning, "DataAccess");
                        }
                    }
                    else
                    {
                        retVal = mappings[t];
                    }
                }
            }

            return retVal;
        }
    }
}
