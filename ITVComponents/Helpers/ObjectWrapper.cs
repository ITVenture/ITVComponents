using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.TypeConversion;

namespace ITVComponents.Helpers
{
    internal class ObjectWrapper:IDictionary<string,object>
    {
        private readonly object toWrap;

        private string[] keys;

        private PropertyInfo[] values;

        public ObjectWrapper(object toWrap, string[] keys, PropertyInfo[] values)
        {
            this.toWrap = toWrap;
            this.keys = keys;
            this.values = values;
            
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (var index = 0; index < keys.Length; index++)
            {
                var key = keys[index];
                var value = values[index];
                if (value.CanRead)
                {
                    yield return new KeyValuePair<string, object>(key, value.GetValue(toWrap));
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool CanRead(string key)
        {
            var id = keys.IndexOf(key,StringComparison.Ordinal);
            return id != -1 && values[id].CanRead;
        }

        public bool CanWrite(string key)
        {
            var id = keys.IndexOf(key, StringComparison.Ordinal);
            return id != -1 && values[id].CanWrite;
        }

        public void Add(KeyValuePair<string, object> item)
        {
            throw new InvalidOperationException("Object-Wrapper can not be modified.");
        }

        public void Clear()
        {
            throw new InvalidOperationException("Object-Wrapper can not be modified.");
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return ContainsKey(item.Key) && Equals(this[item.Key], item.Value);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            var thisArr = this.ToArray();
            Array.Copy(thisArr, 0, array, arrayIndex, thisArr.Length);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            throw new InvalidOperationException("Object-Wrapper can not be modified.");
        }

        public int Count => keys.Length;
        public bool IsReadOnly => false;
        public void Add(string key, object value)
        {
            throw new InvalidOperationException("Object-Wrapper can not be modified.");
        }

        public bool ContainsKey(string key)
        {
            return keys.Contains(key);
        }

        public bool Remove(string key)
        {
            throw new InvalidOperationException("Object-Wrapper can not be modified.");
        }

        public bool TryGetValue(string key, out object value)
        {
            value = null;
            bool retVal = false;
            // ReSharper disable once AssignmentInConditionalExpression
            if (retVal = ContainsKey(key))
            {
                value = this[key];
            }

            return retVal;
        }

        public object this[string key]
        {
            get
            { 
                var idx = Array.IndexOf(keys, key);
                if (idx >= 0)
                {
                    if (values[idx].CanRead)
                    {
                        return values[idx].GetValue(toWrap);
                    }

                    throw new InvalidOperationException(
                        $"The Property '{key}' is write-only in the underlying object.");
                }

                throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
            }
            set
            {
                var idx = Array.IndexOf(keys, key);
                if (idx >= 0)
                {
                    if (values[idx].CanWrite)
                    {
                        values[idx].SetValue(toWrap, TypeConverter.TryConvert(value, values[idx].PropertyType));
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"The Property '{key}' is read-only in the underlying object.");
                    }
                }
                else
                {
                    throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
                }
            }
        }

        public ICollection<string> Keys => keys;
        public ICollection<object> Values => this.Select(kv => kv.Value).ToArray();
    }
}
