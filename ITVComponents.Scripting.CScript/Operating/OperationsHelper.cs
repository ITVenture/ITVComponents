//#define TypeSave

using System;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Operating
{
    public static class OperationsHelper
    {
        public static object Add(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 + v2;
        }

        public static object Subtract(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 - v2;
        }

        public static object Multiply(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 * v2;
        }

        public static object Divide(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 / v2;
        }

        public static object Modulus(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 % v2;
        }

        public static object Xor(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 ^ v2;
        }

        public static object Or(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 | v2;
        }

        public static object And(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }

            return v1 & v2;
        }

        public static object Increment(object value)
        {
            dynamic val = value;
            return val + 1;
        }

        public static object Decrement(object value)
        {
            dynamic val = value;
            return val - 1;
        }

        public static object UnaryMinus(object value)
        {
            dynamic val = value;
            return -val;
        }

        public static object Negate(object value)
        {
            dynamic val = value;
            return ~val;
        }

        public static object LShift(object value1, object value2, bool fooTsIgnored=false)
        {
            dynamic v1, v2;
            v1 = value1;
            v2 = value2;
            return v1 << v2;
        }

        public static object RShift(object value1, object value2, bool fooTsIgnored=false)
        {
            dynamic v1, v2;
            v1 = value1;
            v2 = value2;
            return v1 >> v2;
        }

        public static int Compare(object value1, object value2, bool safe)
        {
            dynamic v1 = value1, v2 = value2;
            if (safe)
            {
                TypeInfo typ = GetAppropriateType(value1, value2);
                Cast(typ, value1, value2, out v1, out v2);
            }
            return v1.CompareTo(v2);
        }

        /// <summary>
        /// Parses a string into the appropriate type. Numbers with no suffix are parsed as integer if theres no floating point or to double if there is.
        /// </summary>
        /// <param name="number">the number to parse</param>
        /// <returns></returns>
        public static object ParseDecimalValue(string number)
        {
            if (number.EndsWith("UL"))
            {
                return ulong.Parse(number.Substring(0, number.Length - 2));
            }

            if (number.EndsWith("UI"))
            {
                return uint.Parse(number.Substring(0, number.Length - 2));
            }

            if (number.EndsWith("US"))
            {
                return ushort.Parse(number.Substring(0, number.Length - 2));
            }

            if (number.EndsWith("SB"))
            {
                return sbyte.Parse(number.Substring(0, number.Length - 2));
            }

            if (number.EndsWith("L"))
            {
                return long.Parse(number.Substring(0, number.Length - 1));
            }

            if (number.EndsWith("I"))
            {
                return int.Parse(number.Substring(0, number.Length - 1));
            }

            if (number.EndsWith("S"))
            {
                return short.Parse(number.Substring(0, number.Length - 1));
            }

            if (number.EndsWith("B"))
            {
                return byte.Parse(number.Substring(0, number.Length - 1));
            }

            if (number.EndsWith("M"))
            {
                return decimal.Parse(number.Substring(0, number.Length - 1));
            }

            if (number.EndsWith("D"))
            {
                return double.Parse(number.Substring(0, number.Length - 1));
            }

            if (number.EndsWith("F"))
            {
                return float.Parse(number.Substring(0, number.Length - 1));
            }

            if (number.IndexOf(".") == -1)
            {
                return int.Parse(number);
            }

            return double.Parse(number);
        }

        private static object Cast(TypeInfo targetType, object value)
        {
            switch (targetType)
            {
                case TypeInfo.Byte:
                    {
                        if (value is byte)
                        {
                            return value;
                        }

                        return Convert.ToByte(value);
                    }
                case TypeInfo.Decimal:
                    {
                        if (value is decimal)
                        {
                            return value;
                        }
                        return Convert.ToDecimal(value);
                    }
                case TypeInfo.Double:
                    {
                        if (value is double)
                        {
                            return value;
                        }
                        return Convert.ToDouble(value);
                    }
                case TypeInfo.Float:
                    {
                        if (value is float)
                        {
                            return value;
                        }
                        return Convert.ToSingle(value);
                    }
                case TypeInfo.Int:
                    {
                        if (value is int)
                        {
                            return value;
                        }
                        return Convert.ToInt32(value);
                    }
                case TypeInfo.LongInt:
                    {
                        if (value is long)
                        {
                            return value;
                        }
                        return Convert.ToInt64(value);
                    }
                case TypeInfo.ShortInt:
                    {
                        if (value is short)
                        {
                            return value;
                        }
                        return Convert.ToInt16(value);
                    }
                case TypeInfo.SignedByte:
                    {
                        if (value is sbyte)
                        {
                            return value;
                        }
                        return Convert.ToSByte(value);
                    }
                case TypeInfo.String:
                    {
                        if (value is string)
                        {
                            return value;
                        }
                        return value.ToString();
                    }
                case TypeInfo.UnsignedInt:
                    {
                        if (value is uint)
                        {
                            return value;
                        }
                        return Convert.ToUInt32(value);
                    }
                case TypeInfo.UnsignedLongInt:
                    {
                        if (value is ulong)
                        {
                            return value;
                        }
                        return Convert.ToUInt64(value);
                    }
                case TypeInfo.UnsignedShortInt:
                    {
                        if (value is ushort)
                        {
                            return value;
                        }
                        return Convert.ToUInt16(value);
                    }
                default:
                    {
                        throw new ScriptException("Unable to Cast Types meaningfully");
                    }
            }
        }

        private static void Cast(TypeInfo targetType, object value1, object value2, out object v1, out object v2)
        {
            v1 = Cast(targetType, value1);
            v2 = Cast(targetType, value2);
        }

        private static TypeInfo GetAppropriateType(object value1, object value2)
        {
            if (value1 != null && value2 != null)
            {
                if (value1 is string || value2 is string)
                {
                    return TypeInfo.String;
                }

                if (value1 is decimal || value2 is decimal)
                {
                    return TypeInfo.Decimal;
                }

                if (value1 is double || value2 is double)
                {
                    return TypeInfo.Double;
                }

                if (value1 is float || value2 is float)
                {
                    return TypeInfo.Float;
                }

                if (value1 is long || value2 is long)
                {
                    return TypeInfo.LongInt;
                }

                if (value1 is ulong || value2 is ulong)
                {
                    return TypeInfo.UnsignedLongInt;
                }

                if (value1 is int || value2 is int)
                {
                    return TypeInfo.Int;
                }

                if (value1 is uint || value2 is uint)
                {
                    return TypeInfo.UnsignedInt;
                }

                if (value1 is short || value2 is short)
                {
                    return TypeInfo.ShortInt;
                }

                if (value1 is ushort || value2 is ushort)
                {
                    return TypeInfo.UnsignedShortInt;
                }

                if (value1 is sbyte || value2 is sbyte)
                {
                    return TypeInfo.SignedByte;
                }

                if (value1 is byte || value2 is byte)
                {
                    return TypeInfo.Byte;
                }
            }

            return TypeInfo.Error;
        }

        private enum TypeInfo
        {
            Double,
            Float,
            Decimal,
            Byte,
            ShortInt,
            Int,
            LongInt,
            SignedByte,
            UnsignedShortInt,
            UnsignedInt,
            UnsignedLongInt,
            String,
            Error
        }
    }
}
