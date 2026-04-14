using System;
using Microsoft.UI.Xaml.Data;

namespace BlendHub.Converters
{
    public class PlatformConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string filename)
            {
                var lower = filename.ToLower();
                if (lower.Contains("arm64") || lower.Contains("aarch64"))
                    return "ARM64";
                if (lower.Contains("x64") || lower.Contains("64") || lower.Contains("amd64"))
                    return "x64";
                if (lower.Contains("x86") || lower.Contains("32") || lower.Contains("i686"))
                    return "x86";
            }
            return "x64"; // Default to x64
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
