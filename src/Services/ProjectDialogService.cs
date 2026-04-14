using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BlendHub.Models;
using BlendHub.Pages;

namespace BlendHub.Services
{
    public class ProjectDialogService
    {
        public static async Task ShowDeleteConfirmAsync(Project project, XamlRoot xamlRoot, Action? onSuccess = null)
        {
            var deleteFilesCheckBox = new CheckBox 
            { 
                Content = "Move project folder and all its contents to Recycle Bin",
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 13
            };

            var contentStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            contentStack.Children.Add(new TextBlock 
            { 
                Text = $"Are you sure you want to remove '{project.Name}' from your recent projects? This action will remove it from the list.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            });
            contentStack.Children.Add(deleteFilesCheckBox);

            var confirmDialog = new ContentDialog
            {
                Title = "Remove Project?",
                Content = contentStack,
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.None,
                XamlRoot = xamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                CloseButtonStyle = Application.Current.Resources["DefaultButtonStyle"] as Style
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Physical deletion if requested
                if (deleteFilesCheckBox.IsChecked == true && !string.IsNullOrEmpty(project.Path))
                {
                    try
                    {
                        if (Directory.Exists(project.Path))
                        {
                            await Task.Run(() => {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                                    project.Path,
                                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ProjectDialogService] Error moving folder to recycle bin: {ex.Message}");
                        
                        var errorDialog = new ContentDialog
                        {
                            Title = "Deletion Failed",
                            Content = $"Could not move the folder to Recycle Bin. It might be in use by another program.\n\nError: {ex.Message}",
                            CloseButtonText = "OK",
                            XamlRoot = xamlRoot,
                            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                            RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default
                        };
                        await errorDialog.ShowAsync();
                    }
                }

                // Remove from data source
                ProjectService.RemoveProject(project);
                
                // Callback for UI refresh
                onSuccess?.Invoke();
            }
        }

        public static async Task ShowEditDialogAsync(Project project, XamlRoot xamlRoot, Action? onSuccess = null)
        {
            var content = new EditProjectDialogContent(project);
            var dialog = new ContentDialog
            {
                Title = $"Edit: {project.Name}",
                Content = content,
                PrimaryButtonText = "Save Changes",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                CloseButtonStyle = Application.Current.Resources["DefaultButtonStyle"] as Style
            };

            dialog.IsPrimaryButtonEnabled = content.IsValid;
            content.ValidationChanged += (s, args) => { dialog.IsPrimaryButtonEnabled = content.IsValid; };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (content.SelectedVersion != null && !string.IsNullOrEmpty(content.SelectedVersion.Version))
                    project.BlenderVersion = content.SelectedVersion.Version;

                if (!string.IsNullOrEmpty(content.BlendFileName))
                    project.BlendFileName = content.BlendFileName;

                project.Subfolders = content.Folders.Select(f => f.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                project.FileLaunchers = content.Launchers
                    .Where(l => !string.IsNullOrWhiteSpace(l.Extension) && !string.IsNullOrWhiteSpace(l.ProgramPath))
                    .GroupBy(l => l.Extension.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().ProgramPath);

                // Save to data source
                ProjectService.UpdateProject(project);

                // Callback for UI refresh
                onSuccess?.Invoke();
            }
        }
    }
}
