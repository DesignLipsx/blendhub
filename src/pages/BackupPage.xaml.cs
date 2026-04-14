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


    public sealed partial class BackupPage : Page
    {
        private readonly BlenderSettingsService _blenderService = new();
        private ObservableCollection<ConfigItemViewModel> _backupItems = new();
        private ObservableCollection<string> _backups = new();

        public BackupPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ItemsListView.ItemsSource = _backupItems;
            BackupsListView.ItemsSource = _backups;

            // Load settings ensures directory is handled
            var backupDir = AppSettingsService.Instance.Settings.BackupDirectory;
            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);
            
            // Validate initial state
            ValidateBackupState();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadVersions();
            RefreshManageBackupsList();
        }



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
                    IsExists = item.Exists,
                    TooltipText = item.Tooltip, // Pass tooltip info
                    RelativePath = item.RelativePath,
                    IsFolder = item.IsFolder
                };
                vm.PropertyChanged += (s, e) => ValidateBackupState();
                _backupItems.Add(vm);
            }
            ValidateBackupState();
        }



        private void ValidateBackupState()
        {
            if (StartBackupButton == null || WarningInfoBar == null) return;
            if (BackupProgressBar != null && BackupProgressBar.Opacity > 0) return; // Currently backing up

            bool hasLocation = !string.IsNullOrWhiteSpace(AppSettingsService.Instance.Settings.BackupDirectory);
            bool hasItems = _backupItems.Any(i => i.IsEnabled);
            bool hasBackups = _backups.Count > 0;

            if (!hasLocation || !hasItems || !hasBackups)
            {
                if (!hasLocation)
                {
                    StartBackupButton.IsEnabled = false;
                    WarningInfoBar.Title = "Missing Location";
                    WarningInfoBar.Message = "Please specify a Backup Destination.";
                    WarningInfoBar.IsClosable = true;
                }
                else if (!hasItems)
                {
                    StartBackupButton.IsEnabled = false;
                    WarningInfoBar.Title = "No Items Selected";
                    WarningInfoBar.Message = "Please select at least one item to include in the backup.";
                    WarningInfoBar.IsClosable = true;
                }
                else
                {
                    // Has location and items, but no backups created yet
                    StartBackupButton.IsEnabled = true;
                    WarningInfoBar.Title = "No Backups Available";
                    WarningInfoBar.Message = "You haven't created any backups yet.";
                    WarningInfoBar.IsClosable = false;
                }
                
                WarningInfoBar.Severity = InfoBarSeverity.Warning;
                WarningInfoBar.IsOpen = true;
                if (SuccessInfoBar != null) SuccessInfoBar.IsOpen = false;
                UpdateInfoBarSpacing();
            }
            else
            {
                StartBackupButton.IsEnabled = true;
                WarningInfoBar.IsOpen = false;
                WarningInfoBar.IsClosable = true;
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
                UpdateInfoBarSpacing();
                return;
            }

            if (string.IsNullOrEmpty(AppSettingsService.Instance.Settings.BackupDirectory))
            {
                WarningInfoBar.Title = "No Destination";
                WarningInfoBar.Message = "Please select a backup destination folder.";
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
                return;
            }

            var enabledItems = _backupItems.Where(i => i.IsEnabled).ToList();
            if (enabledItems.Count == 0)
            {
                WarningInfoBar.Title = "No Items Selected";
                WarningInfoBar.Message = "Please enable at least one item to include in the backup.";
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
                return;
            }

            // Create and show backup name dialog
            string defaultName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            string versionName = info.Version;
            
            var nameTextBox = new TextBox
            {
                PlaceholderText = "YYYY-MM-DD_HH-mm",
                Text = defaultName
            };
            
            var previewText = new TextBlock
            {
                Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Text = $"Folder name will be: {defaultName}_{versionName}"
            };
            
            var dialogContent = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
            dialogContent.Children.Add(new TextBlock { Text = "Enter a name for this backup:", TextWrapping = TextWrapping.Wrap });
            dialogContent.Children.Add(nameTextBox);
            dialogContent.Children.Add(previewText);

            var dialog = new ContentDialog
            {
                Title = "Name Your Backup",
                Content = dialogContent,
                PrimaryButtonText = "Start Backup",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                CloseButtonStyle = Application.Current.Resources["DefaultButtonStyle"] as Style
            };

            // Update preview and enable/disable primary button based on input
            nameTextBox.TextChanged += (s, e) =>
            {
                var name = nameTextBox.Text.Trim();
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(name);
                previewText.Text = string.IsNullOrWhiteSpace(name) 
                    ? $"Folder name will be: {defaultName}_{versionName}" 
                    : $"Folder name will be: {name}_{versionName}";
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            string backupName = nameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(backupName))
                backupName = defaultName;

            await ExecuteBackupAsync(info, backupName);
        }

        private async Task ExecuteBackupAsync(BlenderVersionInfo info, string backupName)
        {
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

                await _blenderService.BackupAsync(info.ConfigPath, AppSettingsService.Instance.Settings.BackupDirectory, backupName, items, (msg, progress) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusText.Text = msg;
                        BackupProgressBar.IsIndeterminate = false;
                        BackupProgressBar.Value = progress * 100;
                    });
                });

                SuccessInfoBar.Message = $"Your Blender settings have been backed up to {Path.Combine(AppSettingsService.Instance.Settings.BackupDirectory, backupName)}";
                SuccessInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                WarningInfoBar.Severity = InfoBarSeverity.Error;
                WarningInfoBar.Title = "Backup Failed";
                WarningInfoBar.Message = ex.Message;
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
            }
            finally
            {
                StartBackupButton.IsEnabled = true;
                BackupProgressBar.Opacity = 0;
                RefreshManageBackupsList();
            }
        }

        private void RefreshManageBackupsList()
        {
            var backupDir = AppSettingsService.Instance.Settings.BackupDirectory;
            if (string.IsNullOrEmpty(backupDir))
            {
                ManageBackupsExpander.IsEnabled = false;
                ManageBackupsExpander.IsExpanded = false;
                _backups.Clear();
                ValidateBackupState();
                return;
            }

            var backups = _blenderService.GetBackups(backupDir);
            _backups.Clear();
            foreach (var backup in backups)
            {
                _backups.Add(backup);
            }

            bool hasBackups = _backups.Count > 0;
            ManageBackupsExpander.IsEnabled = hasBackups;
            if (!hasBackups) ManageBackupsExpander.IsExpanded = false;

            ValidateBackupState();
        }



        private void OpenBackupFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(AppSettingsService.Instance.Settings.BackupDirectory))
            {
                WarningInfoBar.Title = "No Destination";
                WarningInfoBar.Message = "Please set a backup destination first.";
                WarningInfoBar.IsOpen = true;
                UpdateInfoBarSpacing();
                return;
            }

            _blenderService.OpenBackupFolder(AppSettingsService.Instance.Settings.BackupDirectory);
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string backupName)
            {
                string backupPath = System.IO.Path.Combine(AppSettingsService.Instance.Settings.BackupDirectory, backupName);
                _blenderService.OpenBackupFolder(backupPath);
            }
        }

        private void InfoItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string backupName)
            {
                string backupDir = AppSettingsService.Instance.Settings.BackupDirectory;
                string backupPath = System.IO.Path.Combine(backupDir, backupName);

                if (System.IO.Directory.Exists(backupPath))
                {
                    // Use the existing service logic to check the 10 standard items in this backup folder
                    var trackedItems = _blenderService.GetDefaultBackupItems(backupPath);
                    
                    var statusList = new List<string>();
                    foreach (var item in trackedItems)
                    {
                        // Check if item was actually backed up
                        statusList.Add($"{(item.Exists ? "✓" : "✗")} {item.Name}");
                    }

                    ItemsInfoTeachingTip.Subtitle = string.Join("\n", statusList);

                    ItemsInfoTeachingTip.Target = (FrameworkElement)sender;
                    ItemsInfoTeachingTip.IsOpen = true;
                }
            }
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string backupName)
            {
                await DeleteBackupByName(backupName);
            }
        }

        private async Task DeleteBackupByName(string backupName)
        {
            var deleteDialog = new ContentDialog
            {
                Title = "Delete Backup",
                Content = $"Are you sure you want to delete the backup '{backupName}'? This action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.None,
                XamlRoot = this.XamlRoot,
                CloseButtonStyle = Application.Current.Resources["DefaultButtonStyle"] as Style
            };

            var result = await deleteDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    _blenderService.DeleteBackup(AppSettingsService.Instance.Settings.BackupDirectory, backupName);
                    RefreshManageBackupsList();
                    SuccessInfoBar.Message = $"Backup '{backupName}' deleted successfully.";
                    SuccessInfoBar.IsOpen = true;
                    UpdateInfoBarSpacing();
                }
                catch (Exception ex)
                {
                    WarningInfoBar.Severity = InfoBarSeverity.Error;
                    WarningInfoBar.Title = "Delete Failed";
                    WarningInfoBar.Message = ex.Message;
                    WarningInfoBar.IsOpen = true;
                    UpdateInfoBarSpacing();
                }
            }
        }
        private void InfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => UpdateInfoBarSpacing();

        private void UpdateInfoBarSpacing()
        {
            bool anyOpen = WarningInfoBar.IsOpen || ErrorInfoBar.IsOpen || SuccessInfoBar.IsOpen;
            InfoBarPanel.Margin = new Thickness(0, 0, 0, anyOpen ? 8 : 0);
        }
    }
}
