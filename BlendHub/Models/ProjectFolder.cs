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
}
