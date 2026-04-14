using System;
using System.IO;
using System.Text.Json;

namespace BlendHub.Services
{
    public class AppSettings
    {
        public string BackupDirectory { get; set; } = string.Empty;
        public string UserName { get; set; } = "User";
        public string DefaultPage { get; set; } = "home";
        public System.Collections.Generic.List<string> CustomBlenderPaths { get; set; } = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> DefaultFolders { get; set; } = new System.Collections.Generic.List<string>
        {
            "Scenes", "Assets", "Images", "Source Images", "Hdri", "Clip", "Sound", "Scripts", "Movies"
        };
        public System.Collections.Generic.Dictionary<string, string> DefaultLaunchers { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
    }

    public class AppSettingsService
    {
        private static readonly string SettingsFilePath = GetSettingsPath();

        private static string GetSettingsPath()
        {
            try
            {
                return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "appsettings.json");
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            }
        }

        private static AppSettingsService? _instance;
        public static AppSettingsService Instance => _instance ??= new AppSettingsService();

        public AppSettings Settings { get; private set; } = new AppSettings();

        private AppSettingsService()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var data = JsonSerializer.Deserialize<AppSettings>(json);
                    if (data != null)
                    {
                        Settings = data;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(Settings.BackupDirectory))
            {
                Settings.BackupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BlendHub");
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
