using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BlendHub.Services
{
    /// <summary>
    /// Manages global file launcher associations (extension → program path).
    /// Persisted to launchers.json in the app's local data folder.
    /// </summary>
    public class LauncherSettingsService
    {
        private static readonly string LaunchersFilePath = GetSettingsPath();

        private static string GetSettingsPath()
        {
            try
            {
                // Try packaged storage first
                return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "launchers.json");
            }
            catch
            {
                // Fallback to app directory for standalone/unpackaged
                return Path.Combine(AppContext.BaseDirectory, "launchers.json");
            }
        }

        private static LauncherSettingsService? _instance;
        public static LauncherSettingsService Instance => _instance ??= new LauncherSettingsService();

        public Dictionary<string, string> Launchers { get; private set; } = new Dictionary<string, string>();

        private LauncherSettingsService()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(LaunchersFilePath))
                {
                    string json = File.ReadAllText(LaunchersFilePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (data != null)
                    {
                        Launchers = data;
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Launchers, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(LaunchersFilePath, json);
            }
            catch { }
        }

        public void SetLauncher(string extension, string programPath)
        {
            string ext = extension.StartsWith(".") ? extension : "." + extension;
            Launchers[ext.ToLowerInvariant()] = programPath;
            Save();
        }

        public void RemoveLauncher(string extension)
        {
            string ext = extension.StartsWith(".") ? extension : "." + extension;
            Launchers.Remove(ext.ToLowerInvariant());
            Save();
        }

        public string? GetLauncher(string extension)
        {
            string ext = extension.StartsWith(".") ? extension : "." + extension;
            return Launchers.TryGetValue(ext.ToLowerInvariant(), out var path) ? path : null;
        }

        /// <summary>
        /// Returns a copy of launchers to apply to a new project.
        /// </summary>
        public Dictionary<string, string> GetLaunchersForNewProject()
        {
            return new Dictionary<string, string>(Launchers);
        }
    }
}
