using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ITVComponents.Presentation.Converters
{
    /*[ValueConversion(typeof(Bitmap), typeof(ImageSource))]
    public class BitmapToImageSourceConverter:IValueConverter
    {
        /// <summary>
        /// Konvertiert einen Wert.
        /// </summary>
        /// <returns>
        /// Ein konvertierter Wert. Wenn die Methode null zurückgibt, wird der gültige NULL-Wert verwendet.
        /// </returns>
        /// <param name="value">Der von der Bindungsquelle erzeugte Wert.</param><param name="targetType">Der Typ der Bindungsziel-Eigenschaft.</param><param name="parameter">Der zu verwendende Konverterparameter.</param><param name="culture">Die im Konverter zu verwendende Kultur.</param>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Bitmap bmp = value as Bitmap;
            if (bmp == null)
            {
                return null;
            }

            return Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty,
                                                         BitmapSizeOptions.FromEmptyOptions());
        }

        /// <summary>
        /// Konvertiert einen Wert.
        /// </summary>
        /// <returns>
        /// Ein konvertierter Wert. Wenn die Methode null zurückgibt, wird der gültige NULL-Wert verwendet.
        /// </returns>
        /// <param name="value">Der Wert, der vom Bindungsziel erzeugt wird.</param><param name="targetType">Der Typ, in den konvertiert werden soll.</param><param name="parameter">Der zu verwendende Konverterparameter.</param><param name="culture">Die im Konverter zu verwendende Kultur.</param>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }*/
}
