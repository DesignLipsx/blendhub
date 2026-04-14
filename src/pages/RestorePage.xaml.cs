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

namespace BlendHub.Pages
{
    public sealed partial class RestorePage : Page
    {
        private readonly BlenderSettingsService _blenderService = new();
        private ObservableCollection<ConfigItemViewModel> _restoreItems = new();

        public RestorePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ItemsListView.ItemsSource = _restoreItems;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadTargetVersions();

            var backupDir = AppSettingsService.Instance.Settings.BackupDirectory;
            RefreshBackups(backupDir);

            ValidateRestoreState();
        }

        private void LoadTargetVersions()
        {
            var versions = _blenderService.GetInstalledVersions();
            TargetVersionComboBox.ItemsSource = versions;
            if (versions.Count > 0)
                TargetVersionComboBox.SelectedIndex = 0;
        }



        private void RefreshBackups(string backupPath)
        {
            var backups = _blenderService.GetBackups(backupPath);
            BackupNameComboBox.ItemsSource = backups;

            if (backups.Count > 0)
            {
                BackupNameComboBox.SelectedIndex = 0;
                ErrorInfoBar.IsOpen = false;
                EnableAllExpanders(true);
            }
            else
            {
                // Don't show error InfoBar when no backups exist - only show warning
                ErrorInfoBar.IsOpen = false;
                _restoreItems.Clear();
                EnableAllExpanders(false);
            }
        }

        private void BackupNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BackupNameComboBox.SelectedItem is string backupName)
            {
                RefreshItems(backupName);
            }
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

            bool hasLocation = !string.IsNullOrWhiteSpace(AppSettingsService.Instance.Settings.BackupDirectory);
            bool hasBackup = BackupNameComboBox.SelectedItem != null;
            bool hasItems = _restoreItems.Any(i => i.IsEnabled);
            bool hasTarget = TargetVersionComboBox.SelectedItem != null;
            bool hasBackups = BackupNameComboBox.ItemsSource is System.Collections.Generic.IEnumerable<string> backups && backups.Any();

            if (!hasLocation || !hasBackup || !hasItems || !hasTarget)
            {
                StartRestoreButton.IsEnabled = false;

                // Only show warning if there are backups but user hasn't made selections
                if (hasBackups)
                {
                    if (!hasLocation)
                    {
                        WarningInfoBar.Title = "Missing Location";
                        WarningInfoBar.Message = "Please specify a Backup Location.";
                    }
                    else if (!hasBackup)
                    {
                        WarningInfoBar.Title = "Missing Backup";
                        WarningInfoBar.Message = "Please select a backup to restore from.";
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
                    WarningInfoBar.IsClosable = true;
                    WarningInfoBar.IsOpen = true;
                    if (SuccessInfoBar != null) SuccessInfoBar.IsOpen = false;
                    UpdateInfoBarSpacing();
                }
                else
                {
                    // No backups exist - show a simple message
                    WarningInfoBar.Title = "No Backups Available";
                    WarningInfoBar.Message = "No backups found. Please create a backup first using the Backup page.";
                    WarningInfoBar.Severity = InfoBarSeverity.Warning;
                    WarningInfoBar.IsClosable = false;
                    WarningInfoBar.IsOpen = true;
                    if (SuccessInfoBar != null) SuccessInfoBar.IsOpen = false;
                    UpdateInfoBarSpacing();
                }
            }
            else
            {
                StartRestoreButton.IsEnabled = true;
                WarningInfoBar.IsOpen = false;
                WarningInfoBar.IsClosable = true;
            }
        }

        private void RefreshItems(string backupName)
        {
            _restoreItems.Clear();
            var backupRoot = Path.Combine(AppSettingsService.Instance.Settings.BackupDirectory, backupName);
            var items = _blenderService.GetDefaultBackupItems(backupRoot);
            foreach (var item in items)
            {
                var vm = new ConfigItemViewModel
                {
                    Name = item.Name,
                    IsEnabled = item.IsEnabled,
                    IsExists = item.Exists,
                    TooltipText = item.Tooltip, // Pass tooltip info
                    RelativePath = item.RelativePath,
                    IsFolder = item.IsFolder
                };
                vm.PropertyChanged += (s, e) => ValidateRestoreState();
                _restoreItems.Add(vm);
            }

            ValidateRestoreState();
        }

        private async void StartRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WarningInfoBar.IsOpen = false;
            ErrorInfoBar.IsOpen = false;
            SuccessInfoBar.IsOpen = false;

            if (string.IsNullOrEmpty(AppSettingsService.Instance.Settings.BackupDirectory))
            {
                WarningInfoBar.Title = "No Backup Location";
                WarningInfoBar.Message = "Please select a backup location first.";
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
                return;
            }

            if (TargetVersionComboBox.SelectedItem is not BlenderVersionInfo targetInfo)
            {
                WarningInfoBar.Title = "No Target Version";
                WarningInfoBar.Message = "Please select a target Blender version to restore settings to.";
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
                return;
            }

            if (BackupNameComboBox.SelectedItem is not string backupName)
            {
                WarningInfoBar.Title = "No Backup Selected";
                WarningInfoBar.Message = "Please select a backup to restore from.";
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
                return;
            }

            var enabledItems = _restoreItems.Where(i => i.IsEnabled).ToList();
            if (enabledItems.Count == 0)
            {
                WarningInfoBar.Title = "No Items Selected";
                WarningInfoBar.Message = "Please enable at least one item to include in the restore.";
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
                return;
            }

            StartRestoreButton.IsEnabled = false;
            RestoreProgressBar.Opacity = 1;
            RestoreProgressBar.IsIndeterminate = true;

            try
            {
                var backupPath = Path.Combine(AppSettingsService.Instance.Settings.BackupDirectory, backupName);
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
                UpdateInfoBarSpacing();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                ErrorInfoBar.Title = "Restore Failed";
                ErrorInfoBar.Message = ex.Message;
                ErrorInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
            }
            finally
            {
                StartRestoreButton.IsEnabled = true;
                RestoreProgressBar.Opacity = 0;
            }
        }

        private void EnableAllExpanders(bool enable)
        {
            if (ItemsExpander != null)
            {
                ItemsExpander.IsEnabled = enable;
                if (!enable) ItemsExpander.IsExpanded = false;
            }
            if (RestoreDestinationCard != null) RestoreDestinationCard.IsEnabled = enable;
            if (BackupSelectionCard != null) BackupSelectionCard.IsEnabled = enable;
        }
        private void InfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => UpdateInfoBarSpacing();

        private void UpdateInfoBarSpacing()
        {
            bool anyOpen = WarningInfoBar.IsOpen || ErrorInfoBar.IsOpen || SuccessInfoBar.IsOpen;
            InfoBarPanel.Margin = new Thickness(0, 0, 0, anyOpen ? 8 : 0);
        }
    }
}
