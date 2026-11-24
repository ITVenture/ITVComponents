using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ITVComponents.InterProcessCommunication.Shared.Proxying;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    /// <summary>
    /// An argument together with its declared type (<see cref="NullType"/>), e.g. so a null value still selects the right
    /// overload. A plain transport object: the value is an open place and carries its runtime type itself
    /// (<see cref="ITVComponents.Json.SerializationTypingMode.EnvelopePolymorphism"/>).
    /// </summary>
    public class TypedParam
    {
        [JsonInclude]
        [JsonPropertyName("value")]
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
    }

    [Serializable]
    public class ResolvableArray<T> : TypedParam
    {
        [JsonInclude]
        private List<string> proxies = new List<string>();

        [JsonInclude]
        private List<T> fixObjects = new List<T>();

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
    }
}
