using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using WinRT.Interop;
using BlendHub.Models;
using BlendHub.Services;
using CommunityToolkit.WinUI.Controls;

namespace BlendHub.Pages
{
    public sealed partial class ProjectDetailPage : Page, INotifyPropertyChanged
    {
        private Project? _project;
        private System.Collections.ObjectModel.ObservableCollection<ProjectItemViewModel> _items = new();

        public ProjectDetailPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Project project)
            {
                _project = project;
                LoadProjectDetails();
            }
        }

        private void LoadProjectDetails()
        {
            if (_project == null) return;

            ProjectTitle.Text = _project.Name;
            ProjectPath.Text = _project.Path;
            BlenderVersionText.Text = _project.BlenderVersion;
            BlendFileText.Text = _project.BlendFileName;
            CreatedAtText.Text = _project.CreatedAt.ToString("f");
            
            if (Directory.Exists(_project.Path))
            {
                var dirInfo = new DirectoryInfo(_project.Path);
                ModifiedAtText.Text = dirInfo.LastWriteTime.ToString("f");
                try {
                    long size = CalculateFolderSize(_project.Path);
                    ProjectSizeText.Text = FormatBytes(size);
                } catch { ProjectSizeText.Text = "Unknown"; }
            }
            else
            {
                ModifiedAtText.Text = "N/A";
                ProjectSizeText.Text = "N/A";
            }

            bool folderExists = _project.FolderExists;
            bool blendExists = _project.BlendFileExists;

            MissingProjectInfoBar.IsOpen = !folderExists;
            OpenBlendBtn.IsEnabled = blendExists;
            OpenFolderBtn.IsEnabled = folderExists;
            EditBtn.IsEnabled = folderExists;

            if (ContentSelectorBar != null)
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    _previousTabIndex = -1;
                    ContentSelectorBar.SelectedItem = LocationsTab;
                    LoadTabContent(0);
                });
            }
        }

        private long CalculateFolderSize(string folderPath)
        {
            long size = 0;
            var dirInfo = new DirectoryInfo(folderPath);
            foreach (var file in dirInfo.GetFiles("*.*", SearchOption.AllDirectories)) size += file.Length;
            return size;
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1) { number /= 1024; counter++; }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }

        private int _previousTabIndex = -1;

        private void ContentSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            int selectedIndex = sender.Items.IndexOf(sender.SelectedItem);
            LoadTabContent(selectedIndex);
            _previousTabIndex = selectedIndex;
        }

        private void LoadTabContent(int tabIndex)
        {
            if (_project == null || ContentFrame == null) return; // Note: using local ContentFrame

            RefreshLocationsBtn.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            AddNoteBtn.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            AddFileBtn.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

            UIElement? newContent = null;
            switch (tabIndex)
            {
                case 0: newContent = GetLocationsPanel(); break;
                case 1: LoadProjectItems(); newContent = GetNotesPanel(); break;
                case 2: newContent = GetFilesPanel(); break;
            }

            if (newContent != null)
            {
                var transitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
                var slideEffect = new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition {
                    FromHorizontalOffset = _previousTabIndex == -1 ? 150 : (tabIndex > _previousTabIndex ? 150 : -150),
                    FromVerticalOffset = 0
                };
                transitions.Add(slideEffect);
                ContentFrame.ContentTransitions = transitions;
                ContentFrame.Content = newContent;
            }
        }

        private int GetFolderItemCount(string folderPath)
        {
            try {
                if (Directory.Exists(folderPath)) {
                    var dirInfo = new DirectoryInfo(folderPath);
                    return dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly).Length +
                           dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).Length;
                }
            } catch { }
            return 0;
        }

        private UIElement GetLocationsPanel()
        {
            if (_project == null) return new Grid();

            if (_project.Subfolders.Count > 0)
            {
                var folderVMs = _project.Subfolders.Select(f => {
                    string fullPath = Path.Combine(_project.Path, f);
                    return new FolderViewModel(f, fullPath, GetFolderItemCount(fullPath));
                }).ToList();

                var repeater = new ItemsRepeater {
                    ItemsSource = folderVMs,
                    ItemTemplate = (DataTemplate)Resources["FolderTemplate"],
                    Layout = new UniformGridLayout {
                        MinItemWidth = 300,
                        MinColumnSpacing = 12,
                        MinRowSpacing = 12,
                        ItemsStretch = UniformGridLayoutItemsStretch.Fill
                    },
                    Margin = new Thickness(0, 0, 0, 24)
                };
                return repeater;
            }
            
            return new TextBlock { 
                Text = "No subfolders configured", 
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], 
                HorizontalAlignment = HorizontalAlignment.Center, 
                Margin = new Thickness(0, 40, 0, 40) 
            };
        }

        private void FolderCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard card && card.Tag is string fullPath)
            {
                if (Directory.Exists(fullPath)) Process.Start("explorer.exe", fullPath);
            }
        }

        private void RefreshLocations_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null || !Directory.Exists(_project.Path)) return;

            // Sync subfolders with actual directories on disk
            var diskFolders = Directory.GetDirectories(_project.Path)
                .Select(d => Path.GetFileName(d))
                .OrderBy(n => n)
                .ToList();

            _project.Subfolders = diskFolders;
            ProjectService.UpdateProject(_project);

            // Refresh the current view
            int selectedIndex = ContentSelectorBar.Items.IndexOf(ContentSelectorBar.SelectedItem);
            LoadTabContent(selectedIndex);
        }

        private UIElement GetNotesPanel()
        {
            var panel = new StackPanel { Spacing = 12 };
            var itemsControl = new ItemsControl { ItemTemplateSelector = (ProjectItemTemplateSelector)Resources["ItemSelector"], ItemsSource = _items, ItemsPanel = (ItemsPanelTemplate)Resources["ItemsPanelTemplate"] };
            panel.Children.Add(itemsControl);
            if (_items.Count == 0) panel.Children.Add(new TextBlock { Text = "No notes or tasks yet", Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 40) });
            return panel;
        }

        private UIElement GetFilesPanel()
        {
            var panel = new StackPanel { Spacing = 12 };
            var filesPanel = new StackPanel { Spacing = 8 };
            LoadProjectFilesIntoPanel(filesPanel);
            if (filesPanel.Children.Count > 0) panel.Children.Add(filesPanel);
            else panel.Children.Add(new TextBlock { Text = "No project files", Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 40) });
            return panel;
        }

        private async Task LoadExeIconAsync(string exePath, Image target)
        {
            try {
                if (!File.Exists(exePath)) return;
                var file = await StorageFile.GetFileFromPathAsync(exePath);
                var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 32);
                if (thumb != null) { var bmp = new BitmapImage(); await bmp.SetSourceAsync(thumb.AsStreamForRead().AsRandomAccessStream()); target.Source = bmp; }
            } catch { }
        }

        private async Task LoadFileIconAsync(string filePath, Image target)
        {
            try {
                if (!File.Exists(filePath)) return;
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                var thumb = await file.GetThumbnailAsync(ThumbnailMode.ListView, 32);
                if (thumb != null) { var bmp = new BitmapImage(); await bmp.SetSourceAsync(thumb.AsStreamForRead().AsRandomAccessStream()); target.Source = bmp; }
            } catch { }
        }

        private void LoadProjectFilesIntoPanel(StackPanel targetPanel)
        {
            targetPanel.Children.Clear();
            if (_project == null || !_project.FolderExists) return;

            if (_project.FileLaunchers.Count > 0)
            {
                var launcherExts = new HashSet<string>(_project.FileLaunchers.Keys, StringComparer.OrdinalIgnoreCase);
                var matchingFiles = new List<FileInfo>();
                try {
                    var allFiles = new DirectoryInfo(_project.Path).GetFiles("*.*", SearchOption.AllDirectories);
                    foreach (var file in allFiles) if (launcherExts.Contains(file.Extension.ToLowerInvariant())) matchingFiles.Add(file);
                } catch { }

                if (matchingFiles.Count > 0) {
                    var grouped = matchingFiles.GroupBy(f => f.Extension.ToLowerInvariant()).OrderBy(g => g.Key);
                    bool firstGroup = true;
                    foreach (var group in grouped) {
                        string ext = group.Key;
                        string programPath = _project.FileLaunchers[ext];
                        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, firstGroup ? 0 : 12, 0, 6) };
                        firstGroup = false;
                        var icon = new Image { Width = 18, Height = 18 }; _ = LoadExeIconAsync(programPath, icon);
                        headerPanel.Children.Add(icon);
                        headerPanel.Children.Add(new TextBlock { Text = $"{ext.ToUpperInvariant()} Files — {Path.GetFileNameWithoutExtension(programPath)}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
                        targetPanel.Children.Add(headerPanel);
                        foreach (var file in group) AddFileRowToPanel(targetPanel, file.Name, Path.GetDirectoryName(Path.GetRelativePath(_project.Path, file.FullName)) ?? "", file.FullName, programPath, Path.GetFileNameWithoutExtension(programPath), false);
                    }
                }
            }

            foreach (var relPath in _project.CustomFiles) {
                string full = Path.Combine(_project.Path, relPath);
                if (File.Exists(full)) {
                    var row = new Border { Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(12, 8, 12, 8), CornerRadius = new CornerRadius(6), Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"] };
                    var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var img = new Image { Width = 20, Height = 20, Margin = new Thickness(0, 0, 10, 0) }; g.Children.Add(img); _ = LoadFileIconAsync(full, img);
                    var s = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; s.Children.Add(new TextBlock { Text = Path.GetFileName(relPath), FontSize = 14 }); s.Children.Add(new TextBlock { Text = Path.GetDirectoryName(relPath) ?? "", FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });
                    Grid.SetColumn(s, 1); g.Children.Add(s);
                    var b = new Button { Tag = full + "|", Margin = new Thickness(8, 0, 0, 0) }; b.Content = new TextBlock { Text = "Open" }; b.Click += OpenLauncherFile_Click;
                    Grid.SetColumn(b, 2); g.Children.Add(b);
                    var rb = new Button { Tag = relPath, Style = (Style)Application.Current.Resources["SubtleButtonStyle"] }; rb.Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }; rb.Click += RemoveCustomFile_Click;
                    Grid.SetColumn(rb, 3); g.Children.Add(rb);
                    row.Child = g; targetPanel.Children.Add(row);
                }
            }
        }

        private void AddFileRowToPanel(StackPanel panel, string fileName, string subfolder, string fullPath, string programPath, string programName, bool isCustom)
        {
            var row = new Border { Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(12, 8, 12, 8), CornerRadius = new CornerRadius(6), Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"] };
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var img = new Image { Width = 20, Height = 20, Margin = new Thickness(0, 0, 10, 0) }; g.Children.Add(img); _ = LoadExeIconAsync(programPath, img);
            var s = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; s.Children.Add(new TextBlock { Text = fileName, FontSize = 14 }); s.Children.Add(new TextBlock { Text = subfolder, FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });
            Grid.SetColumn(s, 1); g.Children.Add(s);
            var b = new Button { Tag = fullPath + "|" + programPath, Margin = new Thickness(8, 0, 0, 0) }; b.Content = new TextBlock { Text = $"Open in {programName}" }; b.Click += OpenLauncherFile_Click;
            Grid.SetColumn(b, 2); g.Children.Add(b);
            row.Child = g; panel.Children.Add(row);
        }

        private void OpenLauncherFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag) {
                var parts = tag.Split('|', 2);
                if (parts.Length == 2) {
                    string filePath = parts[0], programPath = parts[1];
                    try {
                        if (!string.IsNullOrEmpty(programPath) && File.Exists(programPath)) Process.Start(new ProcessStartInfo { FileName = programPath, Arguments = $"\"{filePath}\"", UseShellExecute = false });
                        else Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                    } catch { }
                }
            }
        }

        private async void AddCustomFile_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) return;
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder }; picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var files = await picker.PickMultipleFilesAsync();
            if (files != null) {
                foreach (var file in files) {
                    if (file.Path.StartsWith(_project.Path, StringComparison.OrdinalIgnoreCase)) {
                        string rel = Path.GetRelativePath(_project.Path, file.Path);
                        if (!_project.CustomFiles.Contains(rel, StringComparer.OrdinalIgnoreCase)) _project.CustomFiles.Add(rel);
                    } else {
                        string dest = Path.Combine(_project.Path, file.Name);
                        try { if (!File.Exists(dest)) File.Copy(file.Path, dest); if (!_project.CustomFiles.Contains(file.Name)) _project.CustomFiles.Add(file.Name); } catch { }
                    }
                }
                ProjectService.UpdateProject(_project); LoadProjectDetails();
            }
        }

        private void RemoveCustomFile_Click(object sender, RoutedEventArgs e)
        {
            if (_project != null && sender is Button btn && btn.Tag is string rel) { _project.CustomFiles.Remove(rel); ProjectService.UpdateProject(_project); LoadProjectDetails(); }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); }

        private async void EditProject_Click(object sender, RoutedEventArgs e)
        {
            if (_project != null) await ProjectDialogService.ShowEditDialogAsync(_project, this.XamlRoot, LoadProjectDetails);
        }

        private async void DeleteProject_Click(object sender, RoutedEventArgs e)
        {
            if (_project != null) await ProjectDialogService.ShowDeleteConfirmAsync(_project, this.XamlRoot, () => App.MainWindow.Navigate(typeof(ProjectPage)));
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e) { if (_project != null && Directory.Exists(_project.Path)) Process.Start("explorer.exe", _project.Path); }

        
        private void LoadProjectItems() {
            if (_project == null) return;
            _items.Clear(); foreach (var item in _project.Items.OrderByDescending(i => i.CreatedAt)) _items.Add(new ProjectItemViewModel(item));
        }

        private async void AddProjectItem_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) return;
            var itemType = (sender as FrameworkElement)?.Tag?.ToString() == "Todo" ? ProjectItemType.Todo : ProjectItemType.Note;
            
            var contentPanel = new StackPanel { Spacing = 12 };
            
            ComboBox? priorityBox = null;
            if (itemType == ProjectItemType.Todo)
            {
                priorityBox = new ComboBox { Header = "Priority", Width = 450 };
                priorityBox.Items.Add("None");
                priorityBox.Items.Add("Low");
                priorityBox.Items.Add("Medium");
                priorityBox.Items.Add("High");
                priorityBox.SelectedIndex = 0; // Default to None
                contentPanel.Children.Add(priorityBox);
            }

            var input = new TextBox { Width = 450, PlaceholderText = "Enter content...", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
            contentPanel.Children.Add(input);

            var dialog = new ContentDialog {
                Title = $"Add {itemType}", Content = contentPanel, PrimaryButtonText = "Add", CloseButtonText = "Cancel", XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text)) {
                var newItem = new ProjectItem { Content = input.Text, Type = itemType, CreatedAt = DateTime.Now };
                if (itemType == ProjectItemType.Todo && priorityBox != null)
                {
                    newItem.Priority = Enum.Parse<TodoPriority>(priorityBox.SelectedItem.ToString() ?? "Medium");
                }
                _project.Items.Add(newItem);
                ProjectService.UpdateProject(_project); LoadProjectItems();
                
                // Refresh the notes panel to hide the "No notes or tasks yet" message
                int selectedIndex = ContentSelectorBar.Items.IndexOf(ContentSelectorBar.SelectedItem);
                if (selectedIndex == 1) LoadTabContent(1);
            }
        }

        private void TodoCheckbox_Click(object sender, RoutedEventArgs e) {
            if (sender is CheckBox cb && cb.DataContext is ProjectItemViewModel vm) {
                vm.IsCompleted = cb.IsChecked ?? false;
                if (_project != null) ProjectService.UpdateProject(_project);
            }
        }

        private async void EditItem_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.Tag is ProjectItemViewModel vm) {
                var contentPanel = new StackPanel { Spacing = 12 };

                ComboBox? priorityBox = null;
                if (vm.Item.Type == ProjectItemType.Todo)
                {
                    priorityBox = new ComboBox { Header = "Priority", Width = 450 };
                    priorityBox.Items.Add("None");
                    priorityBox.Items.Add("Low");
                    priorityBox.Items.Add("Medium");
                    priorityBox.Items.Add("High");
                    priorityBox.SelectedItem = vm.Item.Priority.ToString();
                    contentPanel.Children.Add(priorityBox);
                }

                var input = new TextBox { Text = vm.Content, Width = 450, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
                contentPanel.Children.Add(input);

                var dialog = new ContentDialog { Title = "Edit Item", Content = contentPanel, PrimaryButtonText = "Save", CloseButtonText = "Cancel", XamlRoot = this.XamlRoot };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary) {
                    vm.Content = input.Text;
                    if (vm.Item.Type == ProjectItemType.Todo && priorityBox != null)
                    {
                        vm.Item.Priority = Enum.Parse<TodoPriority>(priorityBox.SelectedItem.ToString() ?? "Medium");
                        vm.UpdatePriority();
                    }
                    if (_project != null) ProjectService.UpdateProject(_project);
                }
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.Tag is ProjectItemViewModel vm && _project != null) {
                _project.Items.Remove(vm.Item); _items.Remove(vm); ProjectService.UpdateProject(_project);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ProjectItemViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public ProjectItem Item { get; }
        public ProjectItemViewModel(ProjectItem item) { Item = item; }

        public string Content { get => Item.Content; set { Item.Content = value; OnPropertyChanged(nameof(Content)); } }
        public bool IsCompleted { 
            get => Item.IsCompleted; 
            set { 
                Item.IsCompleted = value; 
                OnPropertyChanged(nameof(IsCompleted)); 
                OnPropertyChanged(nameof(TextDecoration)); 
                OnPropertyChanged(nameof(TextColorBrush)); 
            } 
        }
        public string CreatedAtString => Item.CreatedAt.ToString("g");
        public Windows.UI.Text.TextDecorations TextDecoration => IsCompleted ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;
        public Microsoft.UI.Xaml.Media.Brush TextColorBrush => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[IsCompleted ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush"];

        public string PriorityText => Item.Priority.ToString();
        public Microsoft.UI.Xaml.Media.Brush PriorityBackgroundColor => Item.Priority switch {
            TodoPriority.High => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 69, 58)),
            TodoPriority.Medium => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 159, 10)),
            TodoPriority.Low => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 48, 209, 88)),
            _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        public Microsoft.UI.Xaml.Media.Brush PriorityTextColor => Item.Priority switch {
            TodoPriority.High => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 69, 58)),
            TodoPriority.Medium => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 159, 10)),
            TodoPriority.Low => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 48, 209, 88)),
            _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
        };
        public Microsoft.UI.Xaml.Media.Brush PriorityIconColor => PriorityTextColor;
        public Microsoft.UI.Xaml.Visibility PriorityVisibility => Item.Priority == TodoPriority.None ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

        public void UpdatePriority() {
            OnPropertyChanged(nameof(PriorityText));
            OnPropertyChanged(nameof(PriorityBackgroundColor));
            OnPropertyChanged(nameof(PriorityTextColor));
            OnPropertyChanged(nameof(PriorityIconColor));
            OnPropertyChanged(nameof(PriorityVisibility));
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    public class ProjectItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? NoteTemplate { get; set; }
        public DataTemplate? TodoTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item) => SelectTemplate(item) ?? base.SelectTemplateCore(item);
        
        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplate(item) ?? base.SelectTemplateCore(item, container);

        private new DataTemplate? SelectTemplate(object item)
        {
            if (item is ProjectItemViewModel vm)
            {
                return vm.Item.Type == ProjectItemType.Note ? NoteTemplate : TodoTemplate;
            }
            return null;
        }
    }

    public class FolderViewModel
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int ItemCount { get; set; }
        public string ItemCountText => $"{ItemCount} item{(ItemCount == 1 ? "" : "s")}";
        public Uri IconUri => new Uri(ItemCount > 0 ? "ms-appx:///Assets/folder_file.png" : "ms-appx:///Assets/folder_empty.png");

        public FolderViewModel(string name, string path, int itemCount)
        {
            Name = name;
            Path = path;
            ItemCount = itemCount;
        }
    }
}
