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


    public sealed partial class BackupPage : Page
    {
        private readonly BlenderSettingsService _blenderService = new();
        private ObservableCollection<ConfigItemViewModel> _backupItems = new();

        public BackupPage()
        {
            this.InitializeComponent();
            ItemsListView.ItemsSource = _backupItems;

            // Set default backup location to Documents/BlendHub
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BlendHub");
            if (!Directory.Exists(defaultPath))
                Directory.CreateDirectory(defaultPath);
            DestinationTextBox.Text = defaultPath;
            
            // Validate initial state
            ValidateBackupState();

            LoadVersions();
        }

        /// <summary>
        /// Exposes the current backup destination for RestorePage to use.
        /// </summary>
        public static string LastBackupDestination { get; private set; } = string.Empty;

        private void LoadVersions()
        {
            var versions = _blenderService.GetInstalledVersions();
            VersionComboBox.ItemsSource = versions;
            if (versions.Count > 0)
                VersionComboBox.SelectedIndex = 0;
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionComboBox.SelectedItem is BlenderVersionInfo info)
            {
                VersionPathText.Text = info.ConfigPath;
                RefreshItems(info.ConfigPath);
            }
        }

        private void RefreshItems(string versionPath)
        {
            _backupItems.Clear();
            var items = _blenderService.GetDefaultBackupItems(versionPath);
            foreach (var item in items)
            {
                var vm = new ConfigItemViewModel
                {
                    Name = item.Name,
                    IsEnabled = item.IsEnabled,
                    RelativePath = item.RelativePath,
                    IsFolder = item.IsFolder
                };
                vm.PropertyChanged += (s, e) => ValidateBackupState();
                _backupItems.Add(vm);
            }
            ValidateBackupState();
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = await WindowHelper.PickFolderAsync();
            if (folder != null)
            {
                DestinationTextBox.Text = folder.Path;
            }
        }

        private void DestinationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateBackupState();
        }

        private void ValidateBackupState()
        {
            if (StartBackupButton == null || WarningInfoBar == null) return;
            if (BackupProgressBar != null && BackupProgressBar.Opacity > 0) return; // Currently backing up

            bool hasLocation = !string.IsNullOrWhiteSpace(DestinationTextBox.Text);
            bool hasItems = _backupItems.Any(i => i.IsEnabled);

            if (!hasLocation || !hasItems)
            {
                StartBackupButton.IsEnabled = false;

                if (!hasLocation)
                {
                    WarningInfoBar.Title = "Missing Location";
                    WarningInfoBar.Message = "Please specify a Backup Destination.";
                }
                else
                {
                    WarningInfoBar.Title = "No Items Selected";
                    WarningInfoBar.Message = "Please select at least one item to include in the backup.";
                }
                
                WarningInfoBar.Severity = InfoBarSeverity.Warning;
                WarningInfoBar.IsOpen = true;
                if (SuccessInfoBar != null) SuccessInfoBar.IsOpen = false;
            }
            else
            {
                StartBackupButton.IsEnabled = true;
                WarningInfoBar.IsOpen = false;
            }
        }

        private async void StartBackupButton_Click(object sender, RoutedEventArgs e)
        {
            WarningInfoBar.IsOpen = false;
            SuccessInfoBar.IsOpen = false;

            if (VersionComboBox.SelectedItem is not BlenderVersionInfo info)
            {
                WarningInfoBar.Title = "No Version Selected";
                WarningInfoBar.Message = "Please select a Blender version to backup.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            if (string.IsNullOrEmpty(DestinationTextBox.Text))
            {
                WarningInfoBar.Title = "No Destination";
                WarningInfoBar.Message = "Please select a backup destination folder.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            var enabledItems = _backupItems.Where(i => i.IsEnabled).ToList();
            if (enabledItems.Count == 0)
            {
                WarningInfoBar.Title = "No Items Selected";
                WarningInfoBar.Message = "Please enable at least one item to include in the backup.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            StartBackupButton.IsEnabled = false;
            BackupProgressBar.Opacity = 1;
            BackupProgressBar.IsIndeterminate = true;

            try
            {
                var items = _backupItems.Select(vm => new Services.BackupItem
                {
                    Name = vm.Name,
                    IsEnabled = vm.IsEnabled,
                    RelativePath = vm.RelativePath,
                    IsFolder = vm.IsFolder
                }).ToList();

                await _blenderService.BackupAsync(info.ConfigPath, DestinationTextBox.Text, items, (msg, progress) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusText.Text = msg;
                        BackupProgressBar.IsIndeterminate = false;
                        BackupProgressBar.Value = progress * 100;
                    });
                });

                // Store last backup location for RestorePage
                LastBackupDestination = DestinationTextBox.Text;

                StatusText.Text = "Backup completed successfully!";
                SuccessInfoBar.Message = $"Your Blender settings have been backed up to {DestinationTextBox.Text}";
                SuccessInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                WarningInfoBar.Severity = InfoBarSeverity.Error;
                WarningInfoBar.Title = "Backup Failed";
                WarningInfoBar.Message = ex.Message;
                WarningInfoBar.IsOpen = true;
            }
            finally
            {
                StartBackupButton.IsEnabled = true;
                BackupProgressBar.Opacity = 0;
            }
        }
    }
}
