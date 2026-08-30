// Moto.Editor/Models/EditorDocument.cs (v2)
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Moto.Editor.Models
{
    public class EditorDocument : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _text = string.Empty;
        private string _path = string.Empty;
        private int _errorCount;

        public string Title { get => _title; set => SetField(ref _title, value); }
        public string Text { get => _text; set => SetField(ref _text, value); }
        public string Path { get => _path; set => SetField(ref _path, value); }

        /// <summary>Nombre d'erreurs de diagnostic (badge rouge sur l'onglet).</summary>
        public int ErrorCount
        {
            get => _errorCount;
            set
            {
                if (SetField(ref _errorCount, value))
                {
                    OnPropertyChanged(nameof(HasErrors));
                    OnPropertyChanged(nameof(ErrorBadge));
                }
            }
        }

        public bool HasErrors => _errorCount > 0;
        public string ErrorBadge => _errorCount.ToString();

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
