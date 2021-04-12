using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ITVComponents.InterProcessCommunication.Shared.Proxying;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;
using Exception = System.Exception;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    [Serializable]
    public class TypedParam:ISerializable
    {
       private object value;

        public TypedParam(object value)
        {
            this.value = value;
        }

        public TypedParam()
        {
        }

        public TypedParam(SerializationInfo info, StreamingContext context)
        {
            value = info.GetObject(nameof(value));
            NullType = (TypeDescriptor)info.GetValue(nameof(NullType),typeof(TypeDescriptor));
            if (NullType != null)
            {
                Type t = null;
                try
                {
                    t = NullType;
                    value = TypeConverter.Convert(value, t);

                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Error converting Value to {t} ({ex.Message})", LogSeverity.Error);
                }
            }
        }

        public TypeDescriptor NullType { get; set; }

        public virtual object GetValue(Func<string, object> valueCallback)
        {
            object retVal = value;
            var result = value as ProxyResult;
            if (result != null)
            {
                retVal = valueCallback(result.UniqueName);
            }

            return retVal;
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(value), value);
            info.AddValue(nameof(NullType),NullType);
        }
    }

    [Serializable]
    public class ResolvableArray<T> : TypedParam
    {
        List<string> proxies = new List<string>();
        List<T> fixObjects = new List<T>();

        public ResolvableArray()
        {
        }

        public ResolvableArray(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            var tmp = (List<string>)info.GetValue(nameof(proxies), typeof(List<string>));
            if (tmp != null)
            {
                proxies.AddRange(tmp);
            }

            var tmp2 = (List<T>) info.GetValue(nameof(fixObjects), typeof(List<T>));
            if (tmp2 != null)
            {
                fixObjects.AddRange(tmp2);
            }
        }
        public ResolvableArray(T[] value):base(null)
        {
            NullType = typeof(T[]);
            foreach (T obj in value)
            {
                IObjectProxy pobj = obj as IObjectProxy;
                if (pobj != null)
                {
                    proxies.Add(pobj.ObjectName);
                }
                else
                {
                    fixObjects.Add(obj);
                }
            }
        }

        #region Overrides of TypedParam

        public override object GetValue(Func<string, object> valueCallback)
        {
            List<T> retVal = new List<T>();
            foreach (string proxy in proxies)
            {
                retVal.Add((T)valueCallback(proxy));
            }

            retVal.AddRange(fixObjects);
            return retVal.ToArray();
        }

        #endregion

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(proxies), proxies);
            info.AddValue(nameof(fixObjects), fixObjects);
        }
    }
}
