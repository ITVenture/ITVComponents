//#define UseDelegates

using System;
using System.Linq;
using System.Reflection;
using ITVComponents.Scripting.CScript.Buffering;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Invokation;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.ScriptValues
{
    public abstract class ScriptValue: IDisposable
    {
        /// <summary>
        /// The Creator symbol that leads to this ScriptValue
        /// </summary>
        private IScriptSymbol creator;

        /// <summary>
        /// Indicates whether to bypass compatibilityChecks when lazyinvokation is active
        /// </summary>
        private bool bypassCompatibilityOnLazyInvokation;

        /// <summary>
        /// Gets a value indicating whether this ScriptValue is Writable
        /// </summary>
        public abstract bool Writable { get; }

        /// <summary>
        /// Gets a value indicating whether this ScriptValue is Getable
        /// </summary>
        public abstract bool Getable { get; }

        /// <summary>
        /// Gets the Value of this ScriptValue
        /// </summary>
        protected abstract object Value { get; }

        /// <summary>
        /// Gets the explicit type for accessing members through this value
        /// </summary>
        protected virtual Type ExplicitType
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the Value Type of this ScriptValue
        /// </summary>
        public abstract ValueType ValueType { get; set; }

        /// <summary>
        /// The Name of the Target object. This is only required for Methods
        /// </summary>
        protected abstract string Name { get; }

        /// <summary>
        /// Initializes a new instance of the ScriptValue class
        /// </summary>
        public ScriptValue(IScriptSymbol creator, bool bypassCompatibilityOnLazyInvokation)
        {
            //GarbageControl.CurrentCrypt.AddValue(this);
            this.creator = creator;
            this.bypassCompatibilityOnLazyInvokation = bypassCompatibilityOnLazyInvokation;
        }

        /// <summary>
        /// Gets the effective Value of this ScriptValue instance
        /// </summary>
        /// <param name="arguments">indexer/method/constructor arguments</param>
        /// <returns>an object that represents the value of this ScriptValue</returns>
        public virtual object GetValue(ScriptValue[] arguments, ScriptingPolicy policy)
        {
            if (!Getable)
            {
                throw new ScriptException("Get is not supported for this member");
            }

            if (creator != null)
            {
                bool ok;
                object retVal = creator.InvokeExecutor(Value, arguments, bypassCompatibilityOnLazyInvokation, out ok);
                if (ok)
                {
                    return retVal;
                }
            }

            if (ValueType == ValueType.Literal)
            {
                return Value;
            }

            object tmpValue = Value;
            if (ValueType == ValueType.PropertyOrField)
            {
                if (arguments == null || arguments.Length == 0)
                {
                    if (tmpValue is InvokationHelper invokationHelper)
                    {
                        if (policy.IsDenied(policy.GlobalMethods))
                        {
                            throw new ScriptSecurityException("Execution of global methods is denied!");
                        }

                        bool ok;
                        object retVal = invokationHelper.Invoke(null, out ok);
                        if (ok)
                        {
                            return retVal;
                        }
                    }

                    if (tmpValue is FunctionLiteral { AutoInvokeEnabled: true } fx)
                    {
                        if (policy.IsDenied(policy.ScriptMethods))
                        {
                            throw new ScriptSecurityException("Execution of Scripted functions is denied!");
                        }

                        return fx.Invoke(null);
                    }

                    return tmpValue;
                }
                if (tmpValue == null)
                {
                    throw new ScriptException("Indexer Failed for NULL - Value");
                }

                if (tmpValue is Type)
                {
                    throw new ScriptException("Indexer call for Types not supported");
                }

                object[] parameters = (from t in arguments select t.GetValue(null, policy)).ToArray();
                if (!(tmpValue is Array))
                {
                    object[] args;
                    Type targetType = tmpValue.GetType();
                    if (ExplicitType != null)
                    {
                        if (!ExplicitType.IsAssignableFrom(targetType))
                        {
                            throw new ScriptException("Provided Type is not implemented by the target-object");
                        }

                        targetType = ExplicitType;
                    }
                    PropertyInfo pi = MethodHelper.GetCapableIndexer(targetType,
                                                                     parameters,
                                                                     out args);
                    if (pi == null)
                    {
                        throw new ScriptException("No capable Indexer found for the provided arguments");
                    }

                    if (policy.IsDenied(pi, tmpValue, PropertyAccessMode.Read, false))
                    {
                        throw new ScriptSecurityException($"Access to property '{pi.Name}' is denied!");
                    }

                    if (creator != null)
                    {
                        creator.SetPreferredExecutor(new LazyIndexer(pi,parameters.Length != args.Length, policy));
                    }

                    return pi.GetValue(tmpValue, args);
                }

                Array arr = (Array)tmpValue;
                return arr.GetValue(parameters.Cast<int>().ToArray());
            }

            if (ValueType == ValueType.Method)
            {
                SequenceValue ta = (SequenceValue) arguments[0];
                SequenceValue a = (SequenceValue) arguments[1];
                ScriptValue et = arguments[2];
                Type explicitType = null;
                object[] parameters = (from t in a.Sequence select t.GetValue(null, policy)).ToArray();
                Type[] typeParameters = Type.EmptyTypes;
                if (ta != null)
                {
                    typeParameters = (from t in ta.Sequence select (Type) t.GetValue(null, policy)).ToArray();
                }

                if (et != null)
                {
                    explicitType = et.GetValue(null, policy) as Type;
                }

                Type type;
                
                if (tmpValue == null)
                {
                    ValueType = ValueType.PropertyOrField;
                }

                if (Name == null)
                {
                    try
                    {
                        if (tmpValue is Delegate)
                        {
                            if (policy.IsDenied(policy.AllowDelegates))
                            {
                                throw new ScriptSecurityException("Execution of Delegates is denied!");
                            }

                            Delegate dlg = (Delegate) tmpValue;
                            return dlg.DynamicInvoke(parameters);
                        }

                        if (tmpValue is InvokationHelper)
                        {
                            if (policy.IsDenied(policy.GlobalMethods))
                            {
                                throw new ScriptSecurityException("Execution of global methods is denied!");
                            }

                            InvokationHelper ih = (InvokationHelper) tmpValue;
                            bool ok;
                            var retVal = ih.Invoke(parameters, out ok);
                            if (ok)
                            {
                                return retVal;
                            }

                            throw new ScriptException($"Failed to call method {Name}. Possible Arguments-mismatch.");
                        }

                        if (tmpValue is FunctionLiteral)
                        {
                            if (policy.IsDenied(policy.ScriptMethods))
                            {
                                throw new ScriptSecurityException("Execution of Scripted functions is denied!");
                            }

                            FunctionLiteral fl = (FunctionLiteral) tmpValue;
                            return fl.Invoke(parameters);
                        }
                    }
                    finally
                    {
                        ValueType = ValueType.Method;
                    }
                }

                if (tmpValue == null)
                {
                    throw new Exception("Method call failed for NULL - Value");
                }

                object target = tmpValue;
                bool isStatic = false;
                if (tmpValue is Type value)
                {
                    type = value;
                    target = null;
                    isStatic = true;
                }
                else if (tmpValue is ObjectLiteral ol)
                {
                    type = ol.GetType();
                    FunctionLiteral fl = ol[Name] as FunctionLiteral;
                    if (fl != null)
                    {
                        if (policy.IsDenied(policy.ScriptMethods))
                        {
                            throw new ScriptSecurityException("Execution of Scripted functions is denied!");
                        }

                        return fl.Invoke(parameters);
                    }
                }
                else
                {
                    type = explicitType??tmpValue.GetType();
                }

                object[] args;
#if UseDelegates
                MethodInvoker method = MethodHelper.GetCapableMethod(type, typeParameters, Name, Value is Type, parameters, out args);
#else
                bool tmpStatic = isStatic;
                bool isExtensionMethod = false;
                MethodInfo method = MethodHelper.GetCapableMethod(type, typeParameters, Name, ref isStatic, parameters,
                                                                  out args);

                if (!tmpStatic && isStatic)
                {
                    args[0] = target;
                    target = null;
                    isExtensionMethod = true;
                }
#endif
                if (method == null)
                {
                    throw new ScriptException(string.Format("No capable Method found for {0}", Name));
                }

                if (policy.IsDenied(method, !isExtensionMethod ? target : args[0], isStatic | isExtensionMethod))
                {
                    throw new ScriptSecurityException($"Access to method '{method.Name}' is denied!");
                }

                var writeBacks = MethodHelper.GetWritebacks(method, args, a.Sequence);
                if (creator != null)
                {
                    creator.SetPreferredExecutor(new LazyMethod(method, tmpStatic, !tmpStatic && isStatic, args.Length != a.Sequence.Length, policy));
                }
#if UseDelegates
                if (target != null)
                {
                    target = target.WrapIfValueType();
                }

                return method(target, args);
                if (target != null)
                {

                    /*object[] newArgs = new object[args.Length + 1];
                    newArgs[0] = target;
                    Array.Copy(args, 0, newArgs, 1, args.Length);*/
                    args[0] = target;
                    if (!(target is System.ValueType))
                    {
                        return method.FastDynamicInvoke(args);
                    }

                    return method.DynamicInvoke(args);
#else
                try
                {
                    return method.Invoke(target, args);
                }
                finally
                {
                    foreach (var wb in writeBacks)
                    {
                        wb.Target.SetValue(args[wb.Index]);
                    }
                }
#endif
#if UseDelegates
                }


                return method.FastDynamicInvoke(args);
#endif
            }

            if (ValueType == ValueType.Constructor)
            {
                if (tmpValue == null || !(tmpValue is Type))
                {
                    throw new ScriptException("Require Type in order to create a new instance");
                }

                ScriptValue[] ta = null;
                if (arguments[0] != null)
                {
                    ta = ((SequenceValue)arguments[0]).Sequence;
                }

                ScriptValue[] a = ((SequenceValue) arguments[1]).Sequence;
                object[] parameters = (from t in a select t.GetValue(null, policy)).ToArray();
                Type[] typeParameters = ta == null
                                            ? Type.EmptyTypes
                                            : (from t in ta select (Type) t.GetValue(null, policy)).ToArray();
                Type type = (Type)tmpValue;
                if (typeParameters.Length != 0)
                {
                    //throw new ScriptException(string.Format("Unexpected usage of generic Type {0}", ((Type)Value).FullName));
                    type = type.MakeGenericType(typeParameters);
                }

                object[] args;
                ConstructorInfo constructor = MethodHelper.GetCapableConstructor(type, parameters, out args);
                if (constructor == null)
                {
                    throw new ScriptException(string.Format("No appropriate Constructor was found for {0}",
                                                            ((Type)tmpValue).FullName));
                }

                if (policy.IsDenied(type, TypeAccessMode.Construct, policy.CreateNewInstances))
                {
                    throw new ScriptSecurityException($"Creating instances of type '{type.FullName}' is denied!");
                }

                if (creator != null)
                {
                    creator.SetPreferredExecutor(new LazyConstructor(constructor, args.Length != a.Length, policy));
                }

                return constructor.Invoke(args);
            }

            throw new ScriptException("Unexpected Value-Type");
        }

        public bool CanGetValue(ScriptValue[] arguments, ScriptingPolicy policy)
        {
            if (!Getable)
            {
                throw new ScriptException("Get is not supported for this member");
            }

            if (creator != null)
            {
                bool ok = creator.CanInvokeExecutor(Value,arguments,bypassCompatibilityOnLazyInvokation);
                //object retVal = creator.InvokeExecutor(Value, arguments, bypassCompatibilityOnLazyInvokation, out ok);
                if (ok)
                {
                    return true;
                }
            }

            if (ValueType == ValueType.Literal)
            {
                return false;
            }

            object tmpValue = Value;
            if (ValueType == ValueType.PropertyOrField)
            {
                if (arguments == null || arguments.Length == 0)
                {
                    return true;
                }
                if (tmpValue == null)
                {
                    return false;
                }

                if (tmpValue is Type)
                {
                    return false;
                }

                object[] parameters = (from t in arguments select t.GetValue(null, policy)).ToArray();
                if (!(tmpValue is Array))
                {
                    object[] args;
                    PropertyInfo pi = MethodHelper.GetCapableIndexer(ExplicitType??tmpValue.GetType(),
                                                                     parameters,
                                                                     out args);
                    return pi != null;
                }

                Array arr = (Array)tmpValue;
                int[] indices = parameters.Cast<int>().ToArray();
                bool retVal = true;
                for (int i = 0; i < indices.Length; i++)
                {
                    retVal &= arr.GetLength(i) > indices[i];
                }

                return retVal;
            }

            if (ValueType == ValueType.Method)
            {
                SequenceValue ta = (SequenceValue)arguments[0];
                SequenceValue a = (SequenceValue)arguments[1];
                ScriptValue et = arguments[2];
                object[] parameters = (from t in a.Sequence select t.GetValue(null, policy)).ToArray();
                Type[] typeParameters = Type.EmptyTypes;
                if (ta != null)
                {
                    typeParameters = (from t in ta.Sequence select (Type)t.GetValue(null, policy)).ToArray();
                }

                Type type;

                if (tmpValue == null)
                {
                    ValueType = ValueType.PropertyOrField;
                }

                if (Name == null)
                {
                    try
                    {
                        if (tmpValue is Delegate)
                        {
                            return true;
                        }

                        if (tmpValue is InvokationHelper)
                        {
                            return true;
                        }

                        if (tmpValue is FunctionLiteral)
                        {
                            return true;
                        }
                    }
                    finally
                    {
                        ValueType = ValueType.Method;
                    }
                }

                if (tmpValue == null)
                {
                    return false;
                }

                object target = tmpValue;
                bool isStatic = false;
                if (tmpValue is Type)
                {
                    type = (Type)tmpValue;
                    target = null;
                    isStatic = true;
                }
                else if (tmpValue is ObjectLiteral)
                {
                    type = tmpValue.GetType();
                    ObjectLiteral ol = tmpValue as ObjectLiteral;
                    FunctionLiteral fl = ol[Name] as FunctionLiteral;
                    if (fl != null)
                    {
                        return true;
                    }
                }
                else
                {
                    type = et?.GetValue(null, policy) as Type ?? tmpValue.GetType();
                }

                object[] args;
#if UseDelegates
                MethodInvoker method = MethodHelper.GetCapableMethod(type, typeParameters, Name, Value is Type, parameters, out args);
#else
                bool tmpStatic = isStatic;
                MethodInfo method = MethodHelper.GetCapableMethod(type, typeParameters, Name, ref isStatic, parameters,
                                                                  out args);
                if (!tmpStatic && isStatic)
                {
                    args[0] = target;
                    target = null;
                }
#endif
                if (method == null)
                {
                    return false;
                }

                return true;
            }

            if (ValueType == ValueType.Constructor)
            {
                if (tmpValue == null || !(tmpValue is Type))
                {
                    throw new ScriptException("Require Type in order to create a new instance");
                }

                ScriptValue[] ta = null;
                if (arguments[0] != null)
                {
                    ta = ((SequenceValue)arguments[0]).Sequence;
                }

                ScriptValue[] a = ((SequenceValue)arguments[1]).Sequence;
                object[] parameters = (from t in a select t.GetValue(null, policy)).ToArray();
                Type[] typeParameters = ta == null
                                            ? Type.EmptyTypes
                                            : (from t in ta select (Type)t.GetValue(null, policy)).ToArray();
                Type type = (Type)tmpValue;
                if (typeParameters.Length != 0)
                {
                    //throw new ScriptException(string.Format("Unexpected usage of generic Type {0}", ((Type)Value).FullName));
                    type = type.MakeGenericType(typeParameters);
                }

                object[] args;
                ConstructorInfo constructor = MethodHelper.GetCapableConstructor(type, parameters, out args);
                if (constructor == null)
                {
                    return false;
                }

                return true;
            }

            throw new ScriptException("Unexpected Value-Type");
        }

        /// <summary>
        /// Sets the Value of this ScriptValue object
        /// </summary>
        /// <param name="value">the Value to assign to this ScriptValue</param>
        /// <param name="arguments">the indexer arguments, if required</param>
        public virtual void SetValue(object value, ScriptValue[] arguments, ScriptingPolicy policy)
        {
            if (arguments == null || arguments.Length == 0)
            {
                if (!Writable)
                {
                    throw new ScriptException("Set is not supported for this Member");
                }

                SetValue(value);
            }
            else
            {
                object tmpValue = Value;
                if (tmpValue == null)
                {
                    throw new ScriptException("Indexer Failed for NULL - Value");
                }

                if (tmpValue is Type)
                {
                    throw new ScriptException("Indexer call for Types not supported");
                }

                object[] parameters = (from t in arguments select t.GetValue(null, policy)).ToArray();
                if (!(tmpValue is Array))
                {
                    object[] args;
                    PropertyInfo pi = MethodHelper.GetCapableIndexer(tmpValue.GetType(),
                                                                     parameters,
                                                                     out args);
                    if (pi == null)
                    {
                        throw new ScriptException("No capable Indexer found for the provided arguments");
                    }

                    if (!pi.CanWrite)
                    {
                        throw new ScriptException("Set is not supported for this Indexer");
                    }

                    if (policy.IsDenied(pi, tmpValue, PropertyAccessMode.Write, false))
                    {
                        throw new ScriptSecurityException(
                            $"Setting a value on indexer '{pi.Name}' of Type '{pi.DeclaringType.Name}' is denied");
                    }

                    pi.SetValue(tmpValue, value, args);
                }
                else
                {
                    Array arr = (Array)tmpValue;
                    arr.SetValue(value,parameters.Cast<int>().ToArray());
                }
            }
        }

        public virtual void Dispose()
        {
            creator = null;
        }

        /// <summary>
        /// Sets the Value of this ScriptValue object
        /// </summary>
        /// <param name="value">the new Value to assign to this Value</param>
        internal abstract void SetValue(object value);
    }

    /// <summary>
    /// Enum for the Supported Value Types in a Script
    /// </summary>
    public enum ValueType
    {
        /// <summary>
        /// The value is literal and can not be assigned
        /// </summary>
        Literal,

        /// <summary>
        /// The Value is a Property and can be read and written
        /// </summary>
        PropertyOrField,

        /// <summary>
        /// The Value is a Method and can only be read
        /// </summary>
        Method,

        /// <summary>
        /// The Value is a Constructor and can only be read
        /// </summary>
        Constructor
    }
}
