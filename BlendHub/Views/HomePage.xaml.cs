using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BlendHub.Services;

namespace BlendHub.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly BlenderSettingsService _blenderService = new();

        public HomePage()
        {
            this.InitializeComponent();
            LoadVersions();
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

        private void LaunchBlender_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BlenderVersionInfo info)
            {
                _blenderService.LaunchBlender(info.ExecutablePath);
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BlenderVersionInfo info)
            {
                _blenderService.OpenConfigFolder(info.ConfigPath);
            }
        }

        private void BackupCard_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(BackupPage));
        }

        private void CreateBackupCard_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(BackupPage));
        }

        private void RestoreCard_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(RestorePage));
        }

        private void SyncCard_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SyncPage));
        }

        private void ProjectsCard_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(ProjectPage));
        }

        private void InstallCard_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(InstallPage));
        }
    }
}
