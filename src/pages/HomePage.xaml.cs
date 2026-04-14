using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BlendHub.Services;
using BlendHub.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ComponentModel;

namespace BlendHub.Pages
{
    public class NavigationCardInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = string.Empty;
        public Type TargetPageType { get; set; } = typeof(Page);
    }

    public sealed partial class HomePage : Page, INotifyPropertyChanged
    {
        private readonly BlenderSettingsService _blenderService = new();
        public ObservableCollection<Project> RecentProjects { get; } = new ObservableCollection<Project>();
        public string Greeting => $"Welcome Back, {AppSettingsService.Instance.Settings.UserName}!";
        public List<NavigationCardInfo> NavigationCards { get; }


        public HomePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            NavigationCards = new List<NavigationCardInfo>
            {
                new NavigationCardInfo { Title = "Create Backup", Description = "Save your Blender preferences securely.", IconGlyph = "\uE78C", TargetPageType = typeof(BackupPage) },
                new NavigationCardInfo { Title = "Restore Data", Description = "Recover config from a previous backup.", IconGlyph = "\xE777", TargetPageType = typeof(RestorePage) },
                new NavigationCardInfo { Title = "Sync Versions", Description = "Synchronize settings across versions.", IconGlyph = "\xE895", TargetPageType = typeof(SyncPage) },
                new NavigationCardInfo { Title = "Projects", Description = "Manage your Blender projects natively.", IconGlyph = "\xED25", TargetPageType = typeof(ProjectPage) },
                new NavigationCardInfo { Title = "Download Blender", Description = "Download different Blender versions.", IconGlyph = "\xE896", TargetPageType = typeof(DownloadPage) }
            };
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.Bindings.Update();
            LoadVersions();
            LoadRecentProjects();
        }

        public void LoadRecentProjects()
        {
            try
            {
                var projects = ProjectService.LoadProjects();
                var recentProjects = projects.OrderByDescending(p => p.CreatedAt).Take(5).ToList();
                
                Debug.WriteLine($"[HomePage] Loading {recentProjects.Count} recent projects from {projects.Count} total projects");
                
                RecentProjects.Clear();
                foreach (var project in recentProjects)
                {
                    RecentProjects.Add(project);
                }

                RecentProjectsList.ItemsSource = RecentProjects;
                NoRecentProjectsPanel.Visibility = RecentProjects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                
                Debug.WriteLine($"[HomePage] Recent projects UI updated with {RecentProjects.Count} items");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HomePage] Error loading recent projects: {ex.Message}");
            }
        }

        private void LoadVersions()
        {
            var versions = _blenderService.GetInstalledVersions();
            VersionsGridView.ItemsSource = versions;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadVersions();
        }



        private void NavigationCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NavigationCardInfo info)
            {
                App.MainWindow.Navigate(info.TargetPageType);
            }
        }

        private void ViewAllProjects_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow.Navigate(typeof(ProjectPage));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
