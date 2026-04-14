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
using BlendHub.Pages;

namespace BlendHub
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Type> _navigationMap = new()
        {
            { "home", typeof(HomePage) },
            { "download", typeof(DownloadPage) },
            { "backup", typeof(BackupPage) },
            { "restore", typeof(RestorePage) },
            { "sync", typeof(SyncPage) },
            { "project", typeof(ProjectPage) }
        };

        public NavigationView NavigationView => NavView;
        public Frame ContentFrame => this.ContentFrameInternal;

        public MainWindow()
        {
            InitializeComponent();
            SetWindowProperties();

            DispatcherQueue queue = DispatcherQueue.GetForCurrentThread();
            queue.TryEnqueue(() =>
            {
                var defaultPage = Services.AppSettingsService.Instance.Settings.DefaultPage;
                var itemToSelect = NavView.MenuItems.OfType<NavigationViewItem>()
                                    .FirstOrDefault(i => i.Tag?.ToString() == defaultPage) ?? HomeItem;
                NavView.SelectedItem = itemToSelect;
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
            if (e.SourcePageType == typeof(SettingsPage))
                NavView.SelectedItem = NavView.SettingsItem;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItem is NavigationViewItem item && item.Tag != null)
            {
                var tag = item.Tag.ToString();
                if (_navigationMap.TryGetValue(tag ?? "", out var targetPage))
                {
                    Navigate(targetPage);
                }
            }
        }

        private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            // Check if the clicked item is the Feedback button using its Tag
            if (args.InvokedItemContainer != null && args.InvokedItemContainer.Tag?.ToString() == "feedback")
            {
                // Show feedback dialog without changing navigation selection
                ShowFeedbackDialog();
            }
        }

        private async void ShowFeedbackDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Feedback",
                XamlRoot = this.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                RequestedTheme = (this.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                Width = 380
            };

            // Create content layout
            var contentPanel = new StackPanel { Spacing = 16, Width = 340 };

            // Button grid
            var buttonGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 8
            };

            // Feature Request button
            var featureBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uEA80", FontSize = 20 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12)
            };
            ToolTipService.SetToolTip(featureBtn, "Feature Request");
            Grid.SetColumn(featureBtn, 0);

            // Bug Report button
            var bugBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uEBE8", FontSize = 20 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12)
            };
            ToolTipService.SetToolTip(bugBtn, "Bug Report");
            Grid.SetColumn(bugBtn, 1);

            // Question button
            var questionBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE897", FontSize = 20 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12)
            };
            ToolTipService.SetToolTip(questionBtn, "Question");
            Grid.SetColumn(questionBtn, 2);

            // Discussion button
            var discussionBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE8BD", FontSize = 20 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12)
            };
            ToolTipService.SetToolTip(discussionBtn, "Discussion");
            Grid.SetColumn(discussionBtn, 3);

            buttonGrid.Children.Add(featureBtn);
            buttonGrid.Children.Add(bugBtn);
            buttonGrid.Children.Add(questionBtn);
            buttonGrid.Children.Add(discussionBtn);

            // Email text box with copy button
            var emailGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            var emailTextBox = new TextBox
            {
                Text = "blendhub.app@gmail.com",
                IsReadOnly = true,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 32
            };
            Grid.SetColumn(emailTextBox, 0);

            var copyBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE8C8", FontSize = 16 },
                Padding = new Thickness(6, 0, 6, 0),
                Height = 32,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ToolTipService.SetToolTip(copyBtn, "Copy email address");
            copyBtn.Click += async (s, e) =>
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText("blendhub.app@gmail.com");
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                
                // Change icon to checkmark for feedback
                var originalIcon = copyBtn.Content as FontIcon;
                if (originalIcon != null)
                {
                    originalIcon.Glyph = "\uE73E"; // Checkmark icon
                    
                    // Wait for 1 second then restore original icon
                    await System.Threading.Tasks.Task.Delay(1000);
                    originalIcon.Glyph = "\uE8C8"; // Copy icon
                }
            };
            Grid.SetColumn(copyBtn, 1);

            emailGrid.Children.Add(emailTextBox);
            emailGrid.Children.Add(copyBtn);

            // Description
            var descriptionText = new TextBlock
            {
                Text = "For any question, new feature request, suggestion or bug report, please send a message at the above email address.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush
            };

            contentPanel.Children.Add(buttonGrid);
            contentPanel.Children.Add(emailGrid);
            contentPanel.Children.Add(descriptionText);

            dialog.Content = contentPanel;
            dialog.CloseButtonText = "Close";

            await dialog.ShowAsync();
        }

        // Navigation helper for quick access
        public void Navigate(Type pageType, object? parameter = null)
        {
            if (ContentFrameInternal.CurrentSourcePageType != pageType)
            {
                ContentFrameInternal.Navigate(pageType, parameter);
            }
        }
    }
}
