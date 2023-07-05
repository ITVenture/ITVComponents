using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
/*using System.Drawing;
using System.Drawing.Imaging;*/
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ITVComponents.DataAccess.Resources;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using SkiaSharp;
using TypeConverter = ITVComponents.TypeConversion.TypeConverter;

namespace ITVComponents.DataAccess
{
    [Serializable]
    public class DynamicResult:DynamicObject, INotifyPropertyChanged, ISerializable, IBasicKeyValueProvider
    {
        /// <summary>
        /// the controller object used to write changes in this item into the database
        /// </summary>
        private IController controller;

        /// <summary>
        /// Holds the data of the current record
        /// </summary>
        private Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Holds the type information for each column in this result
        /// </summary>
        private Dictionary<string, Type> types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// indicates whether this item is saved automatically when a property changes
        /// </summary>
        private bool autoSave = false;

        /// <summary>
        /// indicates whether this item's updates notifications are being deferred
        /// </summary>
        private bool frozen = false;

        /// <summary>
        /// holds a list of all dirty columns. for these columns, a notification is being raised, when frozen changes from true to false
        /// </summary>
        private List<string> dirtyColumns = new List<string>();

        /// <summary>
        /// Initializes a new instance of the DynamicResult class
        /// </summary>
        /// <param name="reader">the reader that is providing the data represented by this DynamicResult item</param>
        public DynamicResult(IDataReader reader)
            :this()
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object postSet = null;
                string name = reader.GetName(i);//.ToUpper();
                Type type = reader.GetFieldType(i);
                try
                {
                    object val = reader.GetValue(i);
                    //LogEnvironment.LogEvent(string.Format("Field {0} registsered with type {1}",name,type.FullName),LogSeverity.Report);
                    if (val is INotifyPropertyChanged || val is INotifyCollectionChanged)
                    {
                        postSet = val;
                        val = null;
                    }

                    values.Add(name, val);
                    types.Add(name, type);
                    if (postSet != null)
                    {
                        SetValue(name, postSet);
                    }
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent(null, $@"Error when trying to bind Column {name} (Type: {type}).
Error: {ex.OutlineException()}", (int) LogSeverity.Error, null);
                    throw new DataException($"Error binding column {name} (Type: {type}).", ex);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the DynamicResult class
        /// </summary>
        /// <param name="source">the Dictionary containing the values that are represented by this DynamicResult</param>
        public DynamicResult(Dictionary<string, object> source)
            :this()
        {
            foreach (KeyValuePair<string, object> item in source)
            {
                object val = item.Value??DBNull.Value;
                object postSet = null;
                string name = item.Key;//.ToUpper();
                if (val is INotifyPropertyChanged || val is INotifyCollectionChanged)
                {
                    postSet = val;
                    val = null;
                }

                values.Add(name, val);
                types.Add(name, typeof(object));
                if (postSet != null)
                {
                    SetValue(name, postSet);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the DynamicResult class
        /// </summary>
        /// <param name="source">the Source for the values</param>
        /// <param name="types">the Source of the types for the values</param>
        public DynamicResult(Dictionary<string, object> source, Dictionary<string, Type> types) : this()
        {
            foreach (
                var tmp in
                    (from t in source
                     join m in types on t.Key equals m.Key
                     select new {t.Key, t.Value, Type = m.Value}))
            {
                object val = tmp.Value ?? DBNull.Value;
                object postSet = null;
                string name = tmp.Key;//.ToUpper();
                if (val is INotifyPropertyChanged || val is INotifyCollectionChanged)
                {
                    postSet = val;
                    val = null;
                }

                values.Add(name, val);
                this.types.Add(name, tmp.Type);
                if (postSet != null)
                {
                    SetValue(name, postSet);
                }
            }
        }

        /// <summary>
        /// Initialiezs a new isntance of the DynamicResult class
        /// </summary>
        /// <param name="info">serialization info</param>
        /// <param name="context">streaming info</param>
        protected DynamicResult(SerializationInfo info, StreamingContext context)
            : this()
        {
            foreach (SerializationEntry ent in info)
            {
                object val = ent.Value ?? DBNull.Value;
                object postSet = null;
                string name = ent.Name;//.ToUpper();
                if (val is INotifyPropertyChanged || val is INotifyCollectionChanged)
                {
                    postSet = val;
                    val = null;
                }

                values.Add(name, val);
                types.Add(name, typeof(object));
                if (postSet != null)
                {
                    SetValue(name, postSet);
                }
            }
        }

        /// <summary>
        /// Prevents a default instance of the DynamicResult class from being created
        /// </summary>
        private DynamicResult()
        {
        }

        /// <summary>
        /// Gets or sets the controller of this item
        /// </summary>
        public IController Controller
        {
            get { return controller; }
            set { controller = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this record should be saved automaticall on Propertychanges
        /// </summary>
        public bool AutoSave
        {
            get { return autoSave; }
            set { autoSave = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this record is currently locked for notifications. Open Notifications will be distributed as soon as the Value is set to false
        /// </summary>
        public bool NotificationsFrozen
        {
            get { return frozen; }
            set
            {
                frozen = value;
                if (!frozen)
                {
                    foreach (string s in dirtyColumns)
                    {
                        OnPropertyChanged(new PropertyChangedEventArgs(s));
                    }

                    dirtyColumns.Clear();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this Item can be Extended
        /// </summary>
        public bool Extendable { get; set; } = false;

            /// <summary>
        /// Gets the value of the provided FieldName
        /// </summary>
        /// <param name="memberName">the membername that belongs to the requested value</param>
        /// <returns>the requested value if it exists</returns>
        public dynamic this[string memberName]
        {
            get
            {
                return this[memberName, typeof(object)];
            }
            set { this[memberName, typeof(object)] = value; }
        }

        /// <summary>
        /// Gets all MemberNames that are associated with this basicKeyValue provider instance
        /// </summary>
        public string[] Keys { get { return GetDynamicMemberNames().ToArray(); } }

        /// <summary>
        /// Gets the value of the provided FieldName
        /// </summary>
        /// <param name="memberName">the membername that belongs to the requested value</param>
        /// <param name="requestedType">the type in which the returned value should be converted</param>
        /// <returns>the value identified with the provided membername</returns>
        public dynamic this[string memberName, Type requestedType]
        {
            get
            {
                dynamic retVal;
                if (!GetValue(memberName, requestedType, out retVal))
                {
                    LogEnvironment.LogDebugEvent(null, string.Format("Unknown Member {0}", memberName), (int) LogSeverity.Warning, "DataAccess");
                }

                return retVal;
            }
            set {
                SetValue(memberName, value);
            }
        }

            /// <summary>
        /// Gets a list of ColumnNames fetched into this Record
        /// </summary>
        /// <returns>a list of ColumnNames fetched into this Result</returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return values.Keys.ToArray();
        }

        /// <summary>
        /// Tries to fetch a value for the requested member name
        /// </summary>
        /// <param name="binder">Contains information about the Member to get from the Results</param>
        /// <param name="result">the Result that was found in the Dictionary</param>
        /// <returns>a value indicating whether the requested value was found</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return GetValue(binder.Name, binder.ReturnType, out result);
        }

        /// <summary>
        /// Assigns a new value to a specific Column in this result
        /// </summary>
        /// <param name="binder">the binder providing information about the Column that is supposed to be updated</param>
        /// <param name="value">the value that must be assigned to the column</param>
        /// <returns>a value indicating whether the value could be assigned properly</returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return SetValue(binder.Name/*.ToUpper()*/, value);
        }

        /// <summary>
        /// Converts this Result into an instance of the TargetType. The Target type must have a public default constructor.
        /// </summary>
        /// <param name="targetType">the type in which to convert the </param>
        /// <returns>the instance of the target type containing the assignable </returns>
        public dynamic ToSpecificInstance(Type targetType)
        {
            dynamic retVal = targetType.GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
            PropertyInfo[] props = targetType.GetProperties();
            foreach (PropertyInfo prop in props)
            {
                if (prop.CanWrite)
                {
                    prop.SetValue(retVal, this[prop.Name, prop.PropertyType], null);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets the Type for a specific column in this record
        /// </summary>
        /// <param name="memberName">the membername for which to get the type</param>
        /// <returns>the corresponding type for a specific column name</returns>
        public Type GetType(string memberName)
        {
            return types[memberName];
        }


        /// <summary>
        /// Gets a value indicating whether a specific key is present in this basicKeyValueProvider instance
        /// </summary>
        /// <param name="key">the Key for which to check</param>
        /// <returns>a value indicating whether the specified key exists in this provider</returns>
        public bool ContainsKey(string key)
        {
            return values.ContainsKey(key);
        }

        /// <summary>
        /// Informs a listening class of a change of a specific property
        /// </summary>
        /// <param name="e">the changed property</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!frozen)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, e);
                }
            }
            else
            {
                if (!dirtyColumns.Contains(e.PropertyName))
                {
                    dirtyColumns.Add(e.PropertyName);
                }
            }
        }

        /// <summary>
        /// Sets a value of a particular column of this dynamic Result object
        /// </summary>
        /// <param name="name">the columnName</param>
        /// <param name="value">the new value of the column</param>
        /// <returns>a value indicating whether the requested column was found</returns>
        private bool SetValue(string name, object value)
        {
            bool retVal = values.ContainsKey(name/*.ToUpper()*/);
            if (retVal || Extendable)
            {
                if (!retVal)
                {
                    object v = value;
                    if (value is DynamicResult || value is SmartProperty)
                    {
                        v= new object();
                    }
                    else if (value is SKImage)
                    {
                        v = new byte[0];
                    }

                    types.Add(name/*.ToUpper()*/, v?.GetType() ?? typeof(object));
                }

                Type t = types[name/*.ToUpper()*/];
                if (value == null)
                {
                    value = DBNull.Value;
                }
                else
                {
                    if (!t.IsAssignableFrom(value.GetType()) && !(value is DynamicResult) && !(value is SmartProperty))
                    {
                        if (value is SKImage img && t == typeof(byte[]))
                        {
                            MemoryStream mst = new MemoryStream();
                            img.Encode(SKEncodedImageFormat.Png,60).SaveTo(mst);
                            mst.Close();
                            value = mst.ToArray();
                        }
                        else
                        {
                            LogEnvironment.LogDebugEvent(null, string.Format("Target-Type for {0} is: {1}", name, t.FullName),
                                                    (int)LogSeverity.Report, "DataAccess");
                            if (value is IConvertible)
                            {
                                try
                                {
                                    value = TypeConverter.Convert(value, t);
                                }
                                catch (Exception ex)
                                {
                                    LogEnvironment.LogEvent(
                                        string.Format("Error while converting new value of {0} to type {1}: {2}", name,
                                                      t.FullName, ex), LogSeverity.Warning, "DataAccess");
                                }
                            }
                        }
                    }
                    else if (value is SmartProperty)
                    {
                        types[name/*.ToUpper()*/] = typeof(object);
                    }
                }

                if (!retVal || !(values[name/*.ToUpper()*/] is SmartProperty && (values[name/*.ToUpper()*/] as SmartProperty).SetterMethod != null))
                {
                    object oldValue = retVal ? values[name/*.ToUpper()*/] : null;
                    SmartProperty smart = oldValue as SmartProperty;
                    if (smart != null)
                    {
                        smart.PropertyChanged -= SmartPropertyChanged;
                    }
                    else
                    {
                        INotifyCollectionChanged notifyCollection = oldValue as INotifyCollectionChanged;
                        if (notifyCollection != null)
                        {
                            notifyCollection.CollectionChanged -= ChildCollectionChanged;
                        }
                    }

                    values[name/*.ToUpper()*/] = value;
                    smart = value as SmartProperty;
                    if (smart != null)
                    {
                        smart.PropertyChanged += SmartPropertyChanged;
                    }
                    else
                    {
                        INotifyCollectionChanged notifyCollection = value as INotifyCollectionChanged;
                        if (notifyCollection != null)
                        {
                            notifyCollection.CollectionChanged += ChildCollectionChanged;
                        }
                    }

                    if (controller != null && autoSave)
                    {
                        controller.Save(this);
                    }
                }
                else
                {
                    (values[name/*.ToUpper()*/] as SmartProperty).Value = value;
                }

                (from z in values where (z.Value is SmartProperty && ((SmartProperty)z.Value).MonitoredProperties==null) || z.Key.Equals(name,StringComparison.OrdinalIgnoreCase)/*.ToUpper()*/ select new PropertyChangedEventArgs(z.Key))
                    .ToList().ForEach(n => OnPropertyChanged(n));
            }

            return retVal;
        }

        /// <summary>
        /// Handles changed events of nested observable collections
        /// </summary>
        /// <param name="sender">the sending collection</param>
        /// <param name="e">the notification information of the nested list</param>
        private void ChildCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            INotifyCollectionChanged notifyCollection = sender as INotifyCollectionChanged;
            if (notifyCollection != null)
            {
                if (values.ContainsValue(notifyCollection))
                {
                    var item = values.First(n => n.Value == notifyCollection);
                    OnPropertyChanged(new PropertyChangedEventArgs(item.Key));
                }
                else
                {
                    notifyCollection.CollectionChanged -= ChildCollectionChanged;
                }
            }
        }

        /// <summary>
        /// Handles changed smartproperties
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void SmartPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SmartProperty smart = sender as SmartProperty;
            if (smart != null)
            {
                if (values.ContainsValue(smart))
                {
                    var item = values.First(n => n.Value == smart);
                    OnPropertyChanged(new PropertyChangedEventArgs(item.Key));
                }
                else
                {
                    smart.PropertyChanged -= SmartPropertyChanged;
                }
            }
        }

        /// <summary>
        /// Creates a result for the demanded Value
        /// </summary>
        /// <param name="name">the name of the requested field</param>
        /// <param name="type">the requested type of the field</param>
        /// <param name="result">output result of the resulting value</param>
        /// <returns>a value indicating whether the requested value was found</returns>
        private bool GetValue(string name, Type type, out object result)
        {
            result = null;
            if (string.IsNullOrEmpty(name)) return false;

            if (!values.ContainsKey(name /*.ToUpper()*/)) return false;

            object v = values[name /*.ToUpper()*/];
            if (v.GetType() != type && !(v is DBNull))
            {
                if (v is byte[] barr && typeof(SKImage).IsAssignableFrom(type))
                {
                    v = SKImage.FromEncodedData(SKData.Create(new MemoryStream(barr)));
                }
                else
                if (v is DynamicResult)
                {
                    v = (v as DynamicResult).GetIndexValue();
                }
                else if (v is SmartProperty)
                {
                    v = (v as SmartProperty).Value;
                }
                else if (v is Delegate)
                {
                    throw new Exception(DataAccessResources
                        .DynamicResult_GetValue_Delegates_are_not_supported_);
                }
                else
                {
                    if (type != typeof(object) &&
                        !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                    {
                        v = TypeConverter.Convert(v, type);
                    }
                    else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        if (v != null)
                        {
                            try
                            {
                                v = TypeConverter.Convert(v, Nullable.GetUnderlyingType(type));
                            }
                            catch
                            {
                                LogEnvironment.LogEvent(
                                    string.Format(
                                        DataAccessResources
                                            .DynamicResult_GetValue_Can_not_convert___0___to__1__, v,
                                        type.FullName), LogSeverity.Error, "DataAccess");
                                v = null;
                            }
                        }
                    }
                }
            }
            else if (v is DBNull)
            {
                v = null;
            }

            result = v;
            return true;
        }

        /// <summary>
        /// Gets the index value for this record
        /// </summary>
        /// <returns>the value of this record's index</returns>
        private object GetIndexValue()
        {
            object retVal = this;
            if (controller != null)
            {
                retVal = controller.GetIndex(this);
            }

            return retVal;
        }

        /// <summary>
        /// Informs client classes of changed properties
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> to populate with data. </param><param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext"/>) for this serialization. </param><exception cref="T:System.Security.SecurityException">The caller does not have the required permission. </exception>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            foreach (KeyValuePair<string, object> value in values)
            {
                if (!(value.Value is SmartProperty))
                {
                    info.AddValue(value.Key, value.Value);
                }
                else
                {
                    info.AddValue(value.Key, ((SmartProperty) value.Value).Value);
                }
            }
        }
    }
}
