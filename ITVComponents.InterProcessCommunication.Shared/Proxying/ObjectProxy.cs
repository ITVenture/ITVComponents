using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.TypeConversion;

namespace ITVComponents.InterProcessCommunication.Shared.Proxying
{
    /// <summary>
    /// A Object proxy that wrapps a remote object into an interface
    /// </summary>
    public class ObjectProxy:DynamicObject, IBasicKeyValueProvider, IObjectProxy
    {
        private static readonly MethodInfo makeAsyncFunctionCallInfo = typeof(ObjectProxy).GetMethod(nameof(MakeAsyncFunctionCall), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod);

        private static ConcurrentDictionary<Type,MethodInfo> typeCalls = new ConcurrentDictionary<Type, MethodInfo>();

        /// <summary>
        /// the name of the wrapped object
        /// </summary>
        private string objectName;

        /// <summary>
        /// the base client that is used to invoke method and get/set properties
        /// </summary>
        private IBaseClient client;

        /// <summary>
        /// a list of async methods that can be executed on the server
        /// </summary>
        private Dictionary<string, List<MethodDefinition>> methods = new Dictionary<string, List<MethodDefinition>>();

        /// <summary>
        /// a list of events that is implemented in the client interface
        /// </summary>
        private Dictionary<string, Delegate> events = new Dictionary<string, Delegate>();

        /// <summary>
        /// a list of all properties that are not indexed
        /// </summary>
        private List<PropertyDefinition> properties = new List<PropertyDefinition>(); 

        /// <summary>
        /// an empty array that will be used to read norma properties
        /// </summary>
        private object[] emptyIndex = new object[0];

        /// <summary>
        /// Identifies the type of a task
        /// </summary>
        private static readonly Type TaskType = typeof(Task);

        /// <summary>
        ///Initializes a new instance of the ObjectProxy class
        /// </summary>
        /// <param name="serviceClient">the service client that is used to communicate with the target application</param>
        /// <param name="objectName">the object name that is wrapped by this proxy</param>
        /// <param name="expectedServiceType">the expected service type</param>
        public ObjectProxy(IBaseClient serviceClient, string objectName, Type expectedServiceType)
        {
            client = serviceClient;
            this.objectName = objectName;
            AnalyzeServiceType(expectedServiceType);
        }

        /// <summary>
        /// Finalizes this object
        /// </summary>
        ~ObjectProxy()
        {
            client.AbandonObject(objectName);
        }

        /// <summary>
        /// Gets the value that is associated with the given name
        /// </summary>
        /// <param name="name">the name requested by an object</param>
        /// <returns>the value associated with the given key</returns>
        public object this[string name]
        {
            get
            {
                var retVal = client.GetProperty(objectName, name, emptyIndex);
                var prop = properties.FirstOrDefault(n => n.Name == name);
                if (prop != null && retVal != null && !prop.PropertyType.IsInstanceOfType(retVal))
                {
                    retVal = TypeConverter.Convert(retVal, prop.PropertyType);
                }

                return retVal;
            }
        }

        /// <summary>
        /// Gets the ObjectName of the remote original object
        /// </summary>
        public string ObjectName { get { return objectName;} }

        /// <summary>
        /// Gets all MemberNames that are associated with this basicKeyValue provider instance
        /// </summary>
        public string[] Keys { get { return properties.Select(n => n.Name).ToArray(); } }

        /// <summary>
        /// Gets a value indicating whether a specific key is present in this basicKeyValueProvider instance
        /// </summary>
        /// <param name="key">the Key for which to check</param>
        /// <returns>a value indicating whether the specified key exists in this provider</returns>
        public bool ContainsKey(string key)
        {
            return properties.Any(n => n.Name == key);
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
            try
            {
                if (!events.ContainsKey(binder.Name))
                {
                    result = this[binder.Name];
                }
                else
                {
                    result = GetEvent(binder.Name);
                }
            }
            catch (Exception ex)
            {
                result = null;
                LogEnvironment.LogDebugEvent(ex.ToString(), LogSeverity.Error);
                throw;
            }

            return true;
        }

        /// <summary>
        /// Stellt die Implementierung für Vorgänge bereit, die einen Wert nach Index abrufen.Von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Indexvorgänge anzugeben.
        /// </summary>
        /// <returns>
        /// true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine Laufzeitausnahme ausgelöst.)
        /// </returns>
        /// <param name="binder">Stellt Informationen zum Vorgang bereit. </param><param name="indexes">Die Indizes, die bei dem Vorgang verwendet werden.Beim sampleObject[3]-Vorgang in C# (sampleObject(3) in Visual Basic), bei dem sampleObject von der DynamicObject-Klasse abgeleitet wird, entspricht <paramref name="indexes[0]"/> z. B. 3.</param><param name="result">Das Ergebnis des Indexvorgangs.</param>
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            try
            {
                result = client.GetProperty(objectName, "", indexes);
            }
            catch (Exception ex)
            {
                result = null;
                LogEnvironment.LogDebugEvent(ex.ToString(), LogSeverity.Error);
                throw;
            }

            return true;
        }

        /// <summary>
        /// Stellt die Implementierung für Vorgänge bereit, die einen Wert nach Index festlegen.Von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge anzugeben, die auf Objekte mit einem angegebenen Index zugreifen.
        /// </summary>
        /// <returns>
        /// true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)
        /// </returns>
        /// <param name="binder">Stellt Informationen zum Vorgang bereit. </param><param name="indexes">Die Indizes, die bei dem Vorgang verwendet werden.Beim sampleObject[3] = 10-Vorgang in C# (sampleObject(3) = 10 in Visual Basic), bei dem sampleObject von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitet wird, entspricht <paramref name="indexes[0]"/> z. B. 3.</param><param name="value">Der Wert, der auf das Objekt mit dem angegebenen Index festgelegt werden soll.Beim sampleObject[3] = 10-Vorgang in C# (sampleObject(3) = 10 in Visual Basic), bei dem sampleObject von der <see cref="T:System.Dynamic.DynamicObject"/>-Klasse abgeleitet wird, entspricht <paramref name="value"/> z. B. 10.</param>
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            try
            {
                client.SetProperty(objectName, "", indexes, value);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent(ex.ToString(), LogSeverity.Error);
                throw;
            }

            return true;
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
            try
            {
                if (!events.ContainsKey(binder.Name))
                {
                    client.SetProperty(objectName, binder.Name, emptyIndex, value);
                }
                else
                {
                    SetEvent(binder.Name, (Delegate) value);
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent(ex.ToString(), LogSeverity.Error);
                throw;
            }

            return true;
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
            if (binder.Name == "Dispose")
            {
                LogEnvironment.LogEvent("Do not Dispose Proxies!", LogSeverity.Error);
                result = null;
                return true;
            }

            try
            {
                Delegate callback = null;
                object[] parameters;
                string methodName = binder.Name;
                var method = FindMethod(binder.Name, args);
                parameters = (object[]) args.Clone();
                for (int i = 0; i < parameters.Length; i++)
                {
                    Debug.WriteLine($"Cloning parameter {i}");
                    parameters[i] = new TypedParam(parameters[i]) {NullType = method.MethodArgs[i]};
                    Debug.WriteLine($"Cloning parameter {i} -> done");
                }

                if (!method.IsAsync)
                {
                    try
                    {
                        object tmpVal = client.CallRemoteMethod(objectName, methodName, parameters);
                        if (method.ReturnType != null && tmpVal != null && !method.ReturnType.IsInstanceOfType(tmpVal))
                        {
                            tmpVal= TypeConverter.Convert(tmpVal, method.ReturnType);
                        }
                        Debug.WriteLine($"calling done...");
                        Debug.WriteLine($"analysing results...");
                        result = tmpVal;
                        Debug.WriteLine($"Result: {result}");
                    }
                    catch (Exception ex)
                    {
                        throw new InterProcessException(
                                string.Format("Remote call of method {0} has failed!", methodName), ex);
                    }

                    Debug.WriteLine($"writing back arguments");
                    WriteBackValues(parameters, args);
                    Debug.WriteLine($"writeback done");
                }
                else
                {
                    var dlg = MakeAsyncCall(method);
                    var tmpResult = dlg.DynamicInvoke(objectName, method.Name, parameters, args);
                    result = tmpResult;
                }
            }
            catch (InterProcessException ex)
            {
                Debug.WriteLine($"got an error");
                Debug.WriteLine(ex.OutlineException());
                result = null;
                LogEnvironment.LogDebugEvent(ex.InnerException?.Message, LogSeverity.Error);
                LogEnvironment.LogDebugEvent(ex.ServerException?.ToString(), LogSeverity.Error);
                throw;
            }
            catch (Exception ex)
            {
                result = null;
                LogEnvironment.LogDebugEvent(ex.ToString(), LogSeverity.Error);
                throw;
            }

            return true;
        }

        /// <summary>
        /// Enables a child object to add a specific event-subscription
        /// </summary>
        /// <param name="eventName">the name of the target-event</param>
        /// <param name="target">the delegate to add to the subscription</param>
        protected virtual void AddEventSubscription(string eventName, Delegate target)
        {
        }

        /// <summary>
        /// Enables a child object to remove a specific event-subscription
        /// </summary>
        /// <param name="eventName">the name of the target-event</param>
        /// <param name="target">the deletage to remove from the subscription</param>
        protected virtual void RemoveEventSubscription(string eventName, Delegate target)
        {
        }

        /// <summary>
        /// Gets the current value of an event subscription
        /// </summary>
        /// <param name="eventName">the name of the requested event</param>
        private Delegate GetEvent(string eventName)
        {
            return events[eventName];
        }

        /// <summary>
        /// Writes back the provided paraemters to the array provided by the caller
        /// </summary>
        /// <param name="source">the parameter-list that was returned by the service</param>
        /// <param name="dest">the parameter-list that was provided by the caller</param>
        private void WriteBackValues(object[] source, object[] dest)
        {
            Array.Copy(source, dest, source.Length);
        }

        private Delegate MakeAsyncCall(MethodDefinition definition)
        {
            if (definition.TaskType == typeof(Task))
            {
                return MakeAsyncVoidCall();
            }
            else if (definition.TaskType.IsGenericType && definition.TaskType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type t = definition.TaskType.GetGenericArguments().First();
                MethodInfo mif = typeCalls.GetOrAdd(t, tp => makeAsyncFunctionCallInfo.MakeGenericMethod(tp));
                return (Delegate) mif.Invoke(this, null);
            }
            else
            {
                throw new InvalidOperationException($"Unable to construct an Async call for type {definition.TaskType}");
            }
        }

        private Func<string, string, object[], object[], Task> MakeAsyncVoidCall()
        {
            return async (string obj, string method, object[] arg, object[] oriParam) =>
            {
                try
                {
                    await client.CallRemoteMethodAsync(obj, method, arg);
                    WriteBackValues(arg, oriParam);
                }
                catch (Exception ex)
                {
                    throw new InterProcessException(
                        string.Format("Remote call of method {0} has failed!", method), ex);
                }
            };
        }

        private Func<string, string, object[], object[], Task<T>> MakeAsyncFunctionCall<T>()
        {
            return async (string obj, string method, object[] arg, object[] oriParam) =>
            {
                try
                {
                    var retVal = await client.CallRemoteMethodAsync(obj, method, arg);
                    WriteBackValues(arg, oriParam);
                    return (T) retVal;
                }
                catch (Exception ex)
                {
                    throw new InterProcessException(
                        string.Format("Remote call of method {0} has failed!", method), ex);
                }
            };
        }

        /// <summary>
        /// Sets the current value of an event
        /// </summary>
        /// <param name="eventName">the event-name</param>
        /// <param name="target">the new value of the subscription</param>
        private void SetEvent(string eventName, Delegate target)
        {
            Delegate tmp = events[eventName];
            Delegate[] allOldTargets = (tmp != null) ? tmp.GetInvocationList() : new Delegate[0];
            Delegate[] allNewTargets = (target != null) ? target.GetInvocationList() : new Delegate[0];
            bool add = allOldTargets.Length < allNewTargets.Length;
            Delegate diff = add
                                ? (from a in allNewTargets
                                   join b in allOldTargets on a equals b into c
                                   from d in c.DefaultIfEmpty()
                                   where d == null
                                   select a).FirstOrDefault()
                                : (from a in allOldTargets
                                   join b in allNewTargets on a equals b into c
                                   from d in c.DefaultIfEmpty()
                                   where d == null
                                   select a).FirstOrDefault();
            events[eventName] = target;
            if (diff != null)
            {
                if (add)
                {
                    AddEventSubscription(eventName, diff);
                }
                else
                {
                    RemoveEventSubscription(eventName, diff);
                }
            }
        }

        /// <summary>
        /// Indicates whether the target method is async
        /// </summary>
        /// <param name="methodName">the taret method that was invoked over the interface</param>
        /// <param name="invokeMethod">the target method to invoke on the target service</param>
        /// <param name="customTimeout">provides a custom timeout that was decorated on the interface method to hint the objectproxy that this is a long-running process</param>
        /// <returns>a value indicating whether the method must be called with a callback at the end</returns>
        private MethodDefinition FindMethod(string methodName, object[] arguments)
        {
            MethodDefinition invokeMethod = null;
            if (methods.ContainsKey(methodName))
            {
                var tmp = (from t in methods[methodName]
                    where
                    t.MethodArgs.Length == arguments.Length
                    select t).ToArray();
                if (tmp.Length > 1)
                {
                    tmp = (from t in tmp where ArgsFit(arguments, t.MethodArgs) select t).ToArray();
                }

                if (tmp.Length == 0)
                {
                    throw new Exception("Unable to find the requested Method overload!");
                }

                invokeMethod = tmp[0];
            }

            return invokeMethod;
        }

        /// <summary>
        /// Checks the provided arguments against the expected parameters of the target method
        /// </summary>
        /// <param name="arguments">the arguments that were provided to the method</param>
        /// <param name="methodArgs">the argument-types that are expected by the current method</param>
        /// <param name="asyncMethod">indicates whether the target method is an asynchrounous method</param>
        /// <returns>a value indicating whether the provided arguments fit the requirements</returns>
        private bool ArgsFit(object[] arguments, Type[] methodArgs)
        {
            bool retVal = true;
            for (int i = 0; i < arguments.Length && retVal; i++)
            {
                Type t = arguments[i]?.GetType();
                if (t != null)
                {
                    retVal &= (t == methodArgs[i] || (methodArgs[i].IsByRef && t == methodArgs[i].GetElementType()));
                }
            }

            return retVal;
        }

        /// <summary>
        /// Analyzes the target interface for events and async methods
        /// </summary>
        /// <param name="expectedServiceType">the type that is implemented by this objectproxy instance</param>
        private void AnalyzeServiceType(Type expectedServiceType)
        {
            MemberInfo[] allMembers = expectedServiceType.GetMembers(false);
            LogEnvironment.LogDebugEvent($"Analyzing Target-Interface Type: {expectedServiceType.FullName}",LogSeverity.Report);
            foreach (MemberInfo member in allMembers)
            {
                if (member is MethodInfo)
                {
                    MethodDefinition method = new MethodDefinition{IsAsync=false};
                    ParameterInfo[] args = (member as MethodInfo).GetParameters();
                    Type retType = (member as MethodInfo).ReturnType;
                    method.Name = member.Name;
                    method.MethodArgs = new Type[args.Length];
                    if (TaskType.IsAssignableFrom(retType))
                    {
                        method.TaskType = retType;
                        method.IsAsync = true;
                        AutoAsyncAttribute aaa;
                        if ((aaa = Attribute.GetCustomAttribute(member, typeof(AutoAsyncAttribute)) as AutoAsyncAttribute) != null)
                        {
                            method.Name = aaa.SyncMethodName;
                        }
                    }
                    else
                    {
                        method.ReturnType = retType;
                    }

                    for (int i = 0; i < method.MethodArgs.Length; i++)
                    {
                        method.MethodArgs[i] = args[i].ParameterType;
                    }
                    if (!methods.ContainsKey(member.Name))
                    {
                        methods.Add(member.Name, new List<MethodDefinition>());
                    }

                    methods[member.Name].Add(method);
                }
                else if (member is EventInfo)
                {
                    events.Add(member.Name, null);
                }
                else if (member is PropertyInfo pi)
                {
                    if (pi.GetIndexParameters().Length == 0)
                    {
                        properties.Add(new PropertyDefinition
                        {
                            Name = member.Name, 
                            PropertyType = pi.PropertyType
                        });
                    }
                }
            }

            LogEnvironment.LogDebugEvent($"Found {methods.Count} methods, {properties.Count} properties and {events.Count} events.", LogSeverity.Report);
        }

        private class PropertyDefinition
        {
            /// <summary>
            /// Gets or sets the name of the real property that will be read or written
            /// </summary>
            public string Name { get; set; }
            
            /// <summary>
            /// Gets or sets the expected return type of this method
            /// </summary>
            public Type PropertyType{ get; set; }
        }

        private class MethodDefinition
        {
            /// <summary>
            /// Gets or sets the name of the real method that will be called
            /// </summary>
            public string Name { get; set; }

           /// <summary>
            /// Gets or sets a value indicating whether the underlaying method is async
            /// </summary>
            public bool IsAsync { get; set; }

           /// <summary>
           /// Gets or sets the expected Task-Type that is returned
           /// </summary>
           public Type TaskType { get;set; }

            /// <summary>
            /// Gets or sets the arguments that are required by the called method
            /// </summary>
            public Type[] MethodArgs { get; set; }

            /// <summary>
            /// Gets or sets the expected return type of this method
            /// </summary>
            public Type ReturnType { get; set; }
        }
    }
}
