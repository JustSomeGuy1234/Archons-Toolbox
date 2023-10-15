using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ArchonsToolbox
{
    namespace MyConverters
    {
        public class FontSizeConverter : IValueConverter
        {
            public object Convert(object value, Type type, object obj, CultureInfo cultureInfo)
            {
                if (value == null)
                    return 15d;
                double fontSize = (double)value;
                return fontSize * AppSettings.StaticFontSizeMulti;
            }
            public object ConvertBack(object obj, Type type, object obj1, CultureInfo info)
            {
                throw new NotImplementedException();
            }
        }

        // Originally only for converting white/gray/black, but now acts as a converter for all colours.
        public class WhiteColourConverter : IValueConverter
        {
            public object Convert(object value, Type type, object obj, CultureInfo info)
            {
                try
                {
                    System.Collections.ObjectModel.ObservableCollection<ColourByte> collection = (System.Collections.ObjectModel.ObservableCollection<ColourByte>)value;
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(collection[0].Byte, collection[1].Byte, collection[2].Byte));
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                }   
            }

            public object ConvertBack(object obj, Type type, object obj1, CultureInfo info)
            {
                throw new NotImplementedException();
            }
        }
    }
}
