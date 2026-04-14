using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlendHub.Models
{
    public class FileLauncher : INotifyPropertyChanged
    {
        private string _extension = string.Empty;
        private string _programPath = string.Empty;
        private string _programName = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Extension
        {
            get => _extension;
            set { if (_extension != value) { _extension = value; OnPropertyChanged(); } }
        }

        public string ProgramPath
        {
            get => _programPath;
            set { if (_programPath != value) { _programPath = value; OnPropertyChanged(); } }
        }

        public string ProgramName
        {
            get => _programName;
            set { if (_programName != value) { _programName = value; OnPropertyChanged(); } }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
