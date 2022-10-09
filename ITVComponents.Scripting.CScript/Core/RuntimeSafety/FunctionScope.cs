using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Security;


namespace ITVComponents.Scripting.CScript.Core.RuntimeSafety
{
    public class FunctionScope:IScope
    {
        private readonly ScriptingPolicy policy;
        private Scope innerScope;

        private IScope parentScope;

        private Dictionary<string, object> initialScope;
        private ScriptingPolicy overridePolicy;

        public FunctionScope(Dictionary<string, object> initialScope, ScriptingPolicy policy)
        {
            this.policy = policy;
            this.initialScope = new Dictionary<string, object>();
            foreach (var keyValuePair in initialScope)
            {
                this.initialScope[keyValuePair.Key] = keyValuePair.Value;
            }
            //innerScope = new Scope(initialScope);
        }

        #region Implementation of IScope

        object IScope.this[string memberName, bool rootOnly]
        {
            get { return GetValue(memberName, rootOnly); }
        }

        ScriptingPolicy IScope.ScriptingPolicy => overridePolicy ?? parentScope?.ScriptingPolicy ?? policy;

        public Scope PrepareCall()
        {
            Scope retVal = innerScope;
            innerScope = new Scope(initialScope, ((IScope)this).ScriptingPolicy);
            return retVal;
        }
#if !Community
        public SmartProperty GetSmartProperty(string name, bool rootOnly = false)
        {
            throw new InvalidOperationException("GetSmartProperty is not supported in FunctionLiteral!");
        }
#endif

        /// <summary>
        /// Gets the a base-value of this Scope
        /// </summary>
        /// <param name="name">the name of the initial object</param>
        /// <param name="exists">indicates whether the requested value exists</param>
        /// <returns>the base-value of the provided name</returns>
        public object GetBaseValue(string name, out bool exists)
        {
            if (exists=initialScope.ContainsKey(name))
                return initialScope[name];
            return null;
        }

        /// <summary>
        /// Gets a base-value of this scope
        /// </summary>
        /// <param name="name">the name to set on the initial-dictionary</param>
        /// <param name="value">the value ot set</param>
        public void SetBaseValue(string name, object value)
        {
            initialScope[name] = value;
        }

        public void FinalizeScope(Scope oldScope)
        {
            innerScope = oldScope;
        }

        public bool ContainsKey(string key)
        {
            return ContainsKey(key, false);
        }

        public void Add(string key, object value)
        {
            innerScope.Add(key, value);
        }

        public bool Remove(string key)
        {
            return innerScope.Remove(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return innerScope.TryGetValue(key, out value);
        }

        public ICollection<string> Keys { get { return innerScope.Keys; } }
        public ICollection<object> Values { get { return innerScope.Values; } }

        object IDictionary<string,object>.this[string memberName]
        {
            get { return GetValue(memberName, false); }
            set {
                if (parentScope?.ContainsKey(memberName, false)??false)
                {
                    parentScope[memberName] = value;
                    return;
                }

                innerScope[memberName] = value;
            }
        }

        public IScope ParentScope
        {
            get { return parentScope; }
            set
            {
                //Clear();
                parentScope = value;
            }
        }

        public bool ContainsKey(string key, bool rootOnly)
        {
            return (parentScope?.ContainsKey(key, true)??false) || (!rootOnly && innerScope.ContainsKey(key, false));
        }

        public Dictionary<string, object> CopyInitial()
        {
            return innerScope.CopyInitial();
        }

        public Dictionary<string, object> Snapshot()
        {
            return innerScope.Snapshot();
        }

        public void OpenInnerScope()
        {
            innerScope.OpenInnerScope();
        }

        public void CollapseScope()
        {
            innerScope.CollapseScope();
        }

        public void Clear(IDictionary<string, object> rootVariables)
        {
            innerScope.Clear(initialScope);
            if (rootVariables != null)
            {
                foreach (KeyValuePair<string, object> item in rootVariables)
                {
                    innerScope[item.Key] = item.Value;
                }
            }
        }

        private object GetValue(string memberName, bool rootOnly)
        {
            if (innerScope.ContainsKey(memberName, rootOnly))
            {
                return innerScope[memberName, rootOnly];
            }
            
            
            if (parentScope != null)
            {
                if (parentScope is ObjectLiteral li && li.ContainsKey(memberName, true))
                {
                    return parentScope[memberName, true];
                }
                
                if (parentScope.ContainsKey(memberName, rootOnly))
                {
                    return parentScope[memberName, rootOnly];
                }
            }

            return null;
        }

        #endregion

        #region Implementation of IEnumerable

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return innerScope.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Implementation of ICollection<KeyValuePair<string,object>>

        public void Add(KeyValuePair<string, object> item)
        {
            innerScope.Add(item);
        }

        public void Clear()
        {
            innerScope.Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return innerScope.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            innerScope.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return innerScope.Remove(item);
        }

        public int Count { get { return innerScope.Count; } }
        public bool IsReadOnly { get { return false; } }

        #endregion

        internal void ClearInitial()
        {
            initialScope.Clear();
        }

        void IScope.OverridePolicy(ScriptingPolicy newPolicy)
        {
            overridePolicy = newPolicy;
        }
    }
}
