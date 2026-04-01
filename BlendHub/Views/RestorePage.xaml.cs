using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using BlendHub.Services;
using BlendHub.Models;
using BlendHub.Helpers;

namespace BlendHub.Views
{
    public sealed partial class RestorePage : Page
    {
        private readonly BlenderSettingsService _blenderService = new();
        private ObservableCollection<ConfigItemViewModel> _restoreItems = new();

        public RestorePage()
        {
            this.InitializeComponent();
            ItemsListView.ItemsSource = _restoreItems;
            LoadTargetVersions();

            // Auto-populate backup location from BackupPage's last destination
            string defaultPath = BackupPage.LastBackupDestination;
            if (string.IsNullOrEmpty(defaultPath))
            {
                // Fallback to Documents/BlendHub
                defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BlendHub");
            }

            if (Directory.Exists(defaultPath))
            {
                BackupSourceTextBox.Text = defaultPath;
                RefreshBackupVersions(defaultPath);
            }
            else
            {
                WarningInfoBar.Title = "No Backup Location";
                WarningInfoBar.Message = "No backup location is set. Please browse to a folder containing backups, or create a backup first.";
                WarningInfoBar.IsOpen = true;
            }

            ValidateRestoreState();
        }

        private void LoadTargetVersions()
        {
            var versions = _blenderService.GetInstalledVersions();
            TargetVersionComboBox.ItemsSource = versions;
            if (versions.Count > 0)
                TargetVersionComboBox.SelectedIndex = 0;
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = await WindowHelper.PickFolderAsync();
            if (folder != null)
            {
                BackupSourceTextBox.Text = folder.Path;
                WarningInfoBar.IsOpen = false;
                RefreshBackupVersions(folder.Path);
            }
        }

        private void RefreshBackupVersions(string backupPath)
        {
            var versions = _blenderService.GetBackupVersions(backupPath);
            BackupVersionComboBox.ItemsSource = versions;

            if (versions.Count > 0)
            {
                BackupVersionComboBox.SelectedIndex = 0;
                ErrorInfoBar.IsOpen = false;
            }
            else
            {
                ErrorInfoBar.Title = "No Backups Found";
                ErrorInfoBar.Message = $"No backup versions were found at '{backupPath}'. Make sure you've created a backup first.";
                ErrorInfoBar.IsOpen = true;
                _restoreItems.Clear();
                NoItemsText.Visibility = Visibility.Visible;
            }
        }

        private void BackupVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BackupVersionComboBox.SelectedItem is string versionName)
            {
                RefreshItems(versionName);
            }
            ValidateRestoreState();
        }

        private void BackupSourceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateRestoreState();
        }

        private void TargetVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateRestoreState();
        }

        private void ValidateRestoreState()
        {
            if (StartRestoreButton == null || WarningInfoBar == null) return;
            if (RestoreProgressBar != null && RestoreProgressBar.Opacity > 0) return; // Currently restoring

            bool hasLocation = !string.IsNullOrWhiteSpace(BackupSourceTextBox.Text);
            bool hasItems = _restoreItems.Any(i => i.IsEnabled);
            bool hasTarget = TargetVersionComboBox.SelectedItem != null;
            bool hasVersion = BackupVersionComboBox.SelectedItem != null;

            if (!hasLocation || !hasItems || !hasTarget || !hasVersion)
            {
                StartRestoreButton.IsEnabled = false;

                if (!hasLocation)
                {
                    WarningInfoBar.Title = "Missing Location";
                    WarningInfoBar.Message = "Please specify a Backup Location.";
                }
                else if (!hasVersion)
                {
                    WarningInfoBar.Title = "Missing Backup Version";
                    WarningInfoBar.Message = "Please select a backup version to restore from.";
                }
                else if (!hasItems)
                {
                    WarningInfoBar.Title = "No Items Selected";
                    WarningInfoBar.Message = "Please select at least one item to restore.";
                }
                else if (!hasTarget)
                {
                    WarningInfoBar.Title = "No Target Selected";
                    WarningInfoBar.Message = "Please select a target Blender version.";
                }
                
                WarningInfoBar.Severity = InfoBarSeverity.Warning;
                WarningInfoBar.IsOpen = true;
                if (SuccessInfoBar != null) SuccessInfoBar.IsOpen = false;
            }
            else
            {
                StartRestoreButton.IsEnabled = true;
                WarningInfoBar.IsOpen = false;
            }
        }

        private void RefreshItems(string versionName)
        {
            _restoreItems.Clear();
            var backupRoot = Path.Combine(BackupSourceTextBox.Text, "Sync Blender", versionName);
            var items = _blenderService.GetDefaultBackupItems(backupRoot);
            foreach (var item in items)
            {
                var vm = new ConfigItemViewModel
                {
                    Name = item.Name,
                    IsEnabled = item.IsEnabled,
                    RelativePath = item.RelativePath,
                    IsFolder = item.IsFolder
                };
                vm.PropertyChanged += (s, e) => ValidateRestoreState();
                _restoreItems.Add(vm);
            }
            NoItemsText.Visibility = _restoreItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ValidateRestoreState();
        }

        private async void StartRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WarningInfoBar.IsOpen = false;
            ErrorInfoBar.IsOpen = false;
            SuccessInfoBar.IsOpen = false;

            if (string.IsNullOrEmpty(BackupSourceTextBox.Text))
            {
                WarningInfoBar.Title = "No Backup Location";
                WarningInfoBar.Message = "Please select a backup location first.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            if (TargetVersionComboBox.SelectedItem is not BlenderVersionInfo targetInfo)
            {
                WarningInfoBar.Title = "No Target Version";
                WarningInfoBar.Message = "Please select a target Blender version to restore settings to.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            if (BackupVersionComboBox.SelectedItem is not string versionName)
            {
                WarningInfoBar.Title = "No Backup Version";
                WarningInfoBar.Message = "Please select a backup version to restore from.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            var enabledItems = _restoreItems.Where(i => i.IsEnabled).ToList();
            if (enabledItems.Count == 0)
            {
                WarningInfoBar.Title = "No Items Selected";
                WarningInfoBar.Message = "Please enable at least one item to include in the restore.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            StartRestoreButton.IsEnabled = false;
            RestoreProgressBar.Opacity = 1;
            RestoreProgressBar.IsIndeterminate = true;

            try
            {
                var backupPath = Path.Combine(BackupSourceTextBox.Text, "Sync Blender", versionName);
                var items = _restoreItems.Select(vm => new Services.BackupItem
                {
                    Name = vm.Name,
                    IsEnabled = vm.IsEnabled,
                    RelativePath = vm.RelativePath,
                    IsFolder = vm.IsFolder
                }).ToList();

                await _blenderService.RestoreAsync(backupPath, targetInfo.ConfigPath, items, (msg, progress) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusText.Text = msg;
                        RestoreProgressBar.IsIndeterminate = false;
                        RestoreProgressBar.Value = progress * 100;
                    });
                });

                StatusText.Text = "Restore completed successfully!";
                SuccessInfoBar.Message = $"Settings have been restored to {targetInfo.DisplayName}.";
                SuccessInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                ErrorInfoBar.Title = "Restore Failed";
                ErrorInfoBar.Message = ex.Message;
                ErrorInfoBar.IsOpen = true;
            }
            finally
            {
                StartRestoreButton.IsEnabled = true;
                RestoreProgressBar.Opacity = 0;
            }
        }
    }
}
