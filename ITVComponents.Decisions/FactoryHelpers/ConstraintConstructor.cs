using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions.FactoryHelpers
{
    /// <summary>
    /// Identifies a Type-Constructor
    /// </summary>
    public class ConstraintConstructor
    {
        /// <summary>
        /// indicates whether this Constructor definition holds a final type
        /// </summary>
        private bool isFinal = true;

        /// <summary>
        /// Holds the desired target type
        /// </summary>
        private readonly Type targetType;

        /// <summary>
        /// Holds the Parameter for the Generic IConstraint interface
        /// </summary>
        private readonly Type interfaceTypeParameter;

        /// <summary>
        /// Initializes a new instance of the ConstraintConstructor class
        /// </summary>
        /// <param name="targetType">the target type of which to initialize Constraints</param>
        /// <param name="typeIdentifier">the identifier for this Type-Constructor</param>
        public ConstraintConstructor(Type targetType, string typeIdentifier)
        {
            if (!ImplementsInterface(targetType, out interfaceTypeParameter))
            {
                throw new ArgumentException("The provided type does not implement IConstraint", nameof(targetType));
            }

            if (targetType.IsInterface)
            {
                throw new ArgumentException("The provided type must not be an interface", nameof(targetType));
            }

            if (targetType.IsAbstract)
            {
                throw new ArgumentException("The provided type must not be an abstract type", nameof(targetType));
            }

            if (targetType.IsGenericTypeDefinition)
            {
                isFinal = false;
                if (targetType.GetGenericArguments().Length > 1)
                {
                    throw new ArgumentException("Only Generic Types using one Parameter are supported.");
                }
            }

            this.targetType = targetType;
            Identifier = typeIdentifier;
        }

        /// <summary>
        /// Gets the Identifier for this ConstraintConstructor
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Gets the Parameters that are passed to the Constructor
        /// </summary>
        public List<ConstructorParameter> Parameters { get; } = new List<ConstructorParameter>();

        /// <summary>
        /// Provides the constructor for the provided Targettype
        /// </summary>
        /// <param name="targetType">the Type-Parameter for which to get the constructor</param>
        /// <param name="constructor">the Constructor that was found for the provied target-type</param>
        /// <returns>the constructor arguments for this ConstraintConstructor instance</returns>
        public object[] GetArguments(Type targetConstraintType, out ConstructorInfo constructor)
        {
            Type[] types = (from t in Parameters select t.ExpectedType).ToArray();
            Type constructedType = targetType;
            if (!isFinal)
            {
                constructedType = targetType.MakeGenericType(targetConstraintType);
            }
            else
            {
                if (targetConstraintType!= interfaceTypeParameter)
                {
                    throw new ArgumentException("This constructor does not support the Construction of Constraints taking the provided Type", nameof(targetConstraintType));
                }
            }

            constructor = constructedType.GetConstructor(types);
            if (constructor == null)
            {
                throw new InvalidOperationException("No Constructor found that would take the provided arguments");
            }

            return (from t in Parameters select t.Value).ToArray();
        }

        /// <summary>
        /// Indicates for any type whether it implements the IConstriant Interface
        /// </summary>
        /// <param name="targetType">the target-type that is being constructed with this Constructor</param>
        /// <returns>a value indicating whether the targetType impleents IConstraint</returns>
        private bool ImplementsInterface(Type targetType, out Type interfaceParameter)
        {
            Type theInterface = targetType.GetInterfaces().FirstOrDefault(x =>
            x.IsInterface &&
                ((x.IsGenericType &&
                x.GetGenericTypeDefinition() == typeof (IConstraint<>))
                || 
                (x.IsGenericTypeDefinition && x == typeof(IConstraint<>))));
            if (theInterface != null)
            {
                interfaceParameter = theInterface.GetGenericArguments().First();
                return true;
            }

            interfaceParameter = null;
            return false;
        }
    }
}
