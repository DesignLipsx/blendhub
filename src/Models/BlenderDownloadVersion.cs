using System;
using Microsoft.UI.Xaml;

namespace BlendHub.Models
{
    public class BlenderDownloadVersion
    {
        public string Version { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public class BlenderVersionGroup
    {
        public string Version { get; set; } = string.Empty;
        public string ShortVersion { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public Visibility IsLatest { get; set; } = Visibility.Collapsed;
        public string InstallersCountText { get; set; } = string.Empty;
        
        // Added for performance optimization (pre-calculated sorting)
        public Version ComparableVersion { get; set; } = new Version(0, 0);
        public DateTime ComparableDate { get; set; } = DateTime.MinValue;
    }
}
