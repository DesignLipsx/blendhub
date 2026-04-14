using Microsoft.UI.Xaml.Data;
using System;

namespace BlendHub.Converters
{
    public class BytesToSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long bytes)
            {
                if (bytes >= 1024 * 1024 * 1024)
                    return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
                if (bytes >= 1024 * 1024)
                    return $"{bytes / (1024.0 * 1024.0):F0} MB";
                if (bytes >= 1024)
                    return $"{bytes / 1024.0:F0} KB";
                return $"{bytes} B";
            }
            return "0 MB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
