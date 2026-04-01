using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using Microsoft.UI.Dispatching;
using WinRT.Interop;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace BlendHub
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Type> _navigationMap = new()
        {
            { "home", typeof(Views.HomePage) },
            { "install", typeof(Views.InstallPage) },
            { "backup", typeof(Views.BackupPage) },
            { "restore", typeof(Views.RestorePage) },
            { "sync", typeof(Views.SyncPage) },
            { "project", typeof(Views.ProjectPage) }
        };

        public NavigationView NavigationView => NavView;

        public MainWindow()
        {
            InitializeComponent();
            SetWindowProperties();

            // Set Homepage as selected by default
            DispatcherQueue queue = DispatcherQueue.GetForCurrentThread();
            queue.TryEnqueue(() =>
            {
                NavView.SelectedItem = HomeItem;
            });
        }

        private void SetWindowProperties()
        {
            this.Title = "BlendHub";
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            // Set the window icon
            AppWindow appWindow = GetAppWindowForCurrentWindow();
            if (appWindow != null)
            {
                appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico"));
                appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }

            // Sync the title bar caption buttons to match the current theme
            SetTitleBarButtonColors();
            RootGrid.ActualThemeChanged += (s, e) => SetTitleBarButtonColors();
        }

        private void SetTitleBarButtonColors()
        {
            AppWindow appWindow = GetAppWindowForCurrentWindow();
            if (appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

                if (RootGrid.ActualTheme == ElementTheme.Dark)
                {
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 120, 120, 120);
                }
                else
                {
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 0, 0, 0);
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 0, 0, 0);
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 130, 130, 130);
                }
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr windowHandle = WindowNative.GetWindowHandle(this);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            return AppWindow.GetFromWindowId(windowId);
        }

        // TitleBar events (delegated from TitleBar control)
        private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }

        // NavigationView events
        private void NavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
            {
                AppTitleBar.IsPaneToggleButtonVisible = false;
            }
            else
            {
                AppTitleBar.IsPaneToggleButtonVisible = true;
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Sync navigation selection
            foreach (var item in _navigationMap)
            {
                if (e.SourcePageType == item.Value)
                {
                    NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag?.ToString() == item.Key);
                    break;
                }
            }
            if (e.SourcePageType == typeof(Views.SettingsPage))
                NavView.SelectedItem = NavView.SettingsItem;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                Navigate(typeof(Views.SettingsPage));
                return;
            }

            if (args.SelectedItem is NavigationViewItem item && item.Tag != null)
            {
                if (_navigationMap.TryGetValue(item.Tag.ToString() ?? "", out var targetPage))
                {
                    Navigate(targetPage);
                }
            }
        }

        // Navigation helper for quick access
        public void Navigate(Type pageType, object? parameter = null)
        {
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType, parameter);
            }
        }
    }
}
