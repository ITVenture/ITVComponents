using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ITVComponents.Helpers
{
    public static class EnumHelper
    {
        /// <summary>
        /// Gets the values of the provided enum with the description values of it
        /// </summary>
        /// <typeparam name="TEnum">the enum-type that needs to be described</typeparam>
        /// <returns>an enumerable of enumvalues</returns>
        public static IEnumerable<EnumValueDescription> DescribeEnum<TEnum>(bool returnAllItems = false) where TEnum  : struct, IConvertible
        {
            Type enumType = typeof (TEnum);
            if (!enumType.IsEnum) throw new ArgumentException("T must be an enumerated type");

            foreach (TEnum item in Enum.GetValues(typeof(TEnum)))
            {
                string name = item.ToString();
                int value = Convert.ToInt32(item);
                string description = name;
                MemberInfo tmp = enumType.GetMember(item.ToString()).First();
                DescriptionAttribute att = (DescriptionAttribute)Attribute.GetCustomAttribute(tmp, typeof (DescriptionAttribute));
                bool doReturn = true;
                if (att != null)
                {
                    description = att.Description;
                    doReturn = !att.Description.StartsWith("--");
                }

                if (doReturn || returnAllItems)
                {
                    yield return new EnumValueDescription {Value = value, Name = name, Description = description};
                }
            }
        }

        public class EnumValueDescription
        {
            public int Value { get; set; }

            public string Name { get; set; }

            public string Description { get; set; }
        }
    }
}
