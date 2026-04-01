using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;
using BlendHub.Models;
using BlendHub.Services;

namespace BlendHub.Views
{
    public sealed partial class SettingsPage : Page
    {
        public ObservableCollection<FileLauncher> Launchers { get; } = new ObservableCollection<FileLauncher>();
        private bool _isInitializing = false;

        public SettingsPage()
        {
            this.InitializeComponent();
            _isInitializing = true;
            LoadCurrentTheme();
            LoadLaunchers();
            _isInitializing = false;
        }

        private void LoadCurrentTheme()
        {
            var currentTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default;
            ThemeComboBox.SelectedIndex = currentTheme switch
            {
                ElementTheme.Default => 0,
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                ElementTheme theme = tag switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                if (App.MainWindow.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = theme;
                }
            }
        }

        // --- File Launchers ---
        private void LoadLaunchers()
        {
            Launchers.Clear();
            foreach (var kvp in LauncherSettingsService.Instance.Launchers)
            {
                Launchers.Add(new FileLauncher
                {
                    Extension = kvp.Key,
                    ProgramPath = kvp.Value,
                    ProgramName = System.IO.Path.GetFileNameWithoutExtension(kvp.Value)
                });
            }
            
            // Add default .psd entry if no launchers exist
            if (Launchers.Count == 0)
            {
                Launchers.Add(new FileLauncher
                {
                    Extension = ".psd",
                    ProgramPath = "",
                    ProgramName = ""
                });
            }
            
            LaunchersListControl.ItemsSource = Launchers;
        }

        private void SaveLaunchers()
        {
            var service = LauncherSettingsService.Instance;
            service.Launchers.Clear();
            foreach (var launcher in Launchers)
            {
                if (!string.IsNullOrWhiteSpace(launcher.Extension) && !string.IsNullOrWhiteSpace(launcher.ProgramPath))
                {
                    string ext = launcher.Extension.StartsWith(".") ? launcher.Extension : "." + launcher.Extension;
                    service.Launchers[ext.ToLowerInvariant()] = launcher.ProgramPath;
                }
            }
            service.Save();
        }

        private void AddLauncher_Click(object sender, RoutedEventArgs e)
        {
            Launchers.Add(new FileLauncher());
        }

        private void RemoveLauncher_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is FileLauncher launcher)
            {
                Launchers.Remove(launcher);
                SaveLaunchers();
            }
        }

        private async void BrowseLauncherProgram_Click(object sender, RoutedEventArgs e)
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
                SaveLaunchers();
            }
        }
    }
}
