using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using ImpromptuInterface;
#if !Community
using ITVComponents.ExtendedFormatting;
#endif
using ITVComponents.Scripting.CScript.Core.Invokation;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Core.Literals
{
    public class ObjectLiteral : DynamicObject, IScope
    {
        private IScope parent;
        private ConcurrentDictionary<string, object> values;
        private ScriptingPolicy overridePolicy;

        public ObjectLiteral(Dictionary<string, object> values, IScope parent)
        {
            this.values = new ConcurrentDictionary<string, object>(values);
            this.parent = parent;
        }

        /// <summary>
        /// Gets the value of the specified variable name
        /// </summary>
        /// <param name="memberName">the variable for which to check</param>
        /// <param name="rootOnly">indicates whether to check only in the initial scope</param>
        /// <returns>the value of the requeted variable or null if it does not exist</returns>
        object IScope.this[string memberName, bool rootOnly] { get { return this[memberName]; } }

        /// <summary>
        /// Gets or sets the value with the specified name of this object
        /// </summary>
        /// <param name="name">the name of which the name is being set on this object</param>
        /// <returns>the value that is bound to the given name</returns>
        public object this[string name]
        {
            get
            {
                if (name == "this")
                {
                    return this;
                }

                if (values.ContainsKey(name))
                {
                    return values[name];
                }

                if (parent?.ContainsKey(name)??false)
                {
                    return parent[name, true];
                }

                return null;
            }
            set
            {
                if (name == "this")
                {
                    throw new InvalidOperationException("Unable to set the value of this!");
                }

                values[name] = value;
                var literal = value as ObjectLiteral;
                if (literal != null)
                {
                    literal.parent = this;
                }
            }
        }

#if !Community
        public SmartProperty GetSmartProperty(string name, bool rootOnly = false)
        {
            throw new InvalidOperationException("GetSmartProperty is not supported in ObjectLiteral!");
        }

        ScriptingPolicy IScope.ScriptingPolicy => overridePolicy??parent.ScriptingPolicy;
#endif
        /// <summary>Ruft eine <see cref="T:System.Collections.Generic.ICollection`1" />-Schnittstelle ab, die die Schlüssel von <see cref="T:System.Collections.Generic.IDictionary`2" /> enthält.</summary>
        /// <returns>Eine <see cref="T:System.Collections.Generic.ICollection`1" />, die die Schlüssel des Objekts enthält, das <see cref="T:System.Collections.Generic.IDictionary`2" /> implementiert.</returns>
        public ICollection<string> Keys => values.Keys;

        /// <summary>Ruft eine <see cref="T:System.Collections.Generic.ICollection`1" /> ab, die die Werte in <see cref="T:System.Collections.Generic.IDictionary`2" /> enthält.</summary>
        /// <returns>Eine <see cref="T:System.Collections.Generic.ICollection`1" />, die die Werte des Objekts enthält, das <see cref="T:System.Collections.Generic.IDictionary`2" /> implementiert.</returns>
        public ICollection<object> Values { get { return values.Values; } }

        /// <summary>Ruft die Anzahl der Elemente ab, die in <see cref="T:System.Collections.Generic.ICollection`1" /> enthalten sind.</summary>
        /// <returns>Die Anzahl der Elemente, die in <see cref="T:System.Collections.Generic.ICollection`1" /> enthalten sind.</returns>
        public int Count { get { return values.Count ; } }

        /// <summary>Ruft einen Wert ab, der angibt, ob <see cref="T:System.Collections.Generic.ICollection`1" /> schreibgeschützt ist.</summary>
        /// <returns>true, wenn <see cref="T:System.Collections.Generic.ICollection`1" /> schreibgeschützt ist, andernfalls false.</returns>
        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Gets a value indicating whether a specific key exists in the current scope
        /// </summary>
        /// <param name="key">the name for which to check the current scope</param>
        /// <param name="rootOnly">indicates whether to check only the base-scope</param>
        /// <returns>a value indicating whether the scope contains the requested value</returns>
        public bool ContainsKey(string key, bool rootOnly)
        {
            bool retVal = key == "this";
            if (!retVal)
            {
                retVal = values.ContainsKey(key);
            }

            if (!retVal)
            {
                retVal = parent?.ContainsKey(key, true)??false;
            }

            return retVal;
        }

        /// <summary>
        /// Creates an ExpandoObject containing all information of this ObjectLiteral
        /// </summary>
        /// <returns>an expandoObject representing this ObjectLiteral</returns>
        public ExpandoObject ToExpandoObject()
        {
            ExpandoObject retVal = new ExpandoObject();
            ICollection<KeyValuePair<string, object>> eoc = retVal;
            foreach (var keyValuePair in values)
            {
                eoc.Add(keyValuePair);
            }

            return retVal;
        }

        /// <summary>Ermittelt, ob <see cref="T:System.Collections.Generic.IDictionary`2" /> ein Element mit dem angegebenen Schlüssel enthält.</summary>
        /// <returns>true, wenn das <see cref="T:System.Collections.Generic.IDictionary`2" /> ein Element mit dem Schlüssel enthält, andernfalls false.</returns>
        /// <param name="key">Der im <see cref="T:System.Collections.Generic.IDictionary`2" /> zu suchende Schlüssel.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> ist null.</exception>
        public bool ContainsKey(string key)
        {
            return ContainsKey(key,false);
        }

        /// <summary>Fügt der <see cref="T:System.Collections.Generic.IDictionary`2" />-Schnittstelle ein Element mit dem angegebenen Schlüssel und Wert hinzu.</summary>
        /// <param name="key">Das Objekt, das als Schlüssel für das hinzuzufügende Element verwendet werden soll.</param>
        /// <param name="value">Das Objekt, das als Wert für das hinzuzufügende Element verwendet werden soll.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> ist null.</exception>
        /// <exception cref="T:System.ArgumentException">Ein Element mit demselben Schlüssel ist bereits in <see cref="T:System.Collections.Generic.IDictionary`2" /> vorhanden.</exception>
        /// <exception cref="T:System.NotSupportedException">
        /// <see cref="T:System.Collections.Generic.IDictionary`2" /> ist schreibgeschützt.</exception>
        public void Add(string key, object value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException(nameof(key),
                    "The given variablename already exists in this scope or its parent");
            }

            values.AddOrUpdate(key, value, (k, v) => value);
        }

        /// <summary>Entfernt das Element mit dem angegebenen Schlüssel aus dem <see cref="T:System.Collections.Generic.IDictionary`2" />.</summary>
        /// <returns>true, wenn das Element erfolgreich entfernt wurde, andernfalls false.Diese Methode gibt auch dann false zurück, wenn <paramref name="key" /> nicht im ursprünglichen <see cref="T:System.Collections.Generic.IDictionary`2" /> gefunden wurde.</returns>
        /// <param name="key">Der Schlüssel des zu entfernenden Elements.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> ist null.</exception>
        /// <exception cref="T:System.NotSupportedException">
        /// <see cref="T:System.Collections.Generic.IDictionary`2" /> ist schreibgeschützt.</exception>
        public bool Remove(string key)
        {
            object val;
            return values.TryRemove(key, out val);
        }

        /// <summary>Ruft den dem angegebenen Schlüssel zugeordneten Wert ab.</summary>
        /// <returns>true, wenn das Objekt, das <see cref="T:System.Collections.Generic.IDictionary`2" /> implementiert, ein Element mit dem angegebenen Schlüssel enthält, andernfalls false.</returns>
        /// <param name="key">Der Schlüssel, dessen Wert abgerufen werden soll.</param>
        /// <param name="value">Wenn diese Methode zurückgegeben wird, enthält sie den dem angegebenen Schlüssel zugeordneten Wert, wenn der Schlüssel gefunden wird, andernfalls enthält sie den Standardwert für den Typ des <paramref name="value" />-Parameters.Dieser Parameter wird nicht initialisiert übergeben.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> ist null.</exception>
        public bool TryGetValue(string key, out object value)
        {
            return values.TryGetValue(key, out value) || (parent?.TryGetValue(key, out value)??false);
        }

        public Dictionary<string, object> CopyInitial()
        {
            return new Dictionary<string, object>(values);
        }

        public Dictionary<string, object> Snapshot()
        {
            return new Dictionary<string, object>(values);
        }

        public void OpenInnerScope()
        {
        }

        public void CollapseScope()
        {
        }

        public void Clear(IDictionary<string, object> rootVariables)
        {
        }

        void IScope.OverridePolicy(ScriptingPolicy newPolicy)
        {
            overridePolicy = newPolicy;
        }


        /// <summary>Gibt die Enumeration aller dynamischen Membernamen zurück. </summary>
        /// <returns>Eine Sequenz, die dynamische Membernamen enthält.</returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return values.Keys;
        }

        /// <summary>
        /// Casts this object to an instance of the demanded type as long as it is an interface
        /// </summary>
        ///// <param name="targetType">the target interface type</param>
        /// <returns>the representation of the given interface wrapping this instance</returns>
        public object Cast(Type targetType)
        {
            if (targetType.IsInterface)
            {
                return Impromptu.DynamicActLike(this, targetType);
            }

            return null;
        }

        /// <summary>
        /// Casts this object to an instance of the demanded type as long as it is an interface
        /// </summary>
        /// <typeparam name="T">the target interface type</typeparam>
        /// <returns>the representation of the given interface wrapping this instance</returns>
        public T Cast<T>() where T : class
        {
            if (typeof(T).IsInterface)
            {
                return this.ActLike<T>();
            }

            return default(T);
        }

        /// <summary>Stellt die Implementierung für Vorgänge bereit, die Memberwerte abrufen.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Abrufen eines Werts für eine Eigenschaft anzugeben.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zum Objekt bereit, das den dynamischen Vorgang aufgerufen hat.Die binder.Name-Eigenschaft gibt den Namen des Members an, für den der dynamische Vorgang ausgeführt wird.Für die Console.WriteLine(sampleObject.SampleProperty)-Anweisung, in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleProperty" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param>
        /// <param name="result">Das Ergebnis des get-Vorgangs.Wenn die Methode z. B. für eine Eigenschaft aufgerufen wird, können Sie <paramref name="result" /> den Eigenschaftswert zuweisen.</param>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            bool retVal = values.ContainsKey(binder.Name);
            result = retVal ? values[binder.Name] : null;
            return retVal;
        }

        /// <summary>Stellt die Implementierung für Typkonvertierungsvorgänge bereit.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Operationen anzugeben, die ein Objekt von einem Typ in einen anderen konvertieren.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zur Konvertierungsoperation bereit.Die binder.Type-Eigenschaft stellt den Typ bereit, in den das Objekt konvertiert werden muss.Für die Anweisung (String)sampleObject in C# (CType(sampleObject, Type) in Visual Basic), bei der sampleObject eine Instanz der von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleiteten Klasse ist, gibt binder.Type z. B. den <see cref="T:System.String" />-Typ zurück.Die binder.Explicit-Eigenschaft stellt Informationen zur Art der ausgeführten Konvertierung bereit.Für die explizite Konvertierung wird true und für die implizite Konvertierung wird false zurückgegeben.</param>
        /// <param name="result">Das Ergebnis des Typkonvertierungsvorgangs.</param>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type.IsInterface)
            {
                result = Impromptu.DynamicActLike(this, binder.Type);
                return true;
            }

            result = this;
            return false;
        }

        /// <summary>Stellt die Implementierung für Vorgänge bereit, die Memberwerte festlegen.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Festlegen eines Werts für eine Eigenschaft anzugeben.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zum Objekt bereit, das den dynamischen Vorgang aufgerufen hat.Die binder.Name-Eigenschaft gibt den Namen des Members an, dem der Wert zugewiesen wird.Für die Anweisung sampleObject.SampleProperty = "Test", in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleProperty" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param>
        /// <param name="value">Der Wert, der auf den Member festgelegt werden soll.Für die sampleObject.SampleProperty = "Test"-Anweisung, in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Instanz der Klasse ist, ist <paramref name="value" /> z. B. "Test".</param>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            values[binder.Name] = value;
            return true;
        }

        /// <summary>Stellt die Implementierung für Vorgänge bereit, die einen Member aufrufen.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Aufrufen einer Methode anzugeben.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zum dynamischen Vorgang bereit.Die binder.Name-Eigenschaft gibt den Namen des Members an, für den der dynamische Vorgang ausgeführt wird.Für die Anweisung sampleObject.SampleMethod(100), in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleMethod" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param>
        /// <param name="args">Die Argumente, die während des Aufrufvorgangs an den Objektmember übergeben werden.Für die Anweisung sampleObject.SampleMethod(100), in der sampleObject von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitet ist, entspricht <paramref name="args[0]" /> z. B. 100.</param>
        /// <param name="result">Das Ergebnis des Memberaufrufs.</param>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (this[binder.Name] is FunctionLiteral)
            {
                result = ((FunctionLiteral) this[binder.Name]).Invoke(args);
                return true;
            }

            if (this[binder.Name] is InvokationHelper)
            {
                bool ok;
                result = ((InvokationHelper) this[binder.Name]).Invoke(args, out ok);
                return ok;
            }

            if (this[binder.Name] is Delegate)
            {
                result = ((Delegate) this[binder.Name]).DynamicInvoke(args);
                return true;
            }

            result = null;
            return false;
        }

        #region Implementation of IEnumerable

        /// <summary>Gibt einen Enumerator zurück, der die Auflistung durchläuft.</summary>
        /// <returns>Ein <see cref="T:System.Collections.Generic.IEnumerator`1" />, der zum Durchlaufen der Auflistung verwendet werden kann.</returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return values.GetEnumerator();
        }

        /// <summary>Gibt einen Enumerator zurück, der eine Auflistung durchläuft.</summary>
        /// <returns>Ein <see cref="T:System.Collections.IEnumerator" />-Objekt, das zum Durchlaufen der Auflistung verwendet werden kann.</returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Implementation of ICollection<KeyValuePair<string,object>>

        /// <summary>Fügt der <see cref="T:System.Collections.Generic.ICollection`1" /> ein Element hinzu.</summary>
        /// <param name="item">Das Objekt, das <see cref="T:System.Collections.Generic.ICollection`1" /> hinzugefügt werden soll.</param>
        /// <exception cref="T:System.NotSupportedException">
        /// <see cref="T:System.Collections.Generic.ICollection`1" /> ist schreibgeschützt.</exception>
        public void Add(KeyValuePair<string, object> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            values.Clear();
        }

        /// <summary>Bestimmt, ob <see cref="T:System.Collections.Generic.ICollection`1" /> einen bestimmten Wert enthält.</summary>
        /// <returns>true, wenn sich <paramref name="item" /> in <see cref="T:System.Collections.Generic.ICollection`1" /> befindet, andernfalls false.</returns>
        /// <param name="item">Das im <see cref="T:System.Collections.Generic.ICollection`1" /> zu suchende Objekt.</param>
        public bool Contains(KeyValuePair<string, object> item)
        {
            return ContainsKey(item.Key) && this[item.Key] == item.Value;
        }

        /// <summary>Kopiert die Elemente von <see cref="T:System.Collections.Generic.ICollection`1" /> in ein <see cref="T:System.Array" />, beginnend bei einem bestimmten <see cref="T:System.Array" />-Index.</summary>
        /// <param name="array">Das eindimensionale <see cref="T:System.Array" />, das das Ziel der aus <see cref="T:System.Collections.Generic.ICollection`1" /> kopierten Elemente ist.Für <see cref="T:System.Array" /> muss eine nullbasierte Indizierung verwendet werden.</param>
        /// <param name="arrayIndex">Der nullbasierte Index in <paramref name="array" />, an dem das Kopieren beginnt.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="array" /> hat den Wert null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex" /> ist kleiner als 0.</exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="array" /> ist mehrdimensional.- oder -Die Anzahl der Elemente in der Quelle <see cref="T:System.Collections.Generic.ICollection`1" /> ist größer als der verfügbare Speicherplatz ab <paramref name="arrayIndex" /> bis zum Ende des <paramref name="array" />, das als Ziel festgelegt wurde.- oder -Typ <paramref name="T" /> kann nicht automatisch in den Typ des Ziel-<paramref name="array" /> umgewandelt werden.</exception>
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            int id = arrayIndex;
            foreach (var item in this)
            {
                array[id] = item;
                id++;
            }
        }

        /// <summary>Entfernt das erste Vorkommen eines bestimmten Objekts aus <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <returns>true, wenn <paramref name="item" /> erfolgreich aus <see cref="T:System.Collections.Generic.ICollection`1" /> gelöscht wurde, andernfalls false.Diese Methode gibt auch dann false zurück, wenn <paramref name="item" /> nicht in der ursprünglichen <see cref="T:System.Collections.Generic.ICollection`1" /> gefunden wurde.</returns>
        /// <param name="item">Das aus dem <see cref="T:System.Collections.Generic.ICollection`1" /> zu entfernende Objekt.</param>
        /// <exception cref="T:System.NotSupportedException">
        /// <see cref="T:System.Collections.Generic.ICollection`1" /> ist schreibgeschützt.</exception>
        public bool Remove(KeyValuePair<string, object> item)
        {
            if (values.ContainsKey(item.Key) && values[item.Key] == item.Value)
            {
                object obj;
                return values.TryRemove(item.Key, out obj);
            }

            return false;
        }

        #endregion
    }
}
