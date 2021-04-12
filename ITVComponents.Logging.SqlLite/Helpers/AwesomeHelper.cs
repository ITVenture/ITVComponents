using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using FontAwesome.Sharp;

namespace ITVComponents.Logging.SqlLite.Helpers
{
    public static class AwesomeHelper
    {
        public static Icon IconForSeverity(int severity)
        {
            if (severity < 30)
            {
                return new Icon(IconChar.InfoCircle);
            }

            if (severity < 60)
            {
                return new Icon(IconChar.ExclamationCircle);
            }

            return new Icon(IconChar.StopCircle);
        }

        public static Brush BrushForSeverity(int severity)
        {
            if (severity < 30)
            {
                return Brushes.Blue;
            }

            if (severity < 60)
            {
                return Brushes.Goldenrod;
            }

            return Brushes.Red;
        }
    }
}
