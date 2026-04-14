using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using BlendHub.Models;
using BlendHub.Services;

namespace BlendHub.Pages
{
    public sealed partial class EditProjectDialogContent : UserControl
    {
        public ObservableCollection<ProjectFolder> Folders { get; } = new ObservableCollection<ProjectFolder>();
        public ObservableCollection<FileLauncher> Launchers { get; } = new ObservableCollection<FileLauncher>();

        private readonly Project _project;

        // Track original folder names so we can rename on disk
        private readonly List<string> _originalFolderNames;
        private readonly BlenderSettingsService _blenderService = new BlenderSettingsService();
        public List<BlenderVersionInfo> InstalledVersions { get; private set; } = new List<BlenderVersionInfo>();

        public event EventHandler? ValidationChanged;
        
        public EditProjectDialogContent(Project project)
        {
            this.InitializeComponent();
            _project = project;

            ProjectNameTextBox.Text = project.Name;
            ProjectPathTextBox.Text = project.Path;

            // Load Blender Versions
            InstalledVersions = _blenderService.GetInstalledVersions();
            BlenderVersionComboBox.ItemsSource = InstalledVersions;
            BlenderVersionComboBox.SelectedItem = InstalledVersions.FirstOrDefault(v => v.Version == project.BlenderVersion);
            BlenderVersionComboBox.SelectionChanged += Validation_Changed;

            // Load existing subfolders
            _originalFolderNames = new List<string>(project.Subfolders);
            for (int i = 0; i < project.Subfolders.Count; i++)
            {
                var f = new ProjectFolder($"Folder {i + 1}:", project.Subfolders[i]);
                f.PropertyChanged += Folder_PropertyChanged;
                Folders.Add(f);
            }

            // Load existing file launchers
            foreach (var kvp in project.FileLaunchers)
            {
                Launchers.Add(new FileLauncher
                {
                    Extension = kvp.Key,
                    ProgramPath = kvp.Value,
                    ProgramName = System.IO.Path.GetFileNameWithoutExtension(kvp.Value)
                });
            }

            // Merge any global launchers that the project doesn't already have
            var existingExts = new HashSet<string>(project.FileLaunchers.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in Services.LauncherSettingsService.Instance.Launchers)
            {
                if (!existingExts.Contains(kvp.Key))
                {
                    Launchers.Add(new FileLauncher
                    {
                        Extension = kvp.Key,
                        ProgramPath = kvp.Value,
                        ProgramName = System.IO.Path.GetFileNameWithoutExtension(kvp.Value)
                    });
                }
            }

            FoldersItemsControl.ItemsSource = Folders;
            LaunchersItemsControl.ItemsSource = Launchers;

            // Load .blend files
            if (System.IO.Directory.Exists(project.Path))
            {
                var blendFiles = System.IO.Directory.GetFiles(project.Path, "*.blend")
                    .Select(f => System.IO.Path.GetFileName(f))
                    .ToList();
                PrimaryBlendFileComboBox.ItemsSource = blendFiles;
                PrimaryBlendFileComboBox.SelectedItem = blendFiles.FirstOrDefault(f => f == project.BlendFileName) 
                    ?? blendFiles.FirstOrDefault();
            }
        }

        public string? BlendFileName => PrimaryBlendFileComboBox.SelectedItem?.ToString();

        public Project Project => _project;
        public List<string> OriginalFolderNames => _originalFolderNames;
        public string ProjectName => ProjectNameTextBox.Text;
        public string ProjectLocation => ProjectPathTextBox.Text;
        public BlenderVersionInfo? SelectedVersion => BlenderVersionComboBox.SelectedItem as BlenderVersionInfo;

        // --- Validation ---
        public bool IsValid
        {
            get
            {
                foreach (var folder in Folders)
                {
                    if (string.IsNullOrWhiteSpace(folder.Name))
                    {
                        if (WarningInfoBar != null)
                        {
                            WarningInfoBar.Message = "Folder name cannot be empty. Please enter a name or remove the folder.";
                            WarningInfoBar.IsOpen = true;
                            WarningInfoBar.Visibility = Visibility.Visible;
                        }
                        return false;
                    }
                }

                if (WarningInfoBar != null)
                {
                    WarningInfoBar.IsOpen = false;
                    WarningInfoBar.Visibility = Visibility.Collapsed;
                }
                return true;
            }
        }

        private void Folder_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectFolder.Name))
            {
                RaiseValidationChanged();
            }
        }

        private void RaiseValidationChanged()
        {
            ValidationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Validation_Changed(object sender, RoutedEventArgs e)
        {
            RaiseValidationChanged();
        }

        // --- Folders ---
        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = new ProjectFolder($"Folder {Folders.Count + 1}:", "");
            folder.PropertyChanged += Folder_PropertyChanged;
            Folders.Add(folder);
            RaiseValidationChanged();
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ProjectFolder folder)
            {
                folder.PropertyChanged -= Folder_PropertyChanged;
                Folders.Remove(folder);
                UpdateLabels();
                RaiseValidationChanged();
            }
        }

        private void UpdateLabels()
        {
            for (int i = 0; i < Folders.Count; i++)
            {
                Folders[i].Label = $"Folder {i + 1}:";
            }
        }

        // --- Launchers ---
        private void AddLauncher_Click(object sender, RoutedEventArgs e)
        {
            Launchers.Add(new FileLauncher());
        }

        private void DeleteLauncher_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is FileLauncher launcher)
            {
                Launchers.Remove(launcher);
            }
        }

        private async void BrowseLauncher_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not FileLauncher launcher) return;

            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add(".exe");

            var window = App.MainWindow;
            if (window != null)
            {
                IntPtr hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                launcher.ProgramPath = file.Path;
                launcher.ProgramName = System.IO.Path.GetFileNameWithoutExtension(file.Name);
            }
        }
    }
}
