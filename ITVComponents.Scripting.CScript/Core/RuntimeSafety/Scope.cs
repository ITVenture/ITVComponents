﻿//#define UseList
//#define UseDummies
//#define UseVarList

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
#if !Community
using ITVComponents.ExtendedFormatting;
#endif
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;
using ValueType = ITVComponents.Scripting.CScript.ScriptValues.ValueType;

//using ITVComponents.DataAccess.Extensions;

namespace ITVComponents.Scripting.CScript.Core.RuntimeSafety
{
    public class Scope : IScope
    {
        private readonly ScriptingPolicy policy;

        private ScriptingPolicy overridePolicy;
        /// <summary>
        /// holds all scopes that have been initialized
        /// </summary>
        /*#if UseVarList
                private List<Dictionary<string, object>>  scopes;

                /// <summary>
                /// gets current maximum Scope Identity
                /// </summary>
                private int currentMaxScopeId = -1;
        #else
                private Stack<Dictionary<string, object>> scopes;
        #endif*/

        /*#if UseDummies
               /// <summary>
                /// indicates whether the next scope-closer will only be a dummy...
                /// </summary>
                private bool nextDummy = false;
        #if UseList

                /// <summary>
                /// gets the current maximum dummy-indicator id
                /// </summary>
                private int currentMaxDummyId = -1;

                /// <summary>
                /// the dummy-stack that indicates for each current scope where the primary inner scope should be considered dummy
                /// </summary>

                private List<bool> dummyStack;
        #else
                private bool nextDummy = false;
        #endif
        #endif*/

        /// <summary>
        /// the current catcher-count.
        /// </summary>
        //private int catchCount = 0;

        /*/// <summary>
        /// the root object of this scope
        /// </summary>
        private Dictionary<string, object> root;*/

        private Dictionary<string, ScopeVar> variables;

        private int layer = 0;

        private List<int> layerRevisions;// = new List<int>();

        private int currentRevision = 0;

        public string ImplicitContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the Scope class
        /// </summary>
        public Scope(ScriptingPolicy policy = null)
            : this(false, policy)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Scope class
        /// </summary>
        public Scope(bool ignoreCase, ScriptingPolicy policy = null)
        {
            this.policy = policy??ScriptingPolicy.Default;
            layerRevisions = new List<int>();
            /*#if UseVarList
            scopes = new List<Dictionary<string, object>>();
#else
            scopes = new Stack<Dictionary<string, object>>();
#endif
#if UseDummies
#if UseList
            dummyStack = new List<bool>();
#else
            //dummyStack = new Stack<bool>();
#endif
#endif*/
            layerRevisions.Add(0);
            variables =
                new Dictionary<string, ScopeVar>(ignoreCase
                                                     ? StringComparer.OrdinalIgnoreCase
                                                     : StringComparer.CurrentCulture);
            //root = Push();
        }

        /// <summary>
        /// Initializes a new instance of the Scope class
        /// </summary>
        /// <param name="initialScope">the initial variables representing this scope</param>
        public Scope(IDictionary<string, object> initialScope, ScriptingPolicy policy = null)
            : this(false, policy)
        {
            foreach (KeyValuePair<string, object> var in initialScope)
            {
                variables.Add(var.Key, new ScopeVar() {Layer = layer, Revision = currentRevision, Value = var.Value});
            }
        }

        /// <summary>
        /// Ruft das Element mit dem angegebenen Schlüssel ab oder legt dieses fest.
        /// </summary>
        /// <returns>
        /// Das Element mit dem angegebenen Schlüssel.
        /// </returns>
        /// <param name="key">Der Schlüssel des abzurufenden oder zu festzulegenden Elements.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> ist null.</exception><exception cref="T:System.Collections.Generic.KeyNotFoundException">Die Eigenschaft wird abgerufen, und <paramref name="key"/> wird nicht gefunden.</exception><exception cref="T:System.NotSupportedException">Die Eigenschaft wird festgelegt, und <see cref="T:System.Collections.Generic.IDictionary`2"/> ist schreibgeschützt.</exception>
        public object this[string key]
        {
            get
            {
                /*object retVal = null;
                if (ContainsKey(key))
                {
                    retVal = GetDictionaryForKey(key)[key];
                }

                return retVal;*/
                return GetValue(key);
            }
            /*#if UseVarList
            set { (GetDictionaryForKey(key) ?? scopes[currentMaxScopeId])[key] = value; }
#else
            set { (GetDictionaryForKey(key) ?? scopes.Peek())[key] = value; }
#endif*/
            set { SetValue(key, value); }
        }

        public object this[string key, bool rootOnly] { get { return GetValue(key, rootOnly); } }

        /// <summary>
        /// Ruft eine <see cref="T:System.Collections.Generic.ICollection`1"/>-Schnittstelle ab, die die Schlüssel von <see cref="T:System.Collections.Generic.IDictionary`2"/> enthält.
        /// </summary>
        /// <returns>
        /// Eine <see cref="T:System.Collections.Generic.ICollection`1"/>, die die Schlüssel des Objekts enthält, das <see cref="T:System.Collections.Generic.IDictionary`2"/> implementiert.
        /// </returns>
        public ICollection<string> Keys
        {
            get
            {
                /*#if UseVarList
                string[] retVal = new string[Count];
                int id = 0;
                Dictionary<string, object> tmp;
                for (int i = currentMaxScopeId; i >= 0; i--)
                {
                    (tmp = scopes[i]).Keys.CopyTo(retVal, id);
                    id += tmp.Count;
                }

                return retVal;
#else
                return scopes.SelectMany(n => n.Keys).ToArray();
#endif*/
                return (from t in variables where t.Value.Layer <= layer && t.Value.Revision == layerRevisions[t.Value.Layer] select t.Key).ToArray();
            }
        }

        /// <summary>
        /// Ruft eine <see cref="T:System.Collections.Generic.ICollection`1"/> ab, die die Werte in <see cref="T:System.Collections.Generic.IDictionary`2"/> enthält.
        /// </summary>
        /// <returns>
        /// Eine <see cref="T:System.Collections.Generic.ICollection`1"/>, die die Werte des Objekts enthält, das <see cref="T:System.Collections.Generic.IDictionary`2"/> implementiert.
        /// </returns>
        public ICollection<object> Values
        {

            get
            {
                /*#if UseVarList
                object[] retVal = new object[Count];
                int id = 0;
                Dictionary<string, object> tmp;
                for (int i = currentMaxScopeId; i>= 0; i--)
                {
                    (tmp=scopes[i]).Values.CopyTo(retVal, id);
                    id += tmp.Count;
                }

                return retVal;
#else
                return scopes.SelectMany(n => n.Values).ToArray();
#endif*/
                return (from t in variables.Values where t.Layer <= layer && t.Revision == layerRevisions[t.Layer] select t.Value).ToArray();
            }
        }

        /// <summary>
        /// Gibt einen Enumerator zurück, der die Auflistung durchläuft.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.Collections.Generic.IEnumerator`1"/>, der zum Durchlaufen der Auflistung verwendet werden kann.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            /*#if UseVarList
             for (int i = currentMaxScopeId; i >= 0; i-- )
             {
                 foreach (KeyValuePair<string, object> pair in scopes[currentMaxScopeId])
                 {
                     yield return pair;
                 }
             }
 #else
             return scopes.SelectMany(n => n.Select(t=> t)).GetEnumerator();
 #endif*/
            return
                (from t in variables
                 where t.Value.Layer <= layer && t.Value.Revision == layerRevisions[t.Value.Layer]
                 select new KeyValuePair<string, object>(t.Key, t.Value.Value)).GetEnumerator();
        }

        /// <summary>
        /// Gibt einen Enumerator zurück, der eine Auflistung durchläuft.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.Collections.IEnumerator"/>-Objekt, das zum Durchlaufen der Auflistung verwendet werden kann.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        ScriptingPolicy IScope.ScriptingPolicy => overridePolicy ?? policy;

        /// <summary>
        /// Fügt der <see cref="T:System.Collections.Generic.ICollection`1"/> ein Element hinzu.
        /// </summary>
        /// <param name="item">Das Objekt, das <see cref="T:System.Collections.Generic.ICollection`1"/> hinzugefügt werden soll.</param><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> ist schreibgeschützt.</exception>
        public void Add(KeyValuePair<string, object> item)
        {
            if (ContainsKey(item.Key))
            {
                throw new ArgumentException("Tried to insert a Duplicate Key");
            }
            /*#if UseVarList
                        ((IDictionary<string,object>)scopes[currentMaxScopeId]).Add(item);
            #else
                        ((IDictionary<string,object>)scopes.Peek()).Add(item);
            #endif*/
            SetValue(item.Key, item.Value);
        }

        /// <summary>
        /// Clears all elements and initializes this scope with new root values
        /// </summary>
        /// <param name="rootVariables">the root variables to put on the scope</param>
        public void Clear(IDictionary<string, object> rootVariables)
        {
            Clear();
            //root = Push();
            if (rootVariables != null)
            {
                foreach (KeyValuePair<string, object> var in rootVariables)
                {
                    SetValue(var.Key, var.Value);
                }
            }
        }

        /// <summary>
        /// Entfernt alle Elemente aus <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> ist schreibgeschützt. </exception>
        public void Clear()
        {
            /*#if UseVarList
            currentMaxScopeId = -1;
#else
            scopes.Clear();
#endif
#if UseDummies
            //dummyStack.Clear();
            nextDummy = false;
#endif
            //scopes.Clear();*/
            ImplicitContext = null;
            variables.Clear();
            layer = 0;
        }

        /// <summary>
        /// Bestimmt, ob <see cref="T:System.Collections.Generic.ICollection`1"/> einen bestimmten Wert enthält.
        /// </summary>
        /// <returns>
        /// true, wenn sich <paramref name="item"/> in <see cref="T:System.Collections.Generic.ICollection`1"/> befindet, andernfalls false.
        /// </returns>
        /// <param name="item">Das im <see cref="T:System.Collections.Generic.ICollection`1"/> zu suchende Objekt.</param>
        public bool Contains(KeyValuePair<string, object> item)
        {
            /*Dictionary<string, object> dict = GetDictionaryForKey(item.Key);
            return dict != null && ((IDictionary<string, object>) dict).Contains(item);*/
            //return variables.Any(n => n.Key == item.Key && n.Value.Value.Equals(item.Value) && n.Value.Layer <= layer && n.Value.Layer > -1);
            ScopeVar tmp;
            return
                variables.ContainsKey(item.Key) && (tmp = variables[item.Key]).Layer <= layer && tmp.Revision == layerRevisions[tmp.Layer] && tmp.Value == item.Value;
        }

        /// <summary>
        /// Kopiert die Elemente von <see cref="T:System.Collections.Generic.ICollection`1"/> in ein <see cref="T:System.Array"/>, beginnend bei einem bestimmten <see cref="T:System.Array"/>-Index.
        /// </summary>
        /// <param name="array">Das eindimensionale <see cref="T:System.Array"/>, das das Ziel der aus <see cref="T:System.Collections.Generic.ICollection`1"/> kopierten Elemente ist.Für <see cref="T:System.Array"/> muss eine nullbasierte Indizierung verwendet werden.</param><param name="arrayIndex">Der nullbasierte Index in <paramref name="array"/>, an dem das Kopieren beginnt.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> hat den Wert null.</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> ist kleiner als 0.</exception><exception cref="T:System.ArgumentException"><paramref name="array"/> ist mehrdimensional.- oder -Die Anzahl der Elemente in der Quelle <see cref="T:System.Collections.Generic.ICollection`1"/> ist größer als der verfügbare Speicherplatz ab <paramref name="arrayIndex"/> bis zum Ende des <paramref name="array"/>, das als Ziel festgelegt wurde.- oder -Typ <paramref name="T"/> kann nicht automatisch in den Typ des Ziel-<paramref name="array"/> umgewandelt werden.</exception>
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
#if UseVarList
            int index = arrayIndex;
            Dictionary<string, object> tmp;
            for (int i = currentMaxScopeId; i >= 0; i--)
            {
                ((IDictionary<string, object>)(tmp=scopes[i])).CopyTo(array, index);
                index += tmp.Count;
            }
#else
            /*int id = arrayIndex;
            foreach (var n in scopes)
            {
                ((IDictionary<string, object>) n).CopyTo(array, id);
                id += n.Count;
            }*/
            int id = arrayIndex;
            foreach (var t in this)
            {
                array[id] = t;
                id++;
            }
#endif
        }

        /// <summary>
        /// Entfernt das erste Vorkommen eines bestimmten Objekts aus <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true, wenn <paramref name="item"/> erfolgreich aus <see cref="T:System.Collections.Generic.ICollection`1"/> gelöscht wurde, andernfalls false.Diese Methode gibt auch dann false zurück, wenn <paramref name="item"/> nicht in der ursprünglichen <see cref="T:System.Collections.Generic.ICollection`1"/> gefunden wurde.
        /// </returns>
        /// <param name="item">Das aus dem <see cref="T:System.Collections.Generic.ICollection`1"/> zu entfernende Objekt.</param><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> ist schreibgeschützt.</exception>
        public bool Remove(KeyValuePair<string, object> item)
        {
            /*Dictionary<string, object> dict = GetDictionaryForKey(item.Key);
            return dict != null && ((IDictionary<string, object>) dict).Remove(item);*/
            return true;
        }

        /// <summary>
        /// Ruft die Anzahl der Elemente ab, die in <see cref="T:System.Collections.Generic.ICollection`1"/> enthalten sind.
        /// </summary>
        /// <returns>
        /// Die Anzahl der Elemente, die in <see cref="T:System.Collections.Generic.ICollection`1"/> enthalten sind.
        /// </returns>
        public int Count { get { return /*(from t in scopes select t.Count).Sum();*/ variables.Count(n => n.Value.Layer <= layer && n.Value.Revision ==layerRevisions[n.Value.Layer]); } }

        /// <summary>
        /// Ruft einen Wert ab, der angibt, ob <see cref="T:System.Collections.Generic.ICollection`1"/> schreibgeschützt ist.
        /// </summary>
        /// <returns>
        /// true, wenn <see cref="T:System.Collections.Generic.ICollection`1"/> schreibgeschützt ist, andernfalls false.
        /// </returns>
        public bool IsReadOnly { get { return false; } }

        /// <summary>Ermittelt, ob <see cref="T:System.Collections.Generic.IDictionary`2" /> ein Element mit dem angegebenen Schlüssel enthält.</summary>
        /// <returns>true, wenn das <see cref="T:System.Collections.Generic.IDictionary`2" /> ein Element mit dem Schlüssel enthält, andernfalls false.</returns>
        /// <param name="key">Der im <see cref="T:System.Collections.Generic.IDictionary`2" /> zu suchende Schlüssel.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> ist null.</exception>
        public bool ContainsKey(string key)
        {
            return ContainsKeyInternal(key, false, string.IsNullOrEmpty(ImplicitContext));
        }

        /// <summary>
        /// Ermittelt, ob <see cref="T:System.Collections.Generic.IDictionary`2"/> ein Element mit dem angegebenen Schlüssel enthält.
        /// </summary>
        /// <returns>
        /// true, wenn das <see cref="T:System.Collections.Generic.IDictionary`2"/> ein Element mit dem Schlüssel enthält, andernfalls false.
        /// </returns>
        /// <param name="key">Der im <see cref="T:System.Collections.Generic.IDictionary`2"/> zu suchende Schlüssel.</param>
        /// <param name="rootOnly">indicates whether to lookup only root values</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> ist null.</exception>
        public bool ContainsKey(string key, bool rootOnly)
        {
            return ContainsKeyInternal(key, rootOnly, string.IsNullOrEmpty(ImplicitContext));
        }

        /// <summary>
        /// Ermittelt, ob <see cref="T:System.Collections.Generic.IDictionary`2"/> ein Element mit dem angegebenen Schlüssel enthält.
        /// </summary>
        /// <returns>
        /// true, wenn das <see cref="T:System.Collections.Generic.IDictionary`2"/> ein Element mit dem Schlüssel enthält, andernfalls false.
        /// </returns>
        /// <param name="key">Der im <see cref="T:System.Collections.Generic.IDictionary`2"/> zu suchende Schlüssel.</param>
        /// <param name="rootOnly">indicates whether to lookup only root values</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> ist null.</exception>
        public bool ContainsKeyInternal(string key, bool rootOnly, bool checkExistence)
        {
            //return variables.Any(n => n.Key == key && n.Value.Layer <= layer && n.Value.Layer > -1); // GetDictionaryForKey(key) != null;
            if (checkExistence)
            {
                ScopeVar tmp;
                return variables.ContainsKey(key) &&
                       ((tmp = variables[key]).Layer <= layer && tmp.Revision == layerRevisions[tmp.Layer]) &&
                       (!rootOnly || tmp.Layer == 1);
            }

            return true;
        }

        /// <summary>
        /// Fügt der <see cref="T:System.Collections.Generic.IDictionary`2"/>-Schnittstelle ein Element mit dem angegebenen Schlüssel und Wert hinzu.
        /// </summary>
        /// <param name="key">Das Objekt, das als Schlüssel für das hinzuzufügende Element verwendet werden soll.</param><param name="value">Das Objekt, das als Wert für das hinzuzufügende Element verwendet werden soll.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> ist null.</exception><exception cref="T:System.ArgumentException">Ein Element mit demselben Schlüssel ist bereits in <see cref="T:System.Collections.Generic.IDictionary`2"/> vorhanden.</exception><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.IDictionary`2"/> ist schreibgeschützt.</exception>
        public void Add(string key, object value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException("Tried to insert a Duplicate Key");
            }

            /*#if UseVarList
            scopes[currentMaxScopeId].Add(key, value);
#else
            scopes.Peek().Add(key, value);
#endif*/
            SetValue(key, value);
        }

        /// <summary>
        /// Entfernt das Element mit dem angegebenen Schlüssel aus dem <see cref="T:System.Collections.Generic.IDictionary`2"/>.
        /// </summary>
        /// <returns>
        /// true, wenn das Element erfolgreich entfernt wurde, andernfalls false.Diese Methode gibt auch dann false zurück, wenn <paramref name="key"/> nicht im ursprünglichen <see cref="T:System.Collections.Generic.IDictionary`2"/> gefunden wurde.
        /// </returns>
        /// <param name="key">Der Schlüssel des zu entfernenden Elements.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> ist null.</exception><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.IDictionary`2"/> ist schreibgeschützt.</exception>
        public bool Remove(string key)
        {
            /*Dictionary<string, object> dict = GetDictionaryForKey(key);
            return dict != null && dict.Remove(key);*/
            bool retVal = ContainsKey(key);
            if (retVal)
            {
                variables[key].Revision = -1;
            }

            return retVal;
            //return true;
        }

        /// <summary>
        /// Ruft den dem angegebenen Schlüssel zugeordneten Wert ab.
        /// </summary>
        /// <returns>
        /// true, wenn das Objekt, das <see cref="T:System.Collections.Generic.IDictionary`2"/> implementiert, ein Element mit dem angegebenen Schlüssel enthält, andernfalls false.
        /// </returns>
        /// <param name="key">Der Schlüssel, dessen Wert abgerufen werden soll.</param><param name="value">Wenn diese Methode zurückgegeben wird, enthält sie den dem angegebenen Schlüssel zugeordneten Wert, wenn der Schlüssel gefunden wird, andernfalls enthält sie den Standardwert für den Typ des <paramref name="value"/>-Parameters.Dieser Parameter wird nicht initialisiert übergeben.</param><exception cref="T:System.ArgumentNullException"><paramref name="key"/> ist null.</exception>
        public bool TryGetValue(string key, out object value)
        {
            /*bool retVal = false;
            value = null;
            Dictionary<string, object> dict = GetDictionaryForKey(key);
#pragma warning disable 665
            if (retVal = (dict != null))
#pragma warning restore 665
            {
                value = dict[key];
            }

            return retVal;*/

            value = GetValue(key);
            return value != null;
        }

        /// <summary>
        /// Opens an inner scope
        /// </summary>
        public void OpenInnerScope(/*bool isCatch = false*/)
        {
            /*#if UseDummies
                        bool dummy = this.nextDummy;
            #else
                        bool dummy = false;
            #endif*/
            //ScopeDisposer retVal = new ScopeDisposer(this, false, isCatch);
            /*if (!dummy)
            {
                Push();
#if UseDummies
                //PushDummy(this.nextDummy);
#endif
            }*/
            layer++;
            if (layerRevisions.Count <= layer)
            {
                layerRevisions.Add(0);
            }

            layerRevisions[layer] = ++currentRevision;
            /*if (isCatch)
            {
                catchCount++;
            }*/
#if UseDummies
    //this.nextDummy = nextDummy;
#endif
            //return retVal;
        }

        /*#if UseDummies
                /// <summary>
                /// Handles all Dummy - Events
                /// </summary>
                /// <param name="isDummy"></param>
                private void PushDummy(bool isDummy)
                {
        #if UseList
                    currentMaxDummyId++;
                    if (!(dummyStack.Count > currentMaxDummyId))
                    {
                        dummyStack.Add(isDummy);
                    }
                    else
                    {
                        dummyStack[currentMaxDummyId] = isDummy;
                    }
        #else
                    dummyStack.Push(isDummy);
        #endif
                }
        #endif*/

        /// <summary>
        /// Collapses an inner scope
        /// </summary>
        public void CollapseScope(/*bool isCatch = false*/)
        {
            /*#if UseDummies
            #if UseList
                        nextDummy = dummyStack[currentMaxDummyId];
                        currentMaxDummyId--;
            #else
                        //nextDummy = false; //dummyStack.Pop();
            #endif
            #endif
                        #if UseVarList
                        currentMaxScopeId--;
            #else
                        Dictionary<string, object> dict = scopes.Pop();
                        dict.Clear();
            #endif*/
            layer--;
            //OnLeaveLayer(layer);
            /*if (isCatch)
            {
                catchCount--;
            }*/
        }

        /*internal void DecreaseCatches()
        {
            catchCount--;
        }*/

        /* /// <summary>
         /// Gets the dictionary that contains a specific key
         /// </summary>
         /// <param name="key">the demanded key</param>
         /// <returns>the dictionary that contains the demanded key</returns>
         private Dictionary<string, object> GetDictionaryForKey(string key)
         {
             #if UseVarList
             Dictionary<string, object> retVal = null;
             for (int i = currentMaxScopeId; i >= 0; i--)
             {
                 if ((retVal = scopes[i]).ContainsKey(key))
                 {
                     return retVal;
                 }
             }

             return null;
 #else
             return (from t in scopes where t.ContainsKey(key) select t).FirstOrDefault();
 #endif
         }*/

        /// <summary>
        /// Copies the initial root of the current scope
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> CopyInitial()
        {
            Dictionary<string, object> retVal = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ScopeVar> val in variables.Where(n => n.Value.Layer == 0))
            {
                retVal.Add(val.Key, val.Value.Value);
            }

            return retVal;
        }

        public Dictionary<string, object> Snapshot()
        {
            Dictionary<string, object> retVal = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ScopeVar> val in variables.Where(n => n.Value.Layer <= layer && n.Value.Revision == layerRevisions[n.Value.Layer]))
            {
                retVal.Add(val.Key, val.Value.Value);
            }

            return retVal;
        }

#if !Community
        public SmartProperty GetSmartProperty(string name, bool rootOnly = false)
        {
            if (ContainsKeyInternal(name, rootOnly, true))
            {
                var retVal = variables[name];
                if (retVal.Layer <= layer && retVal.Revision == layerRevisions[retVal.Layer])
                {
                    return retVal.GetSmartProperty();
                }

            }

            return null;
        }
#endif

        void IScope.OverridePolicy(ScriptingPolicy newPolicy)
        {
            overridePolicy = newPolicy;
        }

        private object GetValue(string name, bool rootOnly = false)
        {
            if (ContainsKeyInternal(name, rootOnly,true))
            {
                var retVal = variables[name];
                if (retVal.Layer <= layer && retVal.Revision == layerRevisions[retVal.Layer])
                {
                    return retVal.Value;
                }

            }
            else if (!string.IsNullOrEmpty(ImplicitContext) && name != ImplicitContext)
            {
                var retVal = GetValue(ImplicitContext);
                return retVal.GetMemberValue(name, null, ValueType.PropertyOrField, ((IScope)this).ScriptingPolicy, MemberAccessMode.Read);

            }

            return null;
        }

        private void SetValue(string name, object value)
        {
            ScopeVar var = null;
            if (variables.ContainsKey(name))
            {
                var = variables[name];
            }

            if (!string.IsNullOrEmpty(ImplicitContext))
            {
                try
                {
                    var tmp = GetValue(ImplicitContext);
                    if (!(tmp is IDictionary<string, object> dc && !dc.ContainsKey(name)) && !(tmp is IBasicKeyValueProvider kv && !kv.ContainsKey(name)))
                    {
                        tmp.SetMemberValue(name, value, null, ValueType.PropertyOrField, policy);
                        return;
                    }
                }
                catch{ }
            }

            if (var == null)
            {
                var = new ScopeVar();
                variables[name] = var;
            }

            var.Value = value;
            if (var.Layer > layer || var.Revision < layerRevisions[var.Layer])
            {
                var.Layer = layer;
                var.Revision = layerRevisions[var.Layer];
            }
        }

        /*/// <summary>
        /// Creates a new scope on the current scope-stack
        /// </summary>
        /// <returns>the created scope</returns>
        private Dictionary<string, object> Push()
        {
#if UseVarList
            currentMaxScopeId++;
            while (scopes.Count <= currentMaxScopeId)
            {
                scopes.Add(new Dictionary<string, object>());
            }


            Dictionary<string,object> retVal = scopes[currentMaxScopeId];
            retVal.Clear();
            return retVal;
#else
            Dictionary<string, object> retVal = new Dictionary<string, object>();
            scopes.Push(retVal);
            return retVal;
#endif
        }*/
    }
}
