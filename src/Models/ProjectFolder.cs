using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlendHub.Models
{
    public class ProjectFolder : INotifyPropertyChanged
    {
        private string _name;
        private string _label;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ProjectFolder(string label, string name)
        {
            _label = label;
            _name = name;
        }

        public string Label
        {
            get => _label;
            set
            {
                if (_label != value)
                {
                    _label = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CustomBlenderInfo : INotifyPropertyChanged
    {
        private string _path = string.Empty;

        public string Path
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged(nameof(Path));
                }
            }
        }

        public CustomBlenderInfo(string path)
        {
            Path = path;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
