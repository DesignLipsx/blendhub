using System;
using System.IO;

namespace BlendHub.Helpers
{
    public static class VersionHelper
    {
        public static string GetFullVersionFromFilename(string filename)
        {
            // Extract full version from various filename formats:
            // - "blender-4.5.0-windows-x64.msi" → "4.5.0"
            // - "blender-2.83.19-windows-x64.msi" → "2.83.19"
            // - "blenderlts-2.83.5.0-windows64.msix" → "2.83.5.0"
            // - "blender-2.26-windows.exe" → "2.26"
            // - "blender2.04-windows.zip" → "2.04"
            // - "blender1.60_Windows.exe" → "1.60"
            try
            {
                var name = Path.GetFileNameWithoutExtension(filename).ToLower();

                // Handle blenderlts- prefix
                if (name.StartsWith("blenderlts-"))
                {
                    var parts = name.Substring(11).Split('-');
                    if (parts.Length > 0)
                        return parts[0];
                }

                // Handle blender- prefix (modern format)
                if (name.StartsWith("blender-"))
                {
                    var parts = name.Substring(8).Split('-');
                    if (parts.Length > 0)
                        return parts[0];
                }

                // Handle old format: blender2.04-windows, blender1.60_windows
                if (name.StartsWith("blender") && name.Length > 7)
                {
                    var afterBlender = name.Substring(7);
                    // Find where version number ends (before - or _)
                    var endIdx = 0;
                    while (endIdx < afterBlender.Length && 
                           (char.IsDigit(afterBlender[endIdx]) || afterBlender[endIdx] == '.'))
                    {
                        endIdx++;
                    }
                    if (endIdx > 0)
                        return afterBlender.Substring(0, endIdx);
                }

                return name;
            }
            catch
            {
                return filename;
            }
        }

        public static string GetShortVersion(string fullVersion)
        {
            // Convert "5.1.0" or "5.1.0 LTS" to "5.1"
            var clean = fullVersion.Replace("LTS", "").Trim();
            var parts = clean.Split('.');
            if (parts.Length >= 2)
                return $"{parts[0]}.{parts[1]}";
            return clean;
        }

        public static Version ParseVersion(string version)
        {
            // Parse version string like "5.1", "2.83 LTS", "2.93.18" into a comparable Version object
            var clean = version.Replace("LTS", "").Replace("lts", "").Replace("-newpy", "").Replace("beta", "").Trim();

            // Handle versions like "2.56abeta" - extract just the numeric part
            var numericPart = "";
            foreach (var c in clean)
            {
                if (char.IsDigit(c) || c == '.')
                    numericPart += c;
                else
                    break; // Stop at first non-numeric/non-dot character
            }

            if (string.IsNullOrEmpty(numericPart))
                return new Version(0, 0);

            var parts = numericPart.Split('.');
            try
            {
                int major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
                int minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
                int build = parts.Length > 2 && int.TryParse(parts[2], out var b) ? b : 0;
                int revision = parts.Length > 3 && int.TryParse(parts[3], out var r) ? r : 0;
                return new Version(major, minor, build, revision);
            }
            catch
            {
                return new Version(0, 0);
            }
        }
    }
}
