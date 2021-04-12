using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.PluginServices
{
    /// <summary>
    /// Describes a Plugin - Parameter
    /// </summary>
    [Serializable]
    public class PluginParameterElement
    {
        /// <summary>
        /// The Parameter-kind of the given parameter
        /// </summary>
        public ParameterKind TypeOfParameter { get; set; }

        /// <summary>
        /// The Literal-Kind for Literal - Parameters
        /// </summary>
        public LiteralKind LiteralKind { get; set; }

        /// <summary>
        /// The Value of the parameter
        /// </summary>
        public object ParameterValue { get; set; }

        /// <summary>
        /// Gibt einen <see cref="T:System.String"/> zurück, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </summary>
        /// <returns>
        /// Ein <see cref="T:System.String"/>, der das aktuelle <see cref="T:System.Object"/> darstellt.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            string retVal = string.Empty;
            if (ParameterValue != null)
            {
                switch (TypeOfParameter)
                {
                    case ParameterKind.Plugin:
                        retVal = string.Format("${0}", ParameterValue);
                        break;
                    case ParameterKind.Literal:
                        switch (LiteralKind)
                        {
                                case LiteralKind.String:
                                {
                                    retVal = string.Format("\"{0}\"", Escape(ParameterValue.ToString()));
                                    break;
                                }
                                case LiteralKind.Int:
                                {
                                    retVal = string.Format("I{0}", ParameterValue);
                                    break;
                                }
                                case LiteralKind.Long:
                                {
                                    retVal = string.Format("L{0}", ParameterValue);
                                    break;
                                }
                                case LiteralKind.Single:
                                {
                                    retVal = string.Format("F{0}", ParameterValue);
                                    break;
                                }
                            case LiteralKind.Double:
                                {
                                    retVal = string.Format("D{0}", ParameterValue);
                                    break;
                                }
                                case LiteralKind.Decimal:
                                {
                                    retVal = string.Format("M{0}", ParameterValue);
                                    break;
                                }
                                case LiteralKind.Boolean:
                                {
                                    retVal = string.Format("B{0}", ParameterValue);
                                    break;
                                }
                        }

                        break;
                    case ParameterKind.Expression:
                        retVal = string.Format("\"^^{0}\"", ParameterValue);
                        break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Escapes strings in order to put them properly back into the xml file
        /// </summary>
        /// <param name="value">the value that represents a string</param>
        /// <returns>the escaped value of the given string</returns>
        private string Escape(string value)
        {
            return value.Replace("\\","\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

    /// <summary>
    /// Describes allowed literal kinds
    /// </summary>
    public enum LiteralKind
    {
        /// <summary>
        /// A String Literal
        /// </summary>
        String,

        /// <summary>
        /// An Integer Literal
        /// </summary>
        Int,

        /// <summary>
        /// A Long Literal
        /// </summary>
        Long,

        /// <summary>
        /// A Float Literal 
        /// </summary>
        Single,

        /// <summary>
        /// A Double Literal
        /// </summary>
        Double,

        /// <summary>
        /// A Decimal Literal
        /// </summary>
        Decimal,

        /// <summary>
        /// A Boolean Literal
        /// </summary>
        Boolean
    }

    /// <summary>
    /// Describes allowed Parameter Kinds
    /// </summary>
    public enum ParameterKind
    {
        /// <summary>
        /// A Literal Parameter
        /// </summary>
        Literal,

        /// <summary>
        /// A Parameter that must be evaluated using an Expression parser
        /// </summary>
        Expression,

        /// <summary>
        /// A Plugin Reference - Parameter
        /// </summary>
        Plugin
    }
}
