using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using BlendHub.Models;
using BlendHub.Services;

namespace BlendHub.Pages
{
    public sealed partial class ProjectPage : Page
    {
        private readonly BlenderSettingsService _blenderService = new();
        public ObservableCollection<Project> Projects { get; } = new ObservableCollection<Project>();
        private List<Project> _allProjects = new List<Project>();
        private Project? _lastCreatedProject;

        public ProjectPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadProjects();
            if (e.Parameter is string projectName && !string.IsNullOrEmpty(projectName))
            {
                SearchTextBox.Text = projectName;
                ApplyFilterAndSort();
            }
        }

        public void LoadProjects()
        {
            try
            {
                var loadedProjects = ProjectService.LoadProjects();
                Debug.WriteLine($"[ProjectPage] Loaded {loadedProjects.Count} projects from JSON");
                
                _allProjects = loadedProjects;
                ApplyFilterAndSort();
                
                Debug.WriteLine($"[ProjectPage] UI updated with {Projects.Count} filtered projects");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectPage] Error loading projects: {ex.Message}");
            }
        }

        private string _currentSortOption = "DateDesc";

        private void ApplyFilterAndSort()
        {
            if (SearchTextBox == null) return;
            
            string filter = SearchTextBox.Text.Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(filter) 
                ? _allProjects 
                : _allProjects.Where(p => p.Name.ToLowerInvariant().Contains(filter) || p.Path.ToLowerInvariant().Contains(filter));

            bool searchBoxHadFocus = false;
            if (this.XamlRoot != null)
            {
                var focusedElement = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(this.XamlRoot);
                if (focusedElement == (object)SearchTextBox)
                {
                    searchBoxHadFocus = true;
                }
            }

            IEnumerable<Project> sorted;
            switch (_currentSortOption)
            {
                case "DateAsc": sorted = filtered.OrderBy(x => x.CreatedAt); break;
                case "NameAsc": sorted = filtered.OrderBy(x => string.IsNullOrEmpty(x.Name) ? "" : x.Name.ToLower()); break;
                case "NameDesc": sorted = filtered.OrderByDescending(x => string.IsNullOrEmpty(x.Name) ? "" : x.Name.ToLower()); break;
                case "DateDesc":
                default:
                    sorted = filtered.OrderByDescending(x => x.CreatedAt); break;
            }

            Projects.Clear();
            foreach (var p in sorted)
            {
                Projects.Add(p);
            }

            bool hasAnyProjects = _allProjects.Count > 0;

            if (ProjectsList != null)
            {
                ProjectsList.ItemsSource = Projects;
                ProjectsList.Visibility = hasAnyProjects ? Visibility.Visible : Visibility.Collapsed;
            }

            if (searchBoxHadFocus && SearchTextBox != null)
            {
                SearchTextBox.Focus(FocusState.Programmatic);
            }

            if (NoProjectsPanel != null)
            {
                NoProjectsPanel.Visibility = hasAnyProjects ? Visibility.Collapsed : Visibility.Visible;
            }
            
            if (DropZone != null)
            {
                DropZone.Margin = hasAnyProjects ? new Thickness(0, 0, 0, 8) : new Thickness(0);
            }
        }

        private void Filter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private void RefreshProjectsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadProjects();
        }

        private void SortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioMenuFlyoutItem item && item.IsChecked)
            {
                _currentSortOption = item.Tag?.ToString() ?? "DateDesc";
                SortButtonText.Text = item.Text;
                ApplyFilterAndSort();
            }
        }

        private async void ShowDialog_Click(object sender, RoutedEventArgs e)
        {
            var content = new CreateProjectDialogContent();
            var dialog = new ContentDialog
            {
                Title = "Create New Project",
                Content = content,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                CloseButtonStyle = Application.Current.Resources["DefaultButtonStyle"] as Style
            };

            dialog.IsPrimaryButtonEnabled = content.IsValid;
            content.ValidationChanged += (s, args) => { dialog.IsPrimaryButtonEnabled = content.IsValid; };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string projectName = content.ProjectName;
                string projectLocation = content.ProjectLocation;
                string fileName = content.FileName;
                var selectedVersion = content.SelectedVersion;
                string blenderExePath = selectedVersion?.ExecutablePath ?? string.Empty;
                string blenderVersionStr = selectedVersion?.Version ?? "Unknown";
                var folders = content.Folders.Select(f => f.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                var launchers = content.FileLaunchers
                    .Where(l => !string.IsNullOrWhiteSpace(l.Extension) && !string.IsNullOrWhiteSpace(l.ProgramPath))
                    .GroupBy(l => l.Extension.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().ProgramPath);

                CreatingProgressPanel.Visibility = Visibility.Visible;
                ProgressText.Text = $"Creating project '{projectName}'...";
                
                try 
                {
                    Project newProject = new Project
                    {
                        Name = projectName,
                        Path = Path.Combine(projectLocation, projectName),
                        BlendFileName = fileName,
                        BlenderVersion = blenderVersionStr,
                        CreatedAt = DateTime.Now,
                        Subfolders = folders,
                        FileLaunchers = launchers
                    };

                    await Task.Run(() => {
                        if (!Directory.Exists(newProject.Path)) Directory.CreateDirectory(newProject.Path);
                        foreach (var sub in newProject.Subfolders)
                        {
                            string subPath = Path.Combine(newProject.Path, sub);
                            if (!Directory.Exists(subPath)) Directory.CreateDirectory(subPath);
                        }
                        
                        if (!File.Exists(newProject.FullBlendPath))
                        {
                            bool createdProperly = false;
                            if (!string.IsNullOrEmpty(blenderExePath) && File.Exists(blenderExePath))
                            {
                                try {
                                    var info = new ProcessStartInfo {
                                        FileName = blenderExePath,
                                        Arguments = $"--background --python-expr \"import bpy; bpy.ops.wm.save_as_mainfile(filepath=r'{newProject.FullBlendPath}')\"",
                                        CreateNoWindow = true, UseShellExecute = false
                                    };
                                    using var p = Process.Start(info);
                                    if (p != null) createdProperly = p.WaitForExit(10000) && File.Exists(newProject.FullBlendPath);
                                } catch { }
                            }
                            if (!createdProperly) File.WriteAllText(newProject.FullBlendPath, "Blender project placeholder");
                        }
                    });

                    _allProjects.Add(newProject);
                    _lastCreatedProject = newProject;
                    ProjectService.SaveProjects(_allProjects);
                    ApplyFilterAndSort();

                    ProjectSuccessInfoBar.Title = "Project Created";
                    ProjectSuccessInfoBar.Message = $"Project '{newProject.Name}' has been successfully setup at {newProject.Path}.";
                    ProjectSuccessInfoBar.IsOpen = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProjectPage] Error creating project: {ex}");
                    await Task.Delay(100);
                    var errorDialog = new ContentDialog {
                        Title = "Creation Failed", Content = $"Could not create project folders: {ex.Message}",
                        CloseButtonText = "OK", XamlRoot = this.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default
                    };
                    await errorDialog.ShowAsync();
                }
                finally { CreatingProgressPanel.Visibility = Visibility.Collapsed; }
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            if (_lastCreatedProject != null && File.Exists(_lastCreatedProject.FullBlendPath))
                Process.Start(new ProcessStartInfo(_lastCreatedProject.FullBlendPath) { UseShellExecute = true });
        }

        private async void BrowseProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add(".blend");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var file = await picker.PickSingleFileAsync();
            if (file != null) await ProcessDroppedFolderAsync(await file.GetParentAsync());
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Add to BlendHub";
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items) if (item is Windows.Storage.StorageFolder folder) await ProcessDroppedFolderAsync(folder);
            }
        }

        private async Task ProcessDroppedFolderAsync(Windows.Storage.StorageFolder folder)
        {
            if (folder == null) return;
            string path = folder.Path;
            var blendFiles = Directory.GetFiles(path, "*.blend");
            if (blendFiles.Length == 0) return;

            string mainBlend = blendFiles[0];
            if (_allProjects.Any(p => p.Path == path)) return;

            // Automatically scan for existing subdirectories
            var subfolders = Directory.GetDirectories(path)
                .Select(d => System.IO.Path.GetFileName(d))
                .ToList();

            var newProject = new Project {
                Name = folder.Name, Path = path,
                BlendFileName = Path.GetFileName(mainBlend),
                CreatedAt = DateTime.Now, 
                BlenderVersion = "Unknown",
                Subfolders = subfolders
            };
            _allProjects.Add(newProject);
            ProjectService.SaveProjects(_allProjects);
            ApplyFilterAndSort();
        }
    }
}
