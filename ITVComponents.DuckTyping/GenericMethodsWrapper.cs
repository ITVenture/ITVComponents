using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.DuckTyping
{
    public class GenericMethodsWrapper:DynamicObject
    {
        private MemberMap memberMap;

        public GenericMethodsWrapper(Type staticType, Type implementationProvider, Type genericInterface = null, int minGenericParameterCount = 1, params (string name, Type type)[] fixMappings)
        {
            memberMap = new MemberMap(staticType,
                implementationProvider.ImplementGenericMethods(staticType, genericInterface, fixTypeEntries:fixMappings), minGenericParameterCount);
        }

        public void ExtendWith(Type staticType, Type implementationProvider, Type genericInterface = null,
            params (string name, Type type)[] fixMappings)
        {
            memberMap.ExtendWith(staticType,
                implementationProvider.ImplementGenericMethods(staticType, genericInterface, fixTypeEntries: fixMappings));
        }

        public bool PreferGenericMethods
        {
            get { return memberMap.PreferGenerics; }
            set { memberMap.PreferGenerics = value; }
        }

        /// <summary>
        /// Gibt die Enumeration aller dynamischen Membernamen zurück. 
        /// </summary>
        /// <returns>
        /// Eine Sequenz, die dynamische Membernamen enthält.
        /// </returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return memberMap.GetAllNames();
        }

        /// <summary>
        /// Stellt die Implementierung für Vorgänge bereit, die Memberwerte abrufen.Von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Abrufen eines Werts für eine Eigenschaft anzugeben.
        /// </summary>
        /// <returns>
        /// true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine Laufzeitausnahme ausgelöst.)
        /// </returns>
        /// <param name="binder">Stellt Informationen zum Objekt bereit, das den dynamischen Vorgang aufgerufen hat.Die binder.Name-Eigenschaft gibt den Namen des Members an, für den der dynamische Vorgang ausgeführt wird.Für die Console.WriteLine(sampleObject.SampleProperty)-Anweisung, in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleProperty" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param><param name="result">Das Ergebnis des get-Vorgangs.Wenn die Methode z. B. für eine Eigenschaft aufgerufen wird, können Sie <paramref name="result"/> den Eigenschaftswert zuweisen.</param>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            MemberInfo foundMember = FindMember(binder.Name, false);
            bool retVal = false;
            result = null;
            if (foundMember != null)
            {
                result = ReadMember(foundMember, out retVal);
            }

            return retVal;
        }

        /// <summary>
        /// Stellt die Implementierung für Vorgänge bereit, die einen Member aufrufen.Von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Aufrufen einer Methode anzugeben.
        /// </summary>
        /// <returns>
        /// true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)
        /// </returns>
        /// <param name="binder">Stellt Informationen zum dynamischen Vorgang bereit.Die binder.Name-Eigenschaft gibt den Namen des Members an, für den der dynamische Vorgang ausgeführt wird.Für die Anweisung sampleObject.SampleMethod(100), in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleMethod" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param><param name="args">Die Argumente, die während des Aufrufvorgangs an den Objektmember übergeben werden.Für die Anweisung sampleObject.SampleMethod(100), in der sampleObject von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitet ist, entspricht <paramref name="args[0]"/> z. B. 100.</param><param name="result">Das Ergebnis des Memberaufrufs.</param>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            bool retVal = false;
            result = null;
            try
            {
                MethodInfo method = FindMethod(binder.Name, args);
                if (method != null)
                {
                    result = method.Invoke(null, args);
                    retVal = true;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }

            return retVal;
        }

        /// <summary>
        /// Stellt die Implementierung für Vorgänge bereit, die Memberwerte festlegen.Von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Festlegen eines Werts für eine Eigenschaft anzugeben.
        /// </summary>
        /// <returns>
        /// true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)
        /// </returns>
        /// <param name="binder">Stellt Informationen zum Objekt bereit, das den dynamischen Vorgang aufgerufen hat.Die binder.Name-Eigenschaft gibt den Namen des Members an, dem der Wert zugewiesen wird.Für die Anweisung sampleObject.SampleProperty = "Test", in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleProperty" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param><param name="value">Der Wert, der auf den Member festgelegt werden soll.Für die sampleObject.SampleProperty = "Test"-Anweisung, in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Instanz der Klasse ist, ist <paramref name="value"/> z. B. "Test".</param>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            MemberInfo foundMember = FindMember(binder.Name, true);
            bool retVal = false;
            if (foundMember != null)
            {
                WriteMember(foundMember, value, out retVal);
            }

            return retVal;
        }

        /// <summary>
        /// Gets a property or field descriptor for the given name
        /// </summary>
        /// <param name="name">the name of the desired member</param>
        /// <param name="writable">indicates whether the member must be writable</param>
        /// <returns>a MemberInfo instance describing the desired member</returns>
        protected MemberInfo FindMember(string name, bool writable)
        {

            MemberInfo info = (MemberInfo)memberMap.FindProperty(name) ?? memberMap.FindField(name);

            if (info != null)
            {
                if (info is FieldInfo)
                {
                    FieldInfo fi = (FieldInfo)info;
                    if ((!fi.IsLiteral && !fi.IsInitOnly) || !writable)
                    {
                        return fi;
                    }
                }

                if (info is PropertyInfo)
                {
                    PropertyInfo pi = (PropertyInfo)info;
                    if ((pi.CanWrite && writable) || (!writable && pi.CanRead))
                    {
                        return pi;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Writes the value of a specific member with the given value
        /// </summary>
        /// <param name="target">the target member that is being written to</param>
        /// <param name="value">the value to set on the member</param>
        /// <param name="success">indicates whether the value could be set</param>
        protected void WriteMember(MemberInfo target, object value, out bool success)
        {
            success = false;
            if (target is FieldInfo)
            {
                FieldInfo fi = (FieldInfo)target;
                fi.SetValue(null, value);
                success = true;
            }
            else if (target is PropertyInfo)
            {
                PropertyInfo pi = (PropertyInfo)target;
                pi.SetValue(null, value, null);
                success = true;
            }
        }

        /// <summary>
        /// Reads the value of the specified member
        /// </summary>
        /// <param name="target">the target from which to get the value</param>
        /// <param name="success">indicates whether the property or field could be read</param>
        /// <returns>the value of the given property or field</returns>
        protected object ReadMember(MemberInfo target, out bool success)
        {
            object retVal = null;
            success = false;
            if (target is FieldInfo)
            {
                FieldInfo fi = (FieldInfo)target;
                retVal = fi.GetValue(null);
                success = true;
            }
            else if (target is PropertyInfo)
            {
                PropertyInfo pi = (PropertyInfo)target;
                retVal = pi.GetValue(null, null);
                success = true;
            }

            return retVal;
        }

        /// <summary>
        /// Finds the Method on the Class that best fits the target method and its call-information
        /// </summary>
        /// <param name="name">the method name</param>
        /// <param name="arguments">the method arguments</param>
        /// <returns>a method info that best fits the desired method</returns>
        protected MethodInfo FindMethod(string name, object[] arguments)
        {
            StackTrace trace = new StackTrace();
            StackFrame[] frames = trace.GetFrames();
            MethodBase originalMethod = null;
            foreach (StackFrame frame in frames)
            {
                MethodBase method = frame.GetMethod();
                if (method.DeclaringType != null && method.DeclaringType.FullName.Contains("ActLike_") && method.DeclaringType.Assembly.IsDynamic)
                {
                    originalMethod = method;
                    break;
                }
            }

            Type[] types;
            if (originalMethod != null)
            {
                types = (from t in originalMethod.GetParameters() select t.ParameterType).ToArray();
            }
            else
            {
                types = Type.GetTypeArray(arguments);
            }

            return memberMap.FindMethod(name, types);
        }

        private class MemberMap
        {
            /// <summary>
            /// holds all methods that are mapped in this membermap
            /// </summary>
            private List<MethodInfo> methods;

            /// <summary>
            /// Holds all properties that are mapped in this membermap
            /// </summary>
            private List<PropertyInfo> properties;

            /// <summary>
            /// holds all fields that are mapped in this memberMap
            /// </summary>
            private List<FieldInfo> fields;

            /// <summary>
            /// Initializes a new instance of the MemberMap class
            /// </summary>
            /// <param name="targetType">the wrapped target type</param>
            public MemberMap(Type targetType, IEnumerable<MethodInfo> genericMethods, int minGenericParameterCount) : this()
            {
                ExtendWith(targetType,genericMethods, minGenericParameterCount);
            }

            public void ExtendWith(Type targetType, IEnumerable<MethodInfo> genericMethods, int minGenericParameterCount = 1)
            {
                MemberInfo[] allmembers =
                    targetType.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.GetField |
                                          BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty);
                foreach (MemberInfo member in allmembers)
                {
                    MethodInfo method = member as MethodInfo;
                    PropertyInfo property = member as PropertyInfo;
                    FieldInfo field = member as FieldInfo;
                    if (method != null)
                    {
                        if (!method.IsGenericMethod)
                        {
                            methods.Add(method);
                        }
                    }
                    else if (property != null)
                    {

                        properties.Add(property);
                    }
                    else if (field != null)
                    {
                        fields.Add(field);
                    }
                }

                methods.AddRange(genericMethods.Where(n => n.GetGenericArguments().Length >= minGenericParameterCount));
            }

            /// <summary>
            /// Prevents a defaultInstance of the MemberMap class from being created
            /// </summary>
            private MemberMap()
            {
                methods = new List<MethodInfo>();
                properties = new List<PropertyInfo>();
                fields = new List<FieldInfo>();
            }

            public bool PreferGenerics { get; set; } = true;

            /// <summary>
            /// Gets a method with the given name and signature
            /// </summary>
            /// <param name="name">the requested method name</param>
            /// <param name="arguments">the arguments that identify the signature of the requested method</param>
            /// <returns>a methodInfo that is represented by the given name and signature</returns>
            public MethodInfo FindMethod(string name, Type[] arguments)
            {
                if (PreferGenerics)
                {
                    var tmp = (from t in methods
                        where t.IsGenericMethod && !t.ContainsGenericParameters && t.Name == name && TypesEqual(t.GetParameters(), arguments)
                        select t).FirstOrDefault();
                    if (tmp != null)
                    {
                        return tmp;
                    }
                }

                return
                    (from t in methods where t.Name == name && TypesEqual(t.GetParameters(), arguments) select t)
                        .FirstOrDefault();
            }

            /// <summary>
            /// Gets the Property with the given name
            /// </summary>
            /// <param name="name">the name of the Property</param>
            /// <returns>the PropertyInfo that is represented by the given name</returns>
            public PropertyInfo FindProperty(string name)
            {
                return
                    (from t in properties where t.Name == name select t)
                        .FirstOrDefault();
            }

            /// <summary>
            /// Gets the Field with the given name
            /// </summary>
            /// <param name="name">the name of the requested field</param>
            /// <returns>the FieldInfo that is represented by the given name</returns>
            public FieldInfo FindField(string name)
            {
                return (from t in fields where t.Name == name select t).FirstOrDefault();
            }

            /// <summary>
            /// Creates a distincted list of names represented by this map
            /// </summary>
            /// <returns>a distincted name-list of all members</returns>
            public IEnumerable<string> GetAllNames()
            {
                List<string> names = new List<string>();
                foreach (var m in methods)
                {
                    if (!names.Contains(m.Name))
                    {
                        yield return m.Name;
                        names.Add(m.Name);
                    }
                }

                foreach (var p in properties)
                {
                    if (!names.Contains(p.Name))
                    {
                        yield return p.Name;
                        names.Add(p.Name);
                    }
                }

                foreach (var f in fields)
                {
                    if (!names.Contains(f.Name))
                    {
                        yield return f.Name;
                        names.Add(f.Name);
                    }
                }
            }

            /// <summary>
            /// Compares the Parameter - types of two methods or indexers
            /// </summary>
            /// <param name="method1Arguments">the arguments of the first compared method/indexer</param>
            /// <param name="method2Arguments">the arguments of the second compared method/indexer</param>
            /// <returns>a value indicating whether the signatures of the compared methods are equal</returns>
            private bool TypesEqual(ParameterInfo[] method1Arguments, ParameterInfo[] method2Arguments)
            {
                bool retVal = method2Arguments != null && method1Arguments.Length == method2Arguments.Length;
                if (retVal)
                {
                    for (int i = 0; retVal && i < method1Arguments.Length; i++)
                    {
                        retVal &= method1Arguments[i].ParameterType == method2Arguments[i].ParameterType;
                    }
                }

                return retVal;
            }

            /// <summary>
            /// Compares the Parameter - types of two methods or indexers
            /// </summary>
            /// <param name="method1Arguments">the arguments of the first compared method/indexer</param>
            /// <param name="method2Arguments">the arguments of the second compared method/indexer</param>
            /// <returns>a value indicating whether the signatures of the compared methods are equal</returns>
            private bool TypesEqual(ParameterInfo[] method1Arguments, Type[] method2Arguments)
            {
                bool retVal = method1Arguments.Length == method2Arguments.Length;
                if (retVal)
                {
                    for (int i = 0; retVal && i < method1Arguments.Length; i++)
                    {
                        retVal &= method1Arguments[i].ParameterType == method2Arguments[i];
                    }
                }

                return retVal;
            }
        }
    }
}
