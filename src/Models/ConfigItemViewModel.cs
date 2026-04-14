using System.ComponentModel;

namespace BlendHub.Models
{
    public class ConfigItemViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private bool _isExists = true;

        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string TooltipText { get; set; } = string.Empty;
        public bool IsFolder { get; set; }

        public bool IsExists
        {
            get => _isExists;
            set
            {
                if (_isExists != value)
                {
                    _isExists = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExists)));
                }
            }
        }

        public bool IsEnabled 
        { 
            get => _isEnabled; 
            set 
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class TargetVersionViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Version { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string DisplayName => $"Blender {Version}";

        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
