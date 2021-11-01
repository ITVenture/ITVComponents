using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class MemberAccessEvaluator:EvaluatorBase, IAssignableEvaluator
    {
        private readonly EvaluatorBase baseValue;
        private readonly EvaluatorBase explicitTypeValue;
        private bool weakAccess;
        private string name;
        private bool whatIf;
        private ResultType expectedResult = ResultType.PropertyOrField;

        public MemberAccessEvaluator(EvaluatorBase baseValue, EvaluatorBase explicitTypeValue, ITVScriptingParser.MemberDotExpressionContext identifier):base(null,null,BuildArguments(baseValue, explicitTypeValue),identifier,null,null)
        {
            this.baseValue = baseValue;
            this.explicitTypeValue = explicitTypeValue;
            weakAccess = false;
            name = identifier.identifierName().GetText();
        }

        public MemberAccessEvaluator(EvaluatorBase baseValue, EvaluatorBase explicitTypeValue, ITVScriptingParser.MemberDotQExpressionContext identifier) : base(null, null, BuildArguments(baseValue, explicitTypeValue), identifier, null, null)
        {
            this.baseValue = baseValue;
            this.explicitTypeValue = explicitTypeValue;
            weakAccess = true;
            name = identifier.identifierName().GetText();
        }

        public MemberAccessEvaluator(EvaluatorBase baseValue, EvaluatorBase explicitTypeValue,
            ITVScriptingParser.HasMemberExpressionContext identifier) : base(null, null,
            BuildArguments(baseValue, explicitTypeValue), identifier, null, null)
        {
            this.baseValue = baseValue;
            this.explicitTypeValue = explicitTypeValue;
            weakAccess = true;
            name = identifier.identifierName().GetText();
            whatIf = true;
        }

        public override ResultType ExpectedResult
        {
            get
            {
                return expectedResult;
            }
            internal set
            {
                if (ExpectedResult == ResultType.Literal)
                {
                    throw new InvalidOperationException("This Evaluator does not support literals!");
                }

                expectedResult = value;
            }
        }
        public override AccessMode AccessMode { get; internal set; }
        public override bool PutValueOnStack { get; } = true;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var baseVal = arguments[0];
            Type t = null;
            if (explicitTypeValue != null)
            {
                t = (Type)arguments[1];
            }

            if ((AccessMode & AccessMode.Read) == AccessMode.Read)
            {
                object retVal = null;
                if (baseVal == null && weakAccess)
                {
                    if (ExpectedResult == ResultType.PropertyOrField)
                    {
                        return null;
                    }
                }

                retVal = GetMemberValue(baseVal, t);
                if (ExpectedResult == ResultType.Method)
                {
                    return new ActiveCodeAccessDescriptor { Name = name, WeakAccess = true, BaseObject = retVal };
                }

                if (ExpectedResult == ResultType.Constructor)
                {
                    return new ActiveCodeAccessDescriptor { BaseObject = retVal };
                }

                if (ExpectedResult == ResultType.PropertyOrField)
                {
                    if (AccessMode == AccessMode.Read)
                    {
                        return retVal;
                    }

                    if (!whatIf)
                    {
                        return new object[]
                        {
                            retVal,
                            new ActiveCodeAccessDescriptor { BaseObject = baseVal, ExplicitType = t, Name = name }
                        };
                    }

                    throw new ScriptException("Write is not supported in combination with the 'has' keyword.");
                }

                throw new ScriptException($"Result-Type {ExpectedResult} is not supported!");

            }

            return new ActiveCodeAccessDescriptor { BaseObject = baseVal, ExplicitType = t, Name = name };
        }

        private static List<EvaluatorBase> BuildArguments(EvaluatorBase baseValue, EvaluatorBase explicitTypeValue)
        {
            var retVal = new List<EvaluatorBase>();
            retVal.Add(baseValue);
            if (explicitTypeValue != null)
            {
                retVal.Add(explicitTypeValue);
            }

            return retVal;
        }

        private object GetMemberValue(object target, Type explicitType)
        {
            if (ExpectedResult == ResultType.Method || ExpectedResult == ResultType.Constructor)
            {
                var bv = target;
                if (bv is ObjectLiteral olt && ExpectedResult == ResultType.Constructor)
                {
                    return olt[name];
                }

                return bv;
            }

            object targetObject;
            bool isEnum;
            MemberInfo mi = FindMember(target, name, explicitType, out targetObject, out isEnum);
            ObjectLiteral ojl = targetObject as ObjectLiteral;
            FunctionLiteral ful = targetObject as FunctionLiteral;
            IDictionary<string, object> odi = targetObject as IDictionary<string, object>;
            IBasicKeyValueProvider iba = targetObject as IBasicKeyValueProvider;
            if (mi == null)
            {
                if (ojl != null)
                {
                    if (!whatIf)
                    {
                        return ojl[name];
                    }

                    return ojl.ContainsKey(name);
                }

                if (ful != null)
                {
                    if (!whatIf)
                    {
                        return ful.GetInitialScopeValue(name);
                    }

                    return ful.IsInitialScopeValueSet(name);
                }

                if (odi != null && odi.ContainsKey(name) && !whatIf)
                {
                    return odi[name];
                }
                else if (odi != null && !whatIf)
                {
                    return null;
                }
                else if (odi != null)
                {
                    return odi.ContainsKey(name);
                }

                if (iba != null && iba.ContainsKey(name) && !whatIf)
                {
                    return iba[name];
                }
                else if (iba != null && !whatIf)
                {
                    return null;
                }
                else if (iba != null)
                {
                    return iba.ContainsKey(name);
                }
            }

            if (
                isEnum)
            {
                if (!whatIf)
                {
                    return Enum.Parse((Type)targetObject, name);
                }

                return Enum.IsDefined((Type)targetObject, name);
            }

            if (mi == null)
            {
                if (!whatIf)
                {
                    throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                        targetObject));
                }

                return false;
            }

            if (mi is PropertyInfo pi)
            {
                if (!whatIf)
                {
                    if (pi.CanRead)
                    {
                        return pi.GetValue(targetObject, null);
                    }

                    return null;
                }

                return true;
            }

            if (mi is FieldInfo fi)
            {
                if (!whatIf)
                {
                    return fi.GetValue(targetObject);
                }

                return true;
            }

            if (mi is EventInfo)
            {
                if (!whatIf)
                {
                    return null;
                }

                return true;
            }

            if (!whatIf)
            {
                throw new ScriptException(string.Format("GetValue is not supported for MemberType {0}", mi.MemberType));
            }

            return false;
        }

        /// <summary>
        /// Finds the member with the given Name
        /// </summary>
        /// <param name="baseVal">the target object from which to read the value of the returned member</param>
        /// <param name="isEnum">indicates whether the base object is an enum type</param>
        /// <returns>a memberinfo that represents the name of this memberAccessValue object</returns>
        private static MemberInfo FindMember(object baseVal, string memberName, Type explicitTargetType, out object targetObject, out bool isEnum)
        {
            if (baseVal == null)
            {
                throw new ScriptException(string.Format("Unable to access {0} on a NULL - Value", memberName));
            }

            targetObject = baseVal;
            isEnum = false;
            bool isStatic = false;
            if (baseVal is Type)
            {
                targetObject = null;
                isStatic = true;
                if (((Type)baseVal).IsEnum)
                {
                    targetObject = baseVal;
                    isEnum = true;
                    return null;
                }
            }
            else
            {
                baseVal = explicitTargetType ?? baseVal.GetType();
            }

            Type t = (Type)baseVal;
            return (from m in t.GetMembers(BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance)) where m.Name == memberName select m).FirstOrDefault();
        }

        public void Assign(object value, ActiveCodeAccessDescriptor contextTarget)
        {
            object targetObject;
            bool isEnum;
            var target = contextTarget.BaseObject;
            var explicitType = contextTarget.ExplicitType;
            MemberInfo mi = FindMember(target, contextTarget.Name, explicitType, out targetObject, out isEnum);
            ObjectLiteral ojl = targetObject as ObjectLiteral;
            FunctionLiteral ful = targetObject as FunctionLiteral;
            IDictionary<string, object> odi = targetObject as IDictionary<string, object>;
            if (mi == null)
            {
                if (ojl != null)
                {
                    ojl[name] = value;
                    return;
                }

                if (ful != null)
                {
                    ful.SetInitialScopeValue(name, value);
                    return;
                }

                if (odi != null)
                {
                    odi[name] = value;
                    return;
                }

                throw new ScriptException(string.Format("Member {0} is not declared on {1}", name,
                    targetObject));
            }

            PropertyInfo pi;
            FieldInfo fi;
            if (mi is PropertyInfo && (pi = (PropertyInfo)mi).CanWrite)
            {
                pi.SetValue(targetObject, value, null);
            }
            else if (mi is FieldInfo && !(fi = (FieldInfo)mi).IsLiteral)
            {
                fi.SetValue(targetObject, value);
            }
            else if (mi is EventInfo && value is FunctionLiteral)
            {
                FunctionLiteral fl = value as FunctionLiteral;
                EventInfo ev = mi as EventInfo;
                ev.AddEventHandler(targetObject, fl.CreateDelegate(ev.EventHandlerType));
            }
            else
            {
                throw new ScriptException(string.Format("SetValue is not supported for this Member ({0}", name));
            }
        }
    }
}
