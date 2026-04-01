namespace BlendHub.Helpers
{
    public static class FileTypeHelper
    {
        public static string GetPlatformFromFilename(string filename)
        {
            if (filename.Contains("windows-x64") || filename.Contains("windows64"))
                return "Windows x64";
            else if (filename.Contains("windows32"))
                return "Windows x86";
            else if (filename.Contains("windows-arm64"))
                return "Windows ARM64";
            else if (filename.Contains("windows"))
                return "Windows";
            else
                return "Unknown";
        }

        public static string GetTypeFromFilename(string filename)
        {
            if (filename.EndsWith(".msi"))
                return "installer";
            else if (filename.EndsWith(".msix"))
                return "store";
            else if (filename.EndsWith(".exe"))
                return "executable";
            else if (filename.EndsWith(".zip"))
                return "portable";
            else
                return "unknown";
        }
    }
}
