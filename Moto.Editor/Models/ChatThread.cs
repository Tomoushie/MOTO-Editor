// Moto.Editor/Models/ChatThread.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Moto.Editor.Models
{
    /// <summary>
    /// Une conversation IA (thread), comme dans les IDE à agent.
    /// Chaque thread garde son historique, son titre et ses horodatages.
    /// </summary>
    public class ChatThread : INotifyPropertyChanged
    {
        private string _title = "Nouvelle conversation";

        public Guid Id { get; } = Guid.NewGuid();

        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
        public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public string TimeLabel => CreatedAtUtc.ToString("HH:mm");

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
