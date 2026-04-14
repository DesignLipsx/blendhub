using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using BlendHub.Models;
using Windows.Storage;
using Windows.System;

namespace BlendHub.Pages
{
    public sealed partial class DownloadDialog : UserControl
    {
        private List<WindowsInstaller> _allInstallers = new();
        private string _versionId = string.Empty;
        private string _baseUrl = string.Empty;
        private static readonly HttpClient _httpClient = new();

        public DownloadDialog()
        {
            this.InitializeComponent();
            this.Loaded += OnDialogLoaded;
        }

        private void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            // Apply initial filters after UI is loaded
            ApplyFilters();
        }

        public void Initialize(string versionId, string fullVersion, string releaseDate, bool isLatest, List<WindowsInstaller> installers, string sourceBaseUrl)
        {
            _versionId = versionId;
            _allInstallers = installers ?? new List<WindowsInstaller>();
            _baseUrl = sourceBaseUrl;

            // Set header info
            DialogVersionText.Text = $"Blender {fullVersion}";

            // Filters will be applied automatically when dialog loads
        }

        private void ApplyFilters()
        {
            var filtered = _allInstallers.AsEnumerable();

            // Get selected platform with null check
            string platformTag = "all";
            if (DialogPlatformComboBox?.SelectedItem is ComboBoxItem selectedPlatform)
            {
                platformTag = selectedPlatform.Tag?.ToString() ?? "all";
            }

            // Get selected type with null check
            string typeTag = "all";
            if (DialogTypeComboBox?.SelectedItem is ComboBoxItem selectedType)
            {
                typeTag = selectedType.Tag?.ToString() ?? "all";
            }

            // Filter by platform
            if (platformTag != "all")
            {
                filtered = filtered.Where(i =>
                {
                    var platform = GetPlatformFromFilename(i.Filename);
                    return platformTag.Contains(platform);
                });
            }

            // Filter by type
            if (typeTag != "all")
            {
                filtered = filtered.Where(i =>
                {
                    var ext = Path.GetExtension(i.Filename)?.ToLower();
                    return ext == typeTag;
                });
            }

            var resultList = filtered.ToList();

            // Update UI with null check
            if (DialogInstallersList != null)
                DialogInstallersList.ItemsSource = resultList;
            
            if (DialogNoInstallersMessage != null)
                DialogNoInstallersMessage.Visibility = resultList.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private string GetPlatformFromFilename(string filename)
        {
            var lower = filename.ToLower();
            if (lower.Contains("arm64") || lower.Contains("aarch64"))
                return "windows-arm64";
            if (lower.Contains("x64") || lower.Contains("64") || lower.Contains("amd64"))
                return "windows-x64";
            if (lower.Contains("x86") || lower.Contains("32") || lower.Contains("i686"))
                return "windows32";
            return "windows-x64"; // Default to x64
        }

        private void DialogPlatformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void DialogTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private async void DialogViewDownloadPage_Click(object sender, RoutedEventArgs e)
        {
            var url = $"https://download.blender.org/release/Blender{_versionId}/";
            await Launcher.LaunchUriAsync(new Uri(url));
        }

        private async void DialogInstallerDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WindowsInstaller installer)
            {
                // Download the file instead of opening in browser
                if (!string.IsNullOrEmpty(installer.Url))
                {
                    await DownloadFileAsync(installer.Url, installer.Filename);
                }
            }
        }

        private async Task DownloadFileAsync(string url, string filename)
        {
            try
            {
                // Get Downloads folder
                var downloadsFolder = await StorageFolder.GetFolderFromPathAsync(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads");
                
                // Create destination file
                var destinationFile = await downloadsFolder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);
                
                // Show initial progress
                DispatcherQueue.TryEnqueue(() =>
                {
                    DialogProgressPanel.Visibility = Visibility.Visible;
                    DialogProgressBar.Value = 0;
                    DialogProgressText.Text = $"Starting download of {filename}...";
                });
                
                // Download with progress reporting
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var contentLength = response.Content.Headers.ContentLength ?? 0;
                var totalBytesRead = 0L;
                var buffer = new byte[8192];
                var lastProgressUpdate = 0;
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = await destinationFile.OpenStreamForWriteAsync();
                
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    
                    // Update progress every 5% or every 1MB
                    var progress = contentLength > 0 ? (double)totalBytesRead / contentLength : 0;
                    var progressPercent = (int)(progress * 100);
                    
                    if (progressPercent > lastProgressUpdate && (progressPercent - lastProgressUpdate >= 5 || totalBytesRead - lastProgressUpdate * contentLength / 100 >= 1 * 1024 * 1024))
                    {
                        lastProgressUpdate = progressPercent;
                        
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            DialogProgressPanel.Visibility = Visibility.Visible;
                            DialogProgressBar.Value = progressPercent;
                            DialogProgressText.Text = $"Downloading {filename}: {FormatBytes(totalBytesRead)} of {FormatBytes(contentLength)} ({progressPercent}%)";
                        });
                    }
                }
                
                // Ensure file stream is properly flushed and closed
                await fileStream.FlushAsync();
                
                // Show completion with action buttons
                DispatcherQueue.TryEnqueue(() =>
                {
                    DialogProgressPanel.Visibility = Visibility.Visible;
                    DialogProgressBar.Value = 100;
                    
                    // Create completion panel with buttons
                    var completionPanel = new StackPanel { Spacing = 8 };
                    completionPanel.Children.Add(new TextBlock 
                    { 
                        Text = $"Download complete: {filename} saved to Downloads folder.", 
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                    });
                    
                    var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    
                    var openFileBtn = new Button 
                    { 
                        Content = "Open Installer", 
                        Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                        Tag = destinationFile.Path
                    };
                    openFileBtn.Click += (s, e) => {
                        try {
                            Process.Start(new ProcessStartInfo { FileName = destinationFile.Path, UseShellExecute = true });
                        } catch { }
                    };
                    
                    var openFolderBtn = new Button 
                    { 
                        Content = "Open Folder", 
                        Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                        Tag = Path.GetDirectoryName(destinationFile.Path)
                    };
                    openFolderBtn.Click += (s, e) => {
                        try {
                            Process.Start("explorer.exe", Path.GetDirectoryName(destinationFile.Path));
                        } catch { }
                    };
                    
                    buttonPanel.Children.Add(openFileBtn);
                    buttonPanel.Children.Add(openFolderBtn);
                    completionPanel.Children.Add(buttonPanel);
                    
                    DialogProgressText.Text = "";
                    
                    // Replace the progress text with the completion panel
                    var parent = DialogProgressText.Parent as StackPanel;
                    if (parent != null)
                    {
                        var textIndex = parent.Children.IndexOf(DialogProgressText);
                        if (textIndex >= 0)
                        {
                            parent.Children.RemoveAt(textIndex);
                            parent.Children.Insert(textIndex, completionPanel);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                // Show error
                DispatcherQueue.TryEnqueue(() =>
                {
                    DialogProgressPanel.Visibility = Visibility.Visible;
                    DialogProgressBar.Value = 0;
                    DialogProgressText.Text = $"Download failed: {ex.Message}";
                });
            }
        }
        
        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1) { number /= 1024; counter++; }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }

        private void DownloadDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Cancel button clicked - just close dialog
        }
    }
}
