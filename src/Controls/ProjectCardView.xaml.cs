using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BlendHub.Models;
using BlendHub.Services;
using BlendHub.Pages;

namespace BlendHub.Controls
{
    public sealed partial class ProjectCardView : UserControl
    {
        private readonly BlenderSettingsService _blenderService = new();

        public static readonly DependencyProperty ProjectProperty =
            DependencyProperty.Register("Project", typeof(Project), typeof(ProjectCardView), new PropertyMetadata(null));

        public Project Project
        {
            get => (Project)GetValue(ProjectProperty);
            set 
            { 
                var oldProject = GetValue(ProjectProperty) as Project;
                SetValue(ProjectProperty, value);
                
                if (value != null)
                {
                    // Reset thumbnail path tracking
                    _currentProjectPath = value.FullBlendPath;
                    
                    // Don't reset thumbnail during refresh - only reset if project actually changed to a different one AND we have no valid thumbnail
                    if (ThumbnailImage != null && oldProject != null && 
                        (value.Name != oldProject.Name || value.Path != oldProject.Path) &&
                        ThumbnailImage.Source == null)
                    {
                        ThumbnailImage.Source = _defaultThumbnail;
                        Debug.WriteLine($"[ProjectCard] Set default thumbnail for new project: {value.Name}");
                    }
                    
                    // Trigger thumbnail loading after a small delay to handle virtualization
                    if (ThumbnailImage != null)
                    {
                        _ = DispatcherQueue.TryEnqueue(async () =>
                        {
                            await Task.Delay(50); // Small delay for virtualization
                            if (ThumbnailImage.IsLoaded)
                            {
                                // Always try to load thumbnail for new projects to ensure we get the actual file thumbnail
                                Debug.WriteLine($"[ProjectCard] Triggering thumbnail load for project: {value.Name}");
                                LoadThumbnailAsync();
                            }
                        });
                    }
                }
            }
        }

        private string _currentProjectPath = string.Empty;
        private static BitmapImage? _defaultThumbnail;

        public ProjectCardView()
        {
            this.InitializeComponent();
            this.Unloaded += ProjectCardView_Unloaded;
            
            // Cache the default thumbnail to avoid repeated loading
            if (_defaultThumbnail == null)
            {
                _defaultThumbnail = new BitmapImage(new Uri("ms-appx:///Assets/blender_logo.png"));
                _defaultThumbnail.DecodePixelWidth = 32;
                _defaultThumbnail.DecodePixelHeight = 32;
            }
        }

        private void ProjectCardView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clear thumbnail when control is unloaded
            if (ThumbnailImage != null)
            {
                ThumbnailImage.Source = null;
            }
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            if (Project == null) return;
            
            // Navigate to detail page using centralized MainWindow
            App.MainWindow.Navigate(typeof(ProjectDetailPage), Project);
        }

        private void Thumbnail_Loaded(object sender, RoutedEventArgs e)
        {
            // Trigger thumbnail loading when the image control is loaded
            if (Project != null)
            {
                Debug.WriteLine($"[ProjectCard] ThumbnailImage loaded, triggering load for: {Project.Name}");
                LoadThumbnailAsync();
            }
        }

        private async void LoadThumbnailAsync()
        {
            if (Project == null) return;
            
            var path = Project.FullBlendPath;
            _currentProjectPath = path;
            
            // Don't set default thumbnail here - let it remain as is to avoid flickering
            if (ThumbnailImage.Source == null)
            {
                Debug.WriteLine($"[ProjectCard] Thumbnail source is null for: {Project.Name} - will load actual thumbnail");
            }

            if (!File.Exists(path)) 
            {
                Debug.WriteLine($"[ProjectCard] Blend file not found: {path}");
                return;
            }

            Windows.Storage.FileProperties.StorageItemThumbnail? thumbnail = null;

            try
            {
                var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
                thumbnail = await storageFile.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 48);
                
                // Check if this card is still for the same project
                if (_currentProjectPath != path || Project?.FullBlendPath != path)
                {
                    Debug.WriteLine($"[ProjectCard] Project changed during thumbnail loading, skipping update");
                    return;
                }

                if (thumbnail != null)
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.DecodePixelWidth = 48;
                    bitmapImage.DecodePixelHeight = 48;
                    await bitmapImage.SetSourceAsync(thumbnail.AsStreamForRead().AsRandomAccessStream());
                    
                    // Final check before setting source
                    if (_currentProjectPath == path && Project?.FullBlendPath == path)
                    {
                        ThumbnailImage.Source = bitmapImage;
                        Debug.WriteLine($"[ProjectCard] Successfully loaded thumbnail for: {Project?.Name ?? "Unknown"}");
                    }
                    else
                    {
                        Debug.WriteLine($"[ProjectCard] Project changed during bitmap creation, skipping update for: {Project?.Name ?? "Unknown"}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[ProjectCard] No thumbnail available for: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectCard] Error loading thumbnail for {path}: {ex.Message}");
                // Only set default thumbnail on error if current source is the default or null
                if (_currentProjectPath == path && Project?.FullBlendPath == path && 
                    (ThumbnailImage.Source == null || IsDefaultThumbnail()))
                {
                    ThumbnailImage.Source = _defaultThumbnail;
                }
            }
            finally
            {
                thumbnail?.Dispose();
            }
        }

        private bool IsDefaultThumbnail()
        {
            // Check if current thumbnail is the default blender logo
            if (ThumbnailImage?.Source is BitmapImage bitmap)
            {
                try
                {
                    // Check if it's the cached default thumbnail or has the blender logo URI
                    return ReferenceEquals(bitmap, _defaultThumbnail) || 
                           bitmap.UriSource?.ToString().Contains("blender_logo.png") == true;
                }
                catch
                {
                    return false;
                }
            }
            return ThumbnailImage?.Source == null; // Also treat null as default state
        }



        private void RequestRefresh()
        {
            try
            {
                if (App.MainWindow.ContentFrame.Content is Page page)
                {
                    var method = page.GetType().GetMethod("LoadProjects") ?? page.GetType().GetMethod("LoadRecentProjects");
                    if (method != null)
                    {
                        Debug.WriteLine($"[ProjectCard] Invoking refresh method: {method.Name} on page: {page.GetType().Name}");
                        method.Invoke(page, null);
                    }
                    else
                    {
                        Debug.WriteLine($"[ProjectCard] No refresh method found on page: {page.GetType().Name}");
                    }
                }
                else
                {
                    Debug.WriteLine("[ProjectCard] No active page found for refresh");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectCard] Error during refresh: {ex.Message}");
            }
        }
    }
}
