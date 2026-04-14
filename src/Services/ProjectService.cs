using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BlendHub.Models;

namespace BlendHub.Services
{
    public class ProjectService
    {
        private static readonly string ProjectsFilePath;

        static ProjectService()
        {
            var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var blendHubFolder = Path.Combine(appDataRoaming, "BlendHub");
            if (!Directory.Exists(blendHubFolder))
            {
                Directory.CreateDirectory(blendHubFolder);
            }
            ProjectsFilePath = Path.Combine(blendHubFolder, "projects.json");
        }

        public static List<Project> LoadProjects()
        {
            try
            {
                if (File.Exists(ProjectsFilePath))
                {
                    string json = File.ReadAllText(ProjectsFilePath);
                    return JsonSerializer.Deserialize<List<Project>>(json) ?? new List<Project>();
                }
            }
            catch (Exception) { }
            return new List<Project>();
        }

        public static void SaveProjects(List<Project> projects)
        {
            try
            {
                string json = JsonSerializer.Serialize(projects, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProjectsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProjectService] Error saving projects: {ex.Message}");
            }
        }

        public static void UpdateProject(Project project)
        {
            var projects = LoadProjects();
            var index = projects.FindIndex(p => p.Name == project.Name && p.Path == project.Path);
            if (index != -1)
            {
                projects[index] = project;
                SaveProjects(projects);
            }
        }

        public static void RemoveProject(Project project)
        {
            var projects = LoadProjects();
            var initialCount = projects.Count;
            
            // Remove all projects matching both name and path
            projects.RemoveAll(p => p.Name == project.Name && p.Path == project.Path);
            
            Debug.WriteLine($"[ProjectService] Removed {initialCount - projects.Count} project(s) matching '{project.Name}' at '{project.Path}'");
            
            SaveProjects(projects);
        }
    }
}
