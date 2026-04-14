using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlendHub.Models
{
    public class WindowsInstaller
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;

        [JsonPropertyName("size_bytes")]
        public long SizeBytes { get; set; }
    }

    public class BlenderVersionJsonInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("windows_installers")]
        public List<WindowsInstaller> WindowsInstallers { get; set; } = new();
    }
}
