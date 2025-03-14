using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ITVComponents.InterProcessCommunication.Shared.Proxying;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using ITVComponents.Helpers;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;
using Exception = System.Exception;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    public class TypedParam:IManualSerializer
    {
       private object value;

        public TypedParam(object value)
        {
            this.value = value;
        }

        public TypedParam()
        {
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

        public IList<ManualSerializationData> Data { get; set; }
        public virtual void GetObjectData()
        {
            Data.Add(ManualSerializationData.FromValue(nameof(value), value));
            Data.Add(ManualSerializationData.FromValue(nameof(NullType), NullType));
        }

        public virtual void ApplyObjectData()
        {
            NullType = Data.GetDeserializedValue<TypeDescriptor>(nameof(NullType));
            try
            {
                if (NullType != null)
                {
                    var meth = LambdaHelper
                        .GetMethodInfo(() =>
                            SerializationDataExtensions.GetDeserializedValue<object>(Data, nameof(value)))
                        .GetGenericMethodDefinition().MakeGenericMethod(NullType);
                    value = meth.Invoke(null, new object[] { Data, nameof(value) });
                }
                else
                {
                    value = Data.GetDeserializedValue<object>(nameof(value));
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Error converting Value to {NullType} ({ex.Message})", LogSeverity.Error);
            }
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

        public override void GetObjectData()
        {
            base.GetObjectData();
            Data.Add(ManualSerializationData.FromValue(nameof(proxies), proxies));
            Data.Add(ManualSerializationData.FromValue(nameof(fixObjects), fixObjects));
        }

        public override void ApplyObjectData()
        {
            base.ApplyObjectData();
            var tmp =Data.GetDeserializedValue<List<string>>(nameof(proxies));
            if (tmp != null)
            {
                proxies.AddRange(tmp);
            }

            var tmp2 = Data.GetDeserializedValue<List<T>>(nameof(fixObjects));
            if (tmp2 != null)
            {
                fixObjects.AddRange(tmp2);
            }
        }
    }
}
