using System;
using System.Collections.Generic;

namespace BlendHub.Models
{
    public enum ProjectItemType { Note, Todo }
    
    public enum TodoPriority { None, Low, Medium, High }

    public class ProjectItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ProjectItemType Type { get; set; } = ProjectItemType.Note;
        
        private string _content = string.Empty;
        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(nameof(Content)); }
        }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Todo-specific properties
        private TodoPriority _priority = TodoPriority.Medium;
        public TodoPriority Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(nameof(Priority)); }
        }

        private bool _isCompleted = false;
        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    public class Project
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string BlendFileName { get; set; } = string.Empty;
        public List<string> Subfolders { get; set; } = new List<string>();
        public string BlenderVersion { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Notes { get; set; } = string.Empty; // Legacy
        public List<ProjectItem> Items { get; set; } = new List<ProjectItem>();

        /// <summary>
        /// Custom file launchers: maps file extension (e.g. ".psd") to executable path.
        /// </summary>
        public Dictionary<string, string> FileLaunchers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Manually-added project files (relative paths) that aren't auto-detected by launchers.
        /// Opened with system defaults.
        /// </summary>
        public List<string> CustomFiles { get; set; } = new List<string>();

        public string FullBlendPath => System.IO.Path.Combine(Path, BlendFileName.EndsWith(".blend") ? BlendFileName : BlendFileName + ".blend");

        [System.Text.Json.Serialization.JsonIgnore]
        public bool FolderExists => System.IO.Directory.Exists(Path);

        [System.Text.Json.Serialization.JsonIgnore]
        public bool BlendFileExists => System.IO.File.Exists(FullBlendPath);

        [System.Text.Json.Serialization.JsonIgnore]
        public string StatusText => FolderExists ? "" : "⚠ Project folder not found";

        // Static helpers for x:Bind function binding
        public static double GetOpacity(bool exists) => exists ? 1.0 : 0.55;
        public static Microsoft.UI.Xaml.Visibility GetMissingVisibility(bool exists) => 
            exists ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
