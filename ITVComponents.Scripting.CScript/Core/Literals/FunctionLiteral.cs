using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Core.Literals
{
    public class FunctionLiteral : DynamicObject
    {
        /// <summary>
        /// The Scope of this function
        /// </summary>
        private FunctionScope scope;

        /// <summary>
        /// The Script visitor that is used to interpret this method
        /// </summary>
        private ScriptVisitor visitor;

        /// <summary>
        /// The method names that are expected by this method
        /// </summary>
        private string[] arguments;

        /// <summary>
        /// The functionbody of this method
        /// </summary>
        private ITVScriptingParser.FunctionBodyContext body;

        private readonly ScriptingPolicy policy;

        /// <summary>
        /// The initialValues that are surrounding this functiondefinition
        /// </summary>
        private Dictionary<string, object> initialValues;

        /// <summary>
        /// Initializes a new instance of the FunctionLiteral class
        /// </summary>
        /// <param name="values">the local values that are surrounding the method at the moment of creation</param>
        /// <param name="parent">the parent scope of this method</param>
        /// <param name="arguments">the argument names that are passed to this method</param>
        /// <param name="body">the method body of this method</param>
        public FunctionLiteral(Dictionary<string, object> values, string[] arguments,
            ITVScriptingParser.FunctionBodyContext body, ScriptingPolicy policy)
        {
            initialValues = values;
            scope = new FunctionScope(values, policy);
            visitor = new ScriptVisitor(scope);
            visitor.ScriptingPolicy = policy;
            this.arguments = arguments;
            this.body = body;
            this.policy = policy;
        }

        public IScope ParentScope { get { return scope.ParentScope; } set { scope.ParentScope = value; } }

        /// <summary>
        /// Gets a value indicating whether autoInvoke for this method is currently active
        /// </summary>
        internal bool AutoInvokeEnabled { get; private set; }

        /// <summary>
        /// Enables AutoInvokation on this functionliteral
        /// </summary>
        /// <returns>a value that enables the caller to disable autoinvokation on this literal</returns>
        public IDisposable EnableAutoInvoke()
        {
            if ((arguments?.Length ?? 0) == 0)
            {
                IDisposable retVal = new AutoInvokeLockHandle(this);
                AutoInvokeEnabled = true;
                return retVal;
            }

            throw new InvalidOperationException("Unable to enable AutoInvoke on a method with parameters");
        }

        public void ClearInitialValues()
        {
            scope.ClearInitial();
        }

        /// <summary>Stellt die Implementierung für Typkonvertierungsvorgänge bereit.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Operationen anzugeben, die ein Objekt von einem Typ in einen anderen konvertieren.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zur Konvertierungsoperation bereit.Die binder.Type-Eigenschaft stellt den Typ bereit, in den das Objekt konvertiert werden muss.Für die Anweisung (String)sampleObject in C# (CType(sampleObject, Type) in Visual Basic), bei der sampleObject eine Instanz der von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleiteten Klasse ist, gibt binder.Type z. B. den <see cref="T:System.String" />-Typ zurück.Die binder.Explicit-Eigenschaft stellt Informationen zur Art der ausgeführten Konvertierung bereit.Für die explizite Konvertierung wird true und für die implizite Konvertierung wird false zurückgegeben.</param>
        /// <param name="result">Das Ergebnis des Typkonvertierungsvorgangs.</param>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (!binder.Type.IsSubclassOf(typeof(Delegate)))
            {
                result = null;
                return false;
            }

            result = CreateDelegate(binder.Type);
            return true;
        }

        /// <summary>
        /// Sets the initial Value of this function Scope
        /// </summary>
        /// <param name="name">the name to set on this function</param>
        /// <param name="value">the value to save in that value</param>
        public void SetInitialScopeValue(string name, object value)
        {
            scope.SetBaseValue(name, value);
        }


        /// <summary>
        /// Gets the initial Value of this function Scope
        /// </summary>
        /// <param name="name">the name to get from this function</param>
        public object GetInitialScopeValue(string name)
        {
            bool declared;
            return scope.GetBaseValue(name, out declared);
        }

        /// <summary>Stellt die Implementierung für Vorgänge bereit, die ein Objekt aufrufen.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Aufrufen eines Objekts oder Delegaten anzugeben.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zum Aufrufvorgang bereit.</param>
        /// <param name="args">Die Argumente, die während des Aufrufvorgangs an das Objekt übergeben werden.Für den sampleObject(100)-Vorgang, in dem sampleObject von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitet ist, entspricht <paramref name="args[0]" /> z. B. 100.</param>
        /// <param name="result">Das Ergebnis des Objektaufrufs.</param>
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            result = Invoke(args);
            return true;
        }

        /// <summary>Stellt die Implementierung für Vorgänge bereit, die Memberwerte festlegen.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Festlegen eines Werts für eine Eigenschaft anzugeben.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine sprachspezifische Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zum Objekt bereit, das den dynamischen Vorgang aufgerufen hat.Die binder.Name-Eigenschaft gibt den Namen des Members an, dem der Wert zugewiesen wird.Für die Anweisung sampleObject.SampleProperty = "Test", in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleProperty" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param>
        /// <param name="value">Der Wert, der auf den Member festgelegt werden soll.Für die sampleObject.SampleProperty = "Test"-Anweisung, in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Instanz der Klasse ist, ist <paramref name="value" /> z. B. "Test".</param>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            scope.SetBaseValue(binder.Name, value);
            return true;
        }

        /// <summary>Stellt die Implementierung für Vorgänge bereit, die Memberwerte abrufen.Von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Klassen können diese Methode überschreiben, um dynamisches Verhalten für Vorgänge wie das Abrufen eines Werts für eine Eigenschaft anzugeben.</summary>
        /// <returns>true, wenn der Vorgang erfolgreich ist, andernfalls false.Wenn die Methode false zurückgibt, wird das Verhalten vom Laufzeitbinder der Sprache bestimmt.(In den meisten Fällen wird eine Laufzeitausnahme ausgelöst.)</returns>
        /// <param name="binder">Stellt Informationen zum Objekt bereit, das den dynamischen Vorgang aufgerufen hat.Die binder.Name-Eigenschaft gibt den Namen des Members an, für den der dynamische Vorgang ausgeführt wird.Für die Console.WriteLine(sampleObject.SampleProperty)-Anweisung, in der sampleObject eine von der <see cref="T:System.Dynamic.DynamicObject" />-Klasse abgeleitete Instanz der Klasse ist, gibt binder.Name z. B. "SampleProperty" zurück.Die binder.IgnoreCase-Eigenschaft gibt an, ob der Membername die Groß-/Kleinschreibung berücksichtigt.</param>
        /// <param name="result">Das Ergebnis des get-Vorgangs.Wenn die Methode z. B. für eine Eigenschaft aufgerufen wird, können Sie <paramref name="result" /> den Eigenschaftswert zuweisen.</param>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            bool retVal;
            result = scope.GetBaseValue(binder.Name, out retVal);
            return retVal;
        }

        /// <summary>
        /// Invokes the body of this function using the provided arguments
        /// </summary>
        /// <param name="arguments">the arguments whith which to initialize the visitor for this method</param>
        /// <returns>the result of this method</returns>
        public object Invoke(object[] arguments)
        {
            var dct = scope.PrepareCall();
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                for (int i = 0; i < this.arguments.Length; i++)
                {
                    object val = i < (arguments?.Length ?? 0) ? arguments[i] : null;
                    parameters[this.arguments[i]] = val;
                }

                parameters["parameters"] = arguments;
                scope.Clear(parameters);
                return ScriptValueHelper.GetScriptValueResult<object>(visitor.Visit(body), false, policy);
            }
            finally
            {
                scope.FinalizeScope(dct);
            }
        }

        /// <summary>
        /// Creates a copy with a new scope of this functionLiteral
        /// </summary>
        /// <returns></returns>
        public FunctionLiteral Copy()
        {
            return new FunctionLiteral(initialValues, arguments, body, policy);
        }

        /// <summary>
        /// Creates an eventhandler for the specified event info
        /// </summary>
        /// <param name="delegateType">the event information for the subscribed event</param>
        /// <returns>a delegate that raises a generic event providing all required information used to distribute the event to clients</returns>
        public Delegate CreateDelegate(Type delegateType)
        {
            Funk d = Invoke;
            Type method = delegateType;
            MethodInfo minf = method.GetMethod("Invoke");
            ParameterInfo[] methodParameters = minf.GetParameters();
            List<string> names = new List<string>();
            int a = 0;
            ParameterExpression[] parameters =
                methodParameters.Select(n => Expression.Parameter(n.ParameterType, string.Format("arg{0}", a++)))
                    .ToArray();
            names.AddRange(parameters.Select(n => n.Name));
            NewArrayExpression array = Expression.NewArrayInit(typeof(object), parameters.Select(n => Expression.Convert(n, typeof(object))));
            List<Expression> xps = new List<Expression>();
            var callExp = Expression.Call(Expression.Constant(d), d.GetType().GetMethod("Invoke"), array);
            //xps.Add(lambda);
            if (minf.ReturnType == typeof(void))
            {
                xps.Add(callExp);
                xps.Add(Expression.Empty());
            }
            else
            {
                xps.Add(Expression.Convert(callExp, minf.ReturnType));
            }

            BlockExpression block = Expression.Block(xps);
            LambdaExpression lambda =
                Expression.Lambda(block, parameters);
            Delegate tmp = lambda.Compile();
            return Delegate.CreateDelegate(method, tmp, "Invoke", false);
        }

        /// <summary>
        /// a temporary delegate type that is used as bridge between an eventhandler and the invoke method
        /// </summary>
        /// <param name="arguments">the arguments to pass to the Invoke method</param>
        /// <returns>the result of this function</returns>
        private delegate object Funk(object[] arguments);

        /// <summary>
        /// allows a scripting session to end autoInvokation for a functionLiteral
        /// </summary>
        private class AutoInvokeLockHandle : IDisposable
        {
            /// <summary>
            /// the function literal for which to disable autoInvoke on disposal
            /// </summary>
            private FunctionLiteral parent;

            /// <summary>
            /// Initializes a new instance of the AutoInvokeLockHandle class
            /// </summary>
            /// <param name="parent"></param>
            public AutoInvokeLockHandle(FunctionLiteral parent)
            {
                this.parent = parent;
            }

            /// <summary>Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.</summary>
            /// <filterpriority>2</filterpriority>
            public void Dispose()
            {
                parent.AutoInvokeEnabled = false;
            }
        }
    }
}
