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

namespace BlendHub.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public ObservableCollection<FileLauncher> Launchers { get; } = new ObservableCollection<FileLauncher>();
        public ObservableCollection<ProjectFolder> DefaultFolders { get; } = new ObservableCollection<ProjectFolder>();
        public ObservableCollection<CustomBlenderInfo> CustomBlenders { get; } = new ObservableCollection<CustomBlenderInfo>();

        public SettingsPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            LoadGeneralSettings();
            LoadCurrentTheme();
            LoadLaunchers();
            LoadDefaultFolders();
            LoadCustomBlenders();
        }

        private void LoadGeneralSettings()
        {
            var settings = AppSettingsService.Instance.Settings;
            BackupLocationTextBox.Text = settings.BackupDirectory;
            UserNameTextBox.Text = settings.UserName;
            
            // Set Default Page selection
            foreach (ComboBoxItem item in DefaultPageComboBox.Items)
            {
                if (item.Tag?.ToString() == settings.DefaultPage)
                {
                    DefaultPageComboBox.SelectedItem = item;
                    break;
                }
            }
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

        private void UserNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveUserName();
        }

        private void UserNameTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.Focus(FocusState.Programmatic);
                e.Handled = true;
                SaveUserName();
            }
        }

        private void SaveUserName()
        {
            AppSettingsService.Instance.Settings.UserName = UserNameTextBox.Text;
            AppSettingsService.Instance.Save();
        }

        private void DefaultPageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DefaultPageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                AppSettingsService.Instance.Settings.DefaultPage = tag;
                AppSettingsService.Instance.Save();
            }
        }

        // --- Default Folders ---
        private void LoadDefaultFolders()
        {
            DefaultFolders.Clear();
            var folders = AppSettingsService.Instance.Settings.DefaultFolders;
            for (int i = 0; i < folders.Count; i++)
            {
                var folder = new ProjectFolder($"Folder {i + 1}:", folders[i]);
                folder.PropertyChanged += DefaultFolder_PropertyChanged;
                DefaultFolders.Add(folder);
            }
        }

        private void SaveDefaultFolders()
        {
            var service = AppSettingsService.Instance;
            service.Settings.DefaultFolders = DefaultFolders
                .Select(f => f.Name)
                .ToList();
            service.Save();
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = new ProjectFolder($"Folder {DefaultFolders.Count + 1}:", "");
            folder.PropertyChanged += DefaultFolder_PropertyChanged;
            DefaultFolders.Add(folder);
            SaveDefaultFolders();
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ProjectFolder folder)
            {
                // Force focus away from the list before removal
                this.Focus(FocusState.Programmatic);

                folder.PropertyChanged -= DefaultFolder_PropertyChanged;
                DefaultFolders.Remove(folder);
                UpdateFolderLabels();
                SaveDefaultFolders();
            }
        }

        private void UpdateFolderLabels()
        {
            for (int i = 0; i < DefaultFolders.Count; i++)
            {
                DefaultFolders[i].Label = $"Folder {i + 1}:";
            }
        }

        private void DefaultFolder_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectFolder.Name))
            {
                SaveDefaultFolders();
            }
        }

        private void FolderNameTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.Focus(FocusState.Programmatic);
                e.Handled = true;
                SaveDefaultFolders();
            }
        }

        private void FolderNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveDefaultFolders();
        }

        // --- File Launchers ---
        private void LoadLaunchers()
        {
            Launchers.Clear();
            var defaultLaunchers = AppSettingsService.Instance.Settings.DefaultLaunchers;
            foreach (var kvp in defaultLaunchers)
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
        }

        private void SaveLaunchers()
        {
            var service = AppSettingsService.Instance;
            service.Settings.DefaultLaunchers.Clear();
            foreach (var launcher in Launchers)
            {
                if (!string.IsNullOrWhiteSpace(launcher.Extension))
                {
                    string ext = launcher.Extension.StartsWith(".") ? launcher.Extension : "." + launcher.Extension;
                    service.Settings.DefaultLaunchers[ext.ToLowerInvariant()] = launcher.ProgramPath ?? "";
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

        private void ExtensionTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.Focus(FocusState.Programmatic);
                e.Handled = true;
                SaveLaunchers();
            }
        }

        private void ExtensionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveLaunchers();
        }

        private void ContentRoot_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            this.Focus(FocusState.Programmatic);
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

        private async void BrowseBackupLocation_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var window = App.MainWindow;
            if (window != null)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                BackupLocationTextBox.Text = folder.Path;
                AppSettingsService.Instance.Settings.BackupDirectory = folder.Path;
                AppSettingsService.Instance.Save();
            }
        }

        // --- Custom Blender Installations ---
        private void LoadCustomBlenders()
        {
            CustomBlenders.Clear();
            var paths = AppSettingsService.Instance.Settings.CustomBlenderPaths;
            foreach (var path in paths)
            {
                var info = new CustomBlenderInfo(path);
                info.PropertyChanged += CustomBlender_PropertyChanged;
                CustomBlenders.Add(info);
            }
        }

        private void SaveCustomBlenders()
        {
            AppSettingsService.Instance.Settings.CustomBlenderPaths = CustomBlenders
                .Select(b => b.Path)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            AppSettingsService.Instance.Save();
        }

        private void CustomBlender_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SaveCustomBlenders();
        }

        private async void AddCustomBlender_Click(object sender, RoutedEventArgs e)
        {
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
                var info = new CustomBlenderInfo(file.Path);
                info.PropertyChanged += CustomBlender_PropertyChanged;
                CustomBlenders.Add(info);
                SaveCustomBlenders();
            }
        }

        private async void BrowseCustomBlender_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CustomBlenderInfo info) return;

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
                info.Path = file.Path;
                SaveCustomBlenders();
            }
        }

        private void RemoveCustomBlender_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is CustomBlenderInfo info)
            {
                info.PropertyChanged -= CustomBlender_PropertyChanged;
                CustomBlenders.Remove(info);
                SaveCustomBlenders();
            }
        }

        private async void PrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Privacy Policy",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Content = BuildPrivacyPolicyContent()
            };
            await dialog.ShowAsync();
        }

        private async void TermsOfService_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Terms of Use",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Content = BuildTermsContent()
            };
            await dialog.ShowAsync();
        }

        private static UIElement BuildPrivacyPolicyContent()
        {
            var root = new StackPanel { Spacing = 16 };

            root.Children.Add(new TextBlock
            {
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Text = "Last update: April 9, 2026"
            });

            root.Children.Add(Wrap("The privacy policy describes how, what and why some data might be required to be collected. By using BlendHub, you acknowledge and consent to the practices described below."));
            root.Children.Add(Header("User data"));
            root.Children.Add(Wrap("This application doesn\u2019t store, collect, use or share any personal/analytics data. All user data including project information, settings, and preferences are stored locally on your device in the AppData\\Roaming\\BlendHub folder."));
            root.Children.Add(Header("Internet connection"));
            root.Children.Add(Wrap("This app requires internet connection only for the following features:"));
            var netList = new StackPanel { Margin = new Microsoft.UI.Xaml.Thickness(16, 0, 0, 0), Spacing = 4 };
            netList.Children.Add(new TextBlock { Text = "\u2022 Downloading Blender versions from blender.org" });
            netList.Children.Add(new TextBlock { Text = "\u2022 Fetching available Blender version information" });
            root.Children.Add(netList);
            root.Children.Add(Wrap("You can use the app without an internet connection but the download features will not work properly."));
            root.Children.Add(Header("Other websites"));
            root.Children.Add(Wrap("Some features may contain links to other websites (such as blender.org or GitHub) that are not operated by the developer. If you click on a third party link, you will be directed to that third party\u2019s site. You are strongly advised to review the Privacy Policy of every site you visit."));
            root.Children.Add(Header("Changes"));
            root.Children.Add(Wrap("The privacy policy may suffer changes from time to time to reflect updates made to the app. When this Policy is changed in a material manner, you\u2019ll be informed by updating the \u201cLast update\u201d section."));

            return new ScrollViewer { MaxHeight = 400, Padding = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 8), Content = root };
        }

        private static UIElement BuildTermsContent()
        {
            var root = new StackPanel { Spacing = 16 };

            root.Children.Add(Wrap("By downloading and installing BlendHub, you acknowledge that you have read, understood and agreed the following terms of use. If you don\u2019t agree with these terms, you may not install or use this software."));
            root.Children.Add(Wrap("This software product is supplied \u201cas-is\u201d. The publisher or the author assumes no liability for damages, direct or consequential, which may result from the use of this software."));
            root.Children.Add(Header("Grant of license"));
            root.Children.Add(Wrap("You are granted the right to install and use copies of this product on your computers for any personal non-commercial purposes only."));
            root.Children.Add(Header("Limitations"));
            root.Children.Add(Wrap("The following limitations are applied to this software product:"));
            var limList = new StackPanel { Margin = new Microsoft.UI.Xaml.Thickness(16, 0, 0, 0), Spacing = 4 };
            limList.Children.Add(new TextBlock { Text = "\u2022 you may not reverse engineer, decompile, or disassemble it" });
            limList.Children.Add(new TextBlock { Text = "\u2022 you may not rent, lease, or lend it" });
            limList.Children.Add(new TextBlock { Text = "\u2022 you may not include parts of it in your software without the publisher's permission" });
            limList.Children.Add(new TextBlock { Text = "\u2022 you may not alter or modify it in any way or create a new installer for it" });
            root.Children.Add(limList);
            root.Children.Add(Header("Third party trademarks"));
            root.Children.Add(Wrap("Blender is a registered trademark of the Blender Foundation. This application is not affiliated with or endorsed by the Blender Foundation."));
            root.Children.Add(Header("Termination"));
            root.Children.Add(Wrap("Without prejudice to any other rights, the author may terminate this License Agreement if you fail to comply with the terms and conditions of this agreement. In such event, you must destroy all copies of the software."));

            return new ScrollViewer { MaxHeight = 400, Padding = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 8), Content = root };
        }

        private static TextBlock Header(string text) =>
            new TextBlock { Text = text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };

        private static TextBlock Wrap(string text) =>
            new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
    }
}
