using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BlendHub.Services;
using BlendHub.Models;
using BlendHub.Helpers;

namespace BlendHub.Views
{


    public sealed partial class SyncPage : Page
    {
        private readonly BlenderSettingsService _blenderService = new();
        private ObservableCollection<ConfigItemViewModel> _syncItems = new();
        private ObservableCollection<TargetVersionViewModel> _targetVersions = new();

        public SyncPage()
        {
            this.InitializeComponent();
            ItemsListView.ItemsSource = _syncItems;
            TargetVersionsListView.ItemsSource = _targetVersions;
            LoadVersions();
        }

        private void LoadVersions()
        {
            var versions = _blenderService.GetInstalledVersions();
            SourceVersionComboBox.ItemsSource = versions;
            if (versions.Count > 0)
                SourceVersionComboBox.SelectedIndex = 0;
        }

        private void SourceVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceVersionComboBox.SelectedItem is BlenderVersionInfo sourceInfo)
            {
                RefreshItems(sourceInfo.ConfigPath);
                RefreshTargetVersions(sourceInfo.Version);
            }
            ValidateSyncState();
        }

        private void ValidateSyncState()
        {
            if (StartSyncButton == null || WarningInfoBar == null) return;
            if (SyncProgressBar != null && SyncProgressBar.Opacity > 0) return;

            bool hasSource = SourceVersionComboBox.SelectedItem != null;
            bool hasItems = _syncItems.Any(i => i.IsEnabled);
            bool hasTarget = _targetVersions.Any(v => v.IsSelected);

            if (!hasSource || !hasItems || !hasTarget)
            {
                StartSyncButton.IsEnabled = false;
                
                if (!hasSource)
                {
                    WarningInfoBar.Title = "Missing Source";
                    WarningInfoBar.Message = "Please select a source Blender version.";
                }
                else if (!hasItems)
                {
                    WarningInfoBar.Title = "No Items Selected";
                    WarningInfoBar.Message = "Please select at least one item to sync.";
                }
                else if (!hasTarget)
                {
                    WarningInfoBar.Title = "No Targets Selected";
                    WarningInfoBar.Message = "Please select at least one target Blender version.";
                }

                WarningInfoBar.Severity = InfoBarSeverity.Warning;
                WarningInfoBar.IsOpen = true;
                if (SuccessInfoBar != null) SuccessInfoBar.IsOpen = false;
            }
            else
            {
                StartSyncButton.IsEnabled = true;
                WarningInfoBar.IsOpen = false;
            }
        }

        private void RefreshItems(string versionPath)
        {
            _syncItems.Clear();
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
                vm.PropertyChanged += (s, e) => ValidateSyncState();
                _syncItems.Add(vm);
            }
            ValidateSyncState();
        }

        private void RefreshTargetVersions(string sourceVersion)
        {
            _targetVersions.Clear();
            var allVersions = _blenderService.GetInstalledVersions();
            foreach (var v in allVersions)
            {
                if (v.Version != sourceVersion)
                {
                    var vm = new TargetVersionViewModel
                    {
                        Version = v.Version,
                        Path = v.ConfigPath,
                        IsSelected = false
                    };
                    vm.PropertyChanged += (s, e) => ValidateSyncState();
                    _targetVersions.Add(vm);
                }
            }
            ValidateSyncState();
        }

        private async void StartSyncButton_Click(object sender, RoutedEventArgs e)
        {
            WarningInfoBar.IsOpen = false;
            SuccessInfoBar.IsOpen = false;

            if (SourceVersionComboBox.SelectedItem is not BlenderVersionInfo sourceInfo)
            {
                WarningInfoBar.Title = "No Source Selected";
                WarningInfoBar.Message = "Please select a source Blender version.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            var enabledItems = _syncItems.Where(i => i.IsEnabled).ToList();
            if (enabledItems.Count == 0)
            {
                WarningInfoBar.Title = "No Items Selected";
                WarningInfoBar.Message = "Please enable at least one item to include in the sync.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            var selectedTargets = _targetVersions.Where(v => v.IsSelected).ToList();
            if (selectedTargets.Count == 0)
            {
                WarningInfoBar.Title = "No Target Versions";
                WarningInfoBar.Message = "Please select at least one target Blender version to sync to.";
                WarningInfoBar.IsOpen = true;
                return;
            }

            StartSyncButton.IsEnabled = false;
            SyncProgressBar.Opacity = 1;
            SyncProgressBar.IsIndeterminate = true;

            try
            {
                var items = _syncItems.Select(vm => new Services.BackupItem
                {
                    Name = vm.Name,
                    IsEnabled = vm.IsEnabled,
                    RelativePath = vm.RelativePath,
                    IsFolder = vm.IsFolder
                }).ToList();

                int totalTargets = selectedTargets.Count;
                int currentTarget = 0;

                foreach (var target in selectedTargets)
                {
                    await _blenderService.SyncAsync(sourceInfo.ConfigPath, target.Path, items, (msg, progress) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            StatusText.Text = $"Syncing {target.DisplayName}: {msg}";
                            SyncProgressBar.IsIndeterminate = false;
                            SyncProgressBar.Value = ((double)currentTarget + progress) / totalTargets * 100;
                        });
                    });
                    currentTarget++;
                }

                StatusText.Text = "Sync completed successfully!";
                SuccessInfoBar.Message = $"Settings synced to {selectedTargets.Count} Blender version(s).";
                SuccessInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                WarningInfoBar.Severity = InfoBarSeverity.Error;
                WarningInfoBar.Title = "Sync Failed";
                WarningInfoBar.Message = ex.Message;
                WarningInfoBar.IsOpen = true;
            }
            finally
            {
                StartSyncButton.IsEnabled = true;
                SyncProgressBar.Opacity = 0;
            }
        }
    }
}
