using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BlendHub.Models;
using BlendHub.Services;

namespace BlendHub.Controls
{
    public sealed partial class BlenderLaunchButton : UserControl
    {
        private readonly BlenderSettingsService _blenderService = new();

        public static readonly DependencyProperty ProjectProperty =
            DependencyProperty.Register("Project", typeof(Project), typeof(BlenderLaunchButton), new PropertyMetadata(null));

        public Project Project
        {
            get => (Project)GetValue(ProjectProperty);
            set => SetValue(ProjectProperty, value);
        }

        public static readonly DependencyProperty BlenderVersionInfoProperty =
            DependencyProperty.Register("BlenderVersionInfo", typeof(BlenderVersionInfo), typeof(BlenderLaunchButton), new PropertyMetadata(null));

        public BlenderVersionInfo BlenderVersionInfo
        {
            get => (BlenderVersionInfo)GetValue(BlenderVersionInfoProperty);
            set => SetValue(BlenderVersionInfoProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(BlenderLaunchButton), new PropertyMetadata("Open"));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register("IconGlyph", typeof(string), typeof(BlenderLaunchButton), new PropertyMetadata("\uE8DA"));

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public static readonly DependencyProperty ShowManageOptionsProperty =
            DependencyProperty.Register("ShowManageOptions", typeof(bool), typeof(BlenderLaunchButton), new PropertyMetadata(false));

        public bool ShowManageOptions
        {
            get => (bool)GetValue(ShowManageOptionsProperty);
            set => SetValue(ShowManageOptionsProperty, value);
        }

        public event Action? RequestRefresh;

        public BlenderLaunchButton()
        {
            this.InitializeComponent();
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            if (Project != null)
            {
                LaunchProject(Project);
            }
            else if (BlenderVersionInfo != null)
            {
                _blenderService.LaunchBlender(BlenderVersionInfo.ExecutablePath);
            }
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            var flyout = sender as MenuFlyout;
            if (flyout == null) return;

            flyout.Items.Clear();

            if (Project != null)
            {
                PopulateProjectFlyout(flyout);
            }
            else if (BlenderVersionInfo != null)
            {
                var openFolderItem = new MenuFlyoutItem { Text = "Open Config Folder", Icon = new FontIcon { Glyph = "\uED25" } };
                openFolderItem.Click += (s, args) => _blenderService.OpenConfigFolder(BlenderVersionInfo.ConfigPath);
                flyout.Items.Add(openFolderItem);
            }
        }

        private void PopulateProjectFlyout(MenuFlyout flyout)
        {
            var versions = _blenderService.GetInstalledVersions();
            if (!versions.Any())
            {
                flyout.Items.Add(new MenuFlyoutItem { Text = "No Blender versions found", IsEnabled = false });
            }
            else
            {
                foreach (var version in versions)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = $"Open with {version.DisplayName}",
                        Tag = version
                    };

                    if (version.Version == Project.BlenderVersion)
                    {
                        item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                        item.Text += " (Default)";
                    }

                    item.Click += (s, args) =>
                    {
                        if (s is MenuFlyoutItem mi && mi.Tag is BlenderVersionInfo info)
                        {
                            LaunchProject(Project, info);
                        }
                    };
                    flyout.Items.Add(item);
                }
            }

            if (ShowManageOptions)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());

                var editItem = new MenuFlyoutItem { Text = "Edit Project", Icon = new FontIcon { Glyph = "\uE70F" } };
                editItem.Click += async (s, args) =>
                    await ProjectDialogService.ShowEditDialogAsync(Project, this.XamlRoot, () => RequestRefresh?.Invoke());
                flyout.Items.Add(editItem);

                var deleteItem = new MenuFlyoutItem { Text = "Delete Project", Icon = new FontIcon { Glyph = "\uE74D" } };
                deleteItem.Click += async (s, args) =>
                    await ProjectDialogService.ShowDeleteConfirmAsync(Project, this.XamlRoot, () => RequestRefresh?.Invoke());
                flyout.Items.Add(deleteItem);
            }
        }

        private void LaunchProject(Project project, BlenderVersionInfo? specificVersion = null)
        {
            try
            {
                if (File.Exists(project.FullBlendPath))
                {
                    var versionToUse = specificVersion;
                    if (versionToUse == null)
                    {
                        var versions = _blenderService.GetInstalledVersions();
                        versionToUse = versions.FirstOrDefault(v => v.Version == project.BlenderVersion);
                    }

                    if (versionToUse != null && !string.IsNullOrEmpty(versionToUse.ExecutablePath) && File.Exists(versionToUse.ExecutablePath))
                    {
                        Process.Start(new ProcessStartInfo(versionToUse.ExecutablePath, $"\"{project.FullBlendPath}\"")
                        {
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo(project.FullBlendPath)
                        {
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BlenderLaunchButton] Error opening project: {ex.Message}");
            }
        }
    }
}
