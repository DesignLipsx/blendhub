using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlendHub.Services
{
    public class BlenderVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string DisplayName => $"Blender {Version}";
        public bool IsUpdateAvailable { get; set; }
    }

    public class BackupItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string RelativePath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
    }

    public class BlenderSettingsService
    {
        private static readonly string[] IgnoredFolders = { "__pycache__", ".cache", ".local", ".git", ".svn", ".idea", ".vscode", "sync_blender" };
        private static readonly string[] IgnoredExtensions = { ".pyc", ".pyo", ".log", ".tmp" };

        public string GetBlenderRootPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blender Foundation", "Blender");
        }

        public List<BlenderVersionInfo> GetInstalledVersions()
        {
            var versions = new List<BlenderVersionInfo>();
            var configRoot = GetBlenderRootPath();
            var searchPaths = new[] 
            {
                @"C:\Program Files\Blender Foundation",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steamapps\common\Blender"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps"),
                @"C:\Program Files\WindowsApps"
            };

            if (Directory.Exists(configRoot))
            {
                foreach (var dir in Directory.GetDirectories(configRoot))
                {
                    var version = Path.GetFileName(dir);
                    if (char.IsDigit(version[0]))
                    {
                        var info = new BlenderVersionInfo 
                        { 
                            Version = version, 
                            ConfigPath = dir 
                        };

                        // Search for executable across common paths
                        foreach (var root in searchPaths)
                        {
                            if (!Directory.Exists(root)) continue;

                            try 
                            {
                                // Check for direct matches in WindowsApps for Store apps
                                if (root.Contains("WindowsApps"))
                                {
                                    // Specifically look for folders containing "Blender" and version
                                    var storeDir = Directory.GetDirectories(root)
                                        .FirstOrDefault(d => d.Contains("Blender") && d.Contains(version));
                                    
                                    if (storeDir != null)
                                    {
                                        var exe = Path.Combine(storeDir, "blender.exe");
                                        if (!File.Exists(exe)) exe = Path.Combine(storeDir, "blender-launcher.exe");
                                        
                                        if (File.Exists(exe))
                                        {
                                            info.ExecutablePath = exe;
                                            break;
                                        }
                                    }

                                    // Fallback to top-level alias (standard but version-agnostic)
                                    var alias = Path.Combine(root, "blender-launcher.exe");
                                    if (File.Exists(alias))
                                    {
                                        info.ExecutablePath = alias;
                                        // Don't break here, we might find a better versioned one
                                    }
                                }
                                else 
                                {
                                    var installDir = Directory.GetDirectories(root)
                                        .FirstOrDefault(d => d.Contains(version) || d.EndsWith("Blender"));
                                    
                                    if (installDir != null)
                                    {
                                        var exe = Path.Combine(installDir, "blender.exe");
                                        if (!File.Exists(exe)) exe = Path.Combine(installDir, "blender-launcher.exe");
                                        
                                        if (File.Exists(exe))
                                        {
                                            info.ExecutablePath = exe;
                                            break;
                                        }
                                    }
                                }
                            } catch { /* Handle permission issues with WindowsApps */ }
                        }

                        // Simulate an update check
                        if (version == "4.2") info.IsUpdateAvailable = true;

                        versions.Add(info);
                    }
                }
            }

            return versions.OrderByDescending(v => v.Version).ToList();
        }

        public void LaunchBlender(string exePath)
        {
            if (File.Exists(exePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
        }

        public void OpenConfigFolder(string configPath)
        {
            if (Directory.Exists(configPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", configPath);
            }
        }

        public List<BackupItem> GetDefaultBackupItems(string versionPath)
        {
            var items = new List<BackupItem>
            {
                new BackupItem { Name = "Preferences", RelativePath = "config/userpref.blend", IsFolder = false },
                new BackupItem { Name = "Startup File", RelativePath = "config/startup.blend", IsFolder = false },
                new BackupItem { Name = "Bookmarks", RelativePath = "config/bookmarks.txt", IsFolder = false },
                new BackupItem { Name = "Recent Files", RelativePath = "config/recent-files.txt", IsFolder = false },
                new BackupItem { Name = "Addons", RelativePath = "scripts/addons", IsFolder = true },
                new BackupItem { Name = "Presets", RelativePath = "scripts/presets", IsFolder = true },
                new BackupItem { Name = "Extensions", RelativePath = "extensions", IsFolder = true }
            };

            // Filter for items that actually exist
            return items.Where(item => {
                var fullPath = Path.Combine(versionPath, item.RelativePath);
                return item.IsFolder ? Directory.Exists(fullPath) : File.Exists(fullPath);
            }).ToList();
        }

        public async Task BackupAsync(string versionPath, string destinationPath, List<BackupItem> items, Action<string, double>? onProgress = null)
        {
            var versionName = Path.GetFileName(versionPath);
            var backupRoot = Path.Combine(destinationPath, "Sync Blender", versionName);
            
            if (!Directory.Exists(backupRoot))
                Directory.CreateDirectory(backupRoot);

            int totalItems = items.Count;
            int currentItem = 0;

            foreach (var item in items.Where(i => i.IsEnabled))
            {
                var src = Path.Combine(versionPath, item.RelativePath);
                var dst = Path.Combine(backupRoot, item.RelativePath);

                onProgress?.Invoke($"Backing up {item.Name}...", (double)currentItem / totalItems);

                if (item.IsFolder)
                {
                    await CopyDirectoryAsync(src, dst);
                }
                else
                {
                    var dstDir = Path.GetDirectoryName(dst);
                    if (dstDir != null && !Directory.Exists(dstDir))
                        Directory.CreateDirectory(dstDir);
                    
                    if (File.Exists(src))
                        File.Copy(src, dst, true);
                }

                currentItem++;
            }

            onProgress?.Invoke("Backup complete", 1.0);
        }

        public async Task RestoreAsync(string backupPath, string targetVersionPath, List<BackupItem> items, Action<string, double>? onProgress = null)
        {
            int totalItems = items.Count;
            int currentItem = 0;

            foreach (var item in items.Where(i => i.IsEnabled))
            {
                var src = Path.Combine(backupPath, item.RelativePath);
                var dst = Path.Combine(targetVersionPath, item.RelativePath);

                onProgress?.Invoke($"Restoring {item.Name}...", (double)currentItem / totalItems);

                if (item.IsFolder)
                {
                    if (Directory.Exists(src))
                        await CopyDirectoryAsync(src, dst);
                }
                else
                {
                    var dstDir = Path.GetDirectoryName(dst);
                    if (dstDir != null && !Directory.Exists(dstDir))
                        Directory.CreateDirectory(dstDir);

                    if (File.Exists(src))
                        File.Copy(src, dst, true);
                }

                currentItem++;
            }

            onProgress?.Invoke("Restore complete", 1.0);
        }

        public async Task SyncAsync(string sourceVersionPath, string targetVersionPath, List<BackupItem> items, Action<string, double>? onProgress = null)
        {
            await RestoreAsync(sourceVersionPath, targetVersionPath, items, onProgress);
        }

        private async Task CopyDirectoryAsync(string sourceDir, string destinationDir)
        {
            if (!Directory.Exists(sourceDir)) return;

            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                // Filter out ignored folders and extensions
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                
                if (pathParts.Any(p => IgnoredFolders.Contains(p))) continue;
                if (IgnoredExtensions.Contains(Path.GetExtension(file))) continue;

                var destFile = Path.Combine(destinationDir, relativePath);
                var destFileDir = Path.GetDirectoryName(destFile);
                if (destFileDir != null) Directory.CreateDirectory(destFileDir);

                await Task.Run(() => File.Copy(file, destFile, true));
            }
        }

        public List<string> GetBackupVersions(string backupRoot)
        {
            var syncFolder = Path.Combine(backupRoot, "Sync Blender");
            if (!Directory.Exists(syncFolder)) return new List<string>();

            return Directory.GetDirectories(syncFolder)
                            .Select(Path.GetFileName)
                            .Where(n => !string.IsNullOrEmpty(n))
                            .Cast<string>()
                            .ToList();
        }
    }
}
