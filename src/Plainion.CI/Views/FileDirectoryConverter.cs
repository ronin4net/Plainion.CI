﻿using System;
using System.IO;
using System.Windows.Data;

namespace Plainion.CI.Views
{
    class FileDirectoryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var file = (string)value;
            return Path.GetDirectoryName(file);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
