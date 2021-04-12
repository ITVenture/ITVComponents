//-----------------------------------------------------------------------
// <copyright file="XLNumber.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace ITVComponents.DataExchange.ExcelSource
{
    /// <summary>
    /// Converts a number into the Excel Column number format (a-z;aa-zz;...)
    /// </summary>
    public struct XLNumber : IFormattable
    {
        /// <summary>
        /// the Number represented by this instance
        /// </summary>
        private int number;

        /// <summary>
        /// Initializes a new instance of the XLNumber struct
        /// </summary>
        /// <param name="number">the integer value of the number</param>
        public XLNumber(int number)
        {
            this.number = number;
        }

        /// <summary>
        /// Addition operator used to add to a specific value
        /// </summary>
        /// <param name="target">the target number</param>
        /// <param name="addition">the value to add to it</param>
        /// <returns>the resulting XLNumber</returns>
        public static XLNumber operator +(XLNumber target, int addition)
        {
            XLNumber retVal = new XLNumber(target.number + addition);
            return retVal;
        }

        /// <summary>
        /// Incremential operator to add 1 to a XLNumber value
        /// </summary>
        /// <param name="target">the Target xlNumber</param>
        /// <returns>the resulting XLNUmber</returns>
        public static XLNumber operator ++(XLNumber target)
        {
            XLNumber retVal = new XLNumber(target.number);
            retVal.number++;
            return retVal;
        }

        /// <summary>
        /// Subtraction operator to subtract a specific value
        /// </summary>
        /// <param name="target">the target XLNumber</param>
        /// <param name="subtraction">the value to substract from it</param>
        /// <returns>the resulting XLNumber</returns>
        public static XLNumber operator -(XLNumber target, int subtraction)
        {
            XLNumber retVal = new XLNumber(target.number - subtraction);
            return retVal;
        }

        /// <summary>
        /// Decremental operator to substract 1 from the selected XLNumber value
        /// </summary>
        /// <param name="target">the target XLNumber</param>
        /// <returns>the resulting XLNumber</returns>
        public static XLNumber operator --(XLNumber target)
        {
            XLNumber retVal = new XLNumber(target.number);
            retVal.number--;
            return retVal;
        }

        /// <summary>
        /// Implicit Cast operator to cast a XLNumber value into an int value
        /// </summary>
        /// <param name="target">the target XLNumber</param>
        /// <returns>the integer representation of the XLNumber</returns>
        public static implicit operator int(XLNumber target)
        {
            return target.number;
        }

        /// <summary>
        /// Implicit Cast operator to cast an int value into a XLNumber value
        /// </summary>
        /// <param name="number">the target Int value</param>
        /// <returns>the XLNumber representation of an integer</returns>
        public static implicit operator XLNumber(int number)
        {
            return new XLNumber(number);
        }

        /// <summary>
        /// Creates a String Representation for a provided Format of this XLNumber value
        /// </summary>
        /// <param name="format">the Format used for this XLNumber formatter</param>
        /// <param name="formatProvider">the formatProvider used to format the number</param>
        /// <returns>an XLFormatted string</returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return this.CreateXLString();
        }

        /// <summary>
        /// Creates a String Representation for of this XLNumber value
        /// </summary>
        /// <returns>an XLFormatted string</returns>
        public override string ToString()
        {
            return this.CreateXLString();
        }

        /// <summary>
        /// Creates a String representation of the current XLNumber value
        /// </summary>
        /// <returns>a string representing the current XLNumber value</returns>
        private string CreateXLString()
        {
            string retVal = string.Empty;
            int num = this.number;
            bool hadNum = false;
            if (num > 0)
            {
                for (int i = 5; i >= 0; i--)
                {
                    if ((i > 0 && num >= (int)Math.Pow(26, i)) || (i == 0) || hadNum)
                    {
                        int idm;
                        if (this.number > 24)
                        {
                            idm = 0;
                        }

                        if (i != 0)
                        {
                            int tmpN = (int)num % (int)Math.Pow(26, i);
                            int tmpD = num - tmpN;
                            idm = tmpD / (int)Math.Pow(26, i);
                            if (hadNum)
                            {
                                retVal += (char)(byte)(idm + 65);
                            }
                            else
                            {
                                retVal += (char)(byte)(idm + 64);
                            }

                            num -= tmpD;
                            hadNum = true;
                        }
                        else
                        {
                            int tmpN = (int)num % (int)Math.Pow(25, i);
                            int tmpD = num - tmpN;
                            idm = tmpD / (int)Math.Pow(25, i);
                            retVal += (char)(byte)(idm + 65);
                            num -= tmpD;
                        }
                    }
                }
            }
            else
            {
                retVal = "A";
            }

            return retVal;
        }
    }
}