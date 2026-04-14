using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using BlendHub.Models;
using Windows.ApplicationModel;

namespace BlendHub.Helpers
{
    public static class WebVersionDatabaseHelper
    {
        private static string GetAppDirectory()
        {
            try
            {
                // For packaged apps (MSIX), use InstalledLocation
                if (Package.Current != null)
                {
                    return Package.Current.InstalledLocation.Path;
                }
            }
            catch
            {
                // Package.Current throws in unpackaged mode
            }
            // Fallback to base directory for unpackaged apps
            return AppContext.BaseDirectory;
        }

        private static string GetRoamingDatabasePath()
        {
            var roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var blendHubPath = Path.Combine(roamingPath, "BlendHub");
            
            if (!Directory.Exists(blendHubPath))
            {
                Directory.CreateDirectory(blendHubPath);
            }
            
            return Path.Combine(blendHubPath, "blender_versions_web.db");
        }

        private static string GetConnectionString()
        {
            var path = GetRoamingDatabasePath();
            return $"Data Source={path};Cache=Shared;Pooling=false;";
        }

        /// <summary>
        /// Initialize database by copying from app package to roaming folder on first run
        /// </summary>
        public static async Task InitializeDatabaseAsync()
        {
            try
            {
                var roamingDbPath = GetRoamingDatabasePath();
                
                Debug.WriteLine($"[WebDB] InitializeDatabaseAsync called");
                Debug.WriteLine($"[WebDB] Roaming path: {roamingDbPath}");
                Debug.WriteLine($"[WebDB] Roaming file exists: {File.Exists(roamingDbPath)}");
                
                // Check if database already exists in roaming folder
                if (!File.Exists(roamingDbPath))
                {
                    Debug.WriteLine("[WebDB] Database not found in roaming folder, copying from app package...");
                    
                    // Copy from app installation directory
                    var appDbPath = Path.Combine(GetAppDirectory(), "blender_versions_web.db");
                    
                    Debug.WriteLine($"[WebDB] Looking for source at: {appDbPath}");
                    Debug.WriteLine($"[WebDB] Source file exists: {File.Exists(appDbPath)}");
                    
                    if (File.Exists(appDbPath))
                    {
                        File.Copy(appDbPath, roamingDbPath, true);
                        Debug.WriteLine($"[WebDB] Copied database to: {roamingDbPath}");
                    }
                    else
                    {
                        Debug.WriteLine($"[WebDB] Source database not found at: {appDbPath}");
                        // Create empty database with schema
                        await CreateEmptyDatabaseAsync();
                    }
                }
                else
                {
                    Debug.WriteLine($"[WebDB] Database already exists at: {roamingDbPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebDB] Error initializing database: {ex.Message}");
                Debug.WriteLine($"[WebDB] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static async Task CreateEmptyDatabaseAsync()
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Versions (
                            Id TEXT PRIMARY KEY,
                            Version TEXT NOT NULL,
                            Directory TEXT NOT NULL,
                            LastUpdated TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS Installers (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            VersionId TEXT NOT NULL,
                            Filename TEXT NOT NULL,
                            Url TEXT NOT NULL,
                            ReleaseDate TEXT NOT NULL,
                            SizeBytes INTEGER NOT NULL,
                            FOREIGN KEY(VersionId) REFERENCES Versions(Id)
                        );

                        CREATE INDEX IF NOT EXISTS idx_version_id ON Installers(VersionId);
                    ";
                    await command.ExecuteNonQueryAsync();
                }
            }

            Debug.WriteLine("[WebDB] Created empty database with schema");
        }

        /// <summary>
        /// Get all versions from database
        /// </summary>
        public static async Task<Dictionary<string, BlenderVersionJsonInfo>> GetAllVersionsAsync()
        {
            var result = new Dictionary<string, BlenderVersionJsonInfo>();
            var roamingDbPath = GetRoamingDatabasePath();

            // Don't try to open if file doesn't exist - SQLite would create empty file
            if (!File.Exists(roamingDbPath))
            {
                Debug.WriteLine("[WebDB] Database file doesn't exist, returning empty result");
                return result;
            }

            try
            {
                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT Id, Version FROM Versions ORDER BY Id DESC";
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var versionId = reader.GetString(0);
                                var version = reader.GetString(1);

                                var versionInfo = new BlenderVersionJsonInfo
                                {
                                    Version = version,
                                    WindowsInstallers = new List<WindowsInstaller>()
                                };

                                // Get installers for this version
                                using (var installerCommand = connection.CreateCommand())
                                {
                                    installerCommand.CommandText = @"
                                        SELECT Filename, Url, ReleaseDate, SizeBytes FROM Installers
                                        WHERE VersionId = @versionId";
                                    installerCommand.Parameters.AddWithValue("@versionId", versionId);

                                    using (var installerReader = await installerCommand.ExecuteReaderAsync())
                                    {
                                        while (await installerReader.ReadAsync())
                                        {
                                            versionInfo.WindowsInstallers.Add(new WindowsInstaller
                                            {
                                                Filename = installerReader.GetString(0),
                                                Url = installerReader.GetString(1),
                                                ReleaseDate = installerReader.IsDBNull(2) ? "Unknown" : installerReader.GetString(2),
                                                SizeBytes = installerReader.GetInt64(3)
                                            });
                                        }
                                    }
                                }

                                result[versionId] = versionInfo;
                            }
                        }
                    }
                }

                Debug.WriteLine($"[WebDB] Retrieved {result.Count} versions from database");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebDB] Error retrieving versions: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Refresh database from app package (use when updating)
        /// </summary>
        public static async Task RefreshDatabaseAsync()
        {
            var roamingDbPath = GetRoamingDatabasePath();
            var appDbPath = Path.Combine(GetAppDirectory(), "blender_versions_web.db");

            if (!File.Exists(appDbPath))
            {
                Debug.WriteLine($"[WebDB] Source database not found at: {appDbPath}");
                return;
            }

            // Retry logic for file lock
            int maxRetries = 3;
            int delayMs = 100;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Force GC to release any lingering SQLite handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    if (File.Exists(roamingDbPath))
                    {
                        File.Delete(roamingDbPath);
                    }

                    File.Copy(appDbPath, roamingDbPath, true);
                    Debug.WriteLine($"[WebDB] Refreshed database from {appDbPath}");
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Debug.WriteLine($"[WebDB] File locked, retrying... ({i + 1}/{maxRetries})");
                    await Task.Delay(delayMs * (i + 1));
                }
            }

            throw new IOException("Unable to refresh database - file is locked by another process");
        }

        /// <summary>
        /// Get database path for diagnostics
        /// </summary>
        public static string GetDatabasePath() => GetRoamingDatabasePath();
    }
}
