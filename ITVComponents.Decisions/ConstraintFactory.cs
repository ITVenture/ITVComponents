using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Decisions.FactoryHelpers;
using ITVComponents.Plugins;

namespace ITVComponents.Decisions
{
    public class ConstraintFactory:IConstraintFactory
    {
        /// <summary>
        /// The PluginFactory that is used request other plugins 
        /// </summary>
        private readonly IPluginFactory factory;

        /// <summary>
        /// Holds a list of registered constructors
        /// </summary>
        private readonly  List<ConstraintConstructor> registeredConstructors = new List<ConstraintConstructor>();

        /// <summary>
        /// Holds all Constructors that have alredy been used
        /// </summary>
        private readonly Dictionary<string, ConstructorBuffer> bufferedConstructors  = new Dictionary<string, ConstructorBuffer>();

        /// <summary>
        /// Initializes a new instance of the ConstraintFactory class
        /// </summary>
        /// <param name="factory">the pluginfactory that is used to request other plugins that are used for Constraint construction</param>
        public ConstraintFactory(IPluginFactory factory)
        {
            this.factory = factory;
        }

        /// <summary>
        /// Gets the PluginFactory that is attached to this ConstraintFactory
        /// </summary>
        protected IPluginFactory Factory => factory;

        /// <summary>
        /// Registers a constructor in this factory
        /// </summary>
        /// <param name="constructor">te provided constructor for a specific constraint type</param>
        public void RegisterType(ConstraintConstructor constructor)
        {
            lock (bufferedConstructors)
            {
                lock (registeredConstructors)
                {
                    ConstraintConstructor cst =
                        registeredConstructors.FirstOrDefault(n => n.Identifier == constructor.Identifier);
                    if (cst != null)
                    {
                        registeredConstructors.Remove(cst);
                        if (bufferedConstructors.ContainsKey(constructor.Identifier))
                        {
                            bufferedConstructors.Remove(constructor.Identifier);
                        }
                    }

                    registeredConstructors.Add(constructor);
                }
            }
        }

        /// <summary>
        /// Gets a Constraint with the given name taking the provided named parameters
        /// </summary>
        /// <typeparam name="T">the Type that is processed by the created Constraint</typeparam>
        /// <param name="identifier">the Constructor identifier</param>
        /// <param name="namedParameters">the named Parameters for the constructor</param>
        /// <returns>an instance of the provided Constraint</returns>
        public IConstraint<T> GetConstraint<T>(string identifier, Dictionary<string, object> namedParameters) where T:class
        {
            return GetConstraint<T>(identifier, (s,t) => namedParameters[s]);
        }

        /// <summary>
        /// Gets a Constraint with the given name taking the provided named parameters
        /// </summary>
        /// <param name="targetType">the Type that is processed by the created Constraint</param>
        /// <param name="identifier">the Constructor identifier</param>
        /// <param name="namedParameters">the named Parameters for the constructor</param>
        /// <returns>an instance of the provided Constraint</returns>
        public IConstraint GetConstraint(Type targetType, string identifier, Dictionary<string, object> namedParameters)
        {
            return GetConstraint(targetType, identifier, (s, t) => namedParameters[s]);
        }

        /// <summary>
        /// Gets a constraint with the given name.
        /// </summary>
        /// <typeparam name="T">the type that is processed by the created constraint</typeparam>
        /// <param name="identifier">the identifier of the given constraint-type</param>
        /// <param name="valueCallback">a callback that is used to request unknown parameters</param>
        /// <returns>an instance of the provided constraint</returns>
        public IConstraint<T> GetConstraint<T>(string identifier, Func<string, Type, object> valueCallback) where T:class
        {
            return (IConstraint<T>)GetConstraint(typeof(T), identifier, valueCallback);
        }

        /// <summary>
        /// Gets a constraint with the given name.
        /// </summary>
        /// <param name="targetType">the type that is processed by the created constraint</param>
        /// <param name="identifier">the identifier of the given constraint-type</param>
        /// <param name="valueCallback">a callback that is used to request unknown parameters</param>
        /// <returns>an instance of the provided constraint</returns>
        public IConstraint GetConstraint(Type targetType, string identifier, Func<string, Type, object> valueCallback)
        {
            ConstructorBuffer buffer;
            lock (bufferedConstructors)
            {
                if (!bufferedConstructors.ContainsKey(identifier))
                {
                    ConstraintConstructor constructor;
                    lock (registeredConstructors)
                    {
                        constructor =
                            registeredConstructors.FirstOrDefault(n => n.Identifier == identifier);
                    }

                    if (constructor == null)
                    {
                        throw new ArgumentException("The provided Identifier is not registered in this factory",
                            nameof(identifier));
                    }

                    ConstructorBuffer tmp = new ConstructorBuffer();
                    ConstructorInfo constructorInfo;
                    tmp.Parameters = constructor.GetArguments(targetType ,out constructorInfo);
                    tmp.ConstructorInfo = constructorInfo;
                    bufferedConstructors.Add(identifier, tmp);
                }

                buffer = bufferedConstructors[identifier];
            }
            ParameterInfo[] args = buffer.ConstructorInfo.GetParameters();
            object[] arguments = new object[buffer.Parameters.Length];
            for (int i = 0; i < buffer.Parameters.Length; i++)
            {
                object tmp;
                if (!(buffer.Parameters[i] is Ask))
                {
                    tmp = buffer.Parameters[i];
                }
                else
                {
                    tmp = valueCallback(args[i].Name, args[i].ParameterType);
                }

                string pival = tmp as string;
                if (pival != null)
                {
                    if (pival.StartsWith("$") && !pival.StartsWith("$$"))
                    {
                        pival = pival.Substring(1);
                        tmp = factory[pival];
                        /*if (tmp == null)
                        {
                            tmp = factory.GetRegisteredObject(pival);
                        }*/
                    }
                }

                arguments[i] = tmp;
            }

            return (IConstraint)buffer.ConstructorInfo.Invoke(arguments);
        }

        public IDecider CreateDecider(Type targetType, bool contextDriven)
        {
            Type baseType = typeof(SimpleDecider<>);
            Type implType = baseType.MakeGenericType(targetType);
            return (IDecider) implType.GetConstructor(new Type[] {typeof(bool)}).Invoke(new object[] {contextDriven});
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

        private class ConstructorBuffer
        {
            public ConstructorInfo ConstructorInfo { get; set; }

            public object[] Parameters { get; set; }
        }
    }
}
