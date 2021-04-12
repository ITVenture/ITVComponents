using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ITVComponents.Presentation.Converters
{
    public class IsNullConverter:IValueConverter 
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool retVal = value != null;
            if (retVal && !(value is bool || value is bool?))
            {
                PropertyInfo pif = value.GetType().GetProperty("Count");
                if (pif != null)
                {
                    retVal = pif.GetValue(value).ToString() != "0";
                }
                else
                {
                    pif = value.GetType().GetProperty("Length");
                    if (pif != null)
                    {
                        retVal = pif.GetValue(value).ToString() != "0";
                    }
                }
            }
            else if (retVal && (value is bool || value is bool?))
            {
                retVal = (bool)value;
            }

            return retVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
