// Moto.Editor/Models/ChatMessage.cs
using System;
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Models
{
    /// <summary>Côté de dockage du panneau chat.</summary>
    public enum DockSide { Left, Right }

    /// <summary>Message de la conversation IA.</summary>
    public class ChatMessage
    {
        public string Role { get; set; } = "user"; // user | ai | system
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsUser => Role == "user";

        public string TimeLabel => Timestamp.ToString("HH:mm");

        /// <summary>Couleur de la bulle selon le rôle.</summary>
        public Color BubbleColor => IsUser
            ? Color.FromArgb(0, 122, 204)
            : Role == "system"
                ? Color.FromArgb(60, 50, 20)
                : Color.FromArgb(32, 33, 38);

        /// <summary>Alignement : utilisateur à droite, IA à gauche.</summary>
        public LayoutOptions Alignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;
    }

    /// <summary>Élément de contexte attaché à la conversation.</summary>
    public class ChatContextItem
    {
        public string Kind { get; set; } = "file"; // file | folder | selection
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public string Label => Kind switch
        {
            "file" => $"📄 {System.IO.Path.GetFileName(Path)}",
            "folder" => $"📁 {System.IO.Path.GetFileName(Path)}",
            _ => "✂ sélection"
        };
    }
}
