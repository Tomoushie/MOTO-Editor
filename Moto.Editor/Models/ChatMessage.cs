// Moto.Editor/Models/ChatMessage.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using Moto.Core.AI.Generation;

namespace Moto.Editor.Models
{
    /// <summary>Côté de dockage du panneau chat.</summary>
    public enum DockSide { Left, Right }

    /// <summary>
    /// Message de la conversation IA.
    /// ★ CHANGÉ (24/09, chat en flux) : le texte d'une réponse grandit pendant que le modèle écrit — le message prévient donc l'affichage
    /// de chaque changement (INotifyPropertyChanged), au lieu d'être figé à sa création.
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content = string.Empty;
        private bool _isStreaming;
        private string _footnote = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Role { get; set; } = "user"; // user | ai | system

        /// <summary>Le texte affiché. Pour une réponse du modèle : exactement ce qu'il a écrit (c'est ce texte qui est rejoué dans l'historique).</summary>
        public string Content
        {
            get => _content;
            set
            {
                value ??= string.Empty;
                if (_content == value) return;
                _content = value;
                Notify();
                Notify(nameof(StreamingText));
                // Pendant l'écriture, les blocs ne sont pas recalculés (dix fois par seconde, tous les cadres et boutons seraient reconstruits) :
                // un simple texte grandit à la place, et le découpage texte / code est fait une fois, à la fin.
                if (!_isStreaming) Notify(nameof(Segments));
            }
        }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>Pour une question : le message tel qu'envoyé au modèle (question + contexte joint par l'éditeur), rejoué tel quel ensuite.</summary>
        public string? SentContent { get; set; }

        /// <summary>
        /// Vrai pour un vrai échange avec un modèle (la question envoyée et sa réponse complète). Seuls ces messages sont rejoués dans
        /// l'historique : ni les messages d'erreur, ni les réponses de plugins, d'agents ou de diagnostics (le modèle croirait les avoir écrits).
        /// </summary>
        public bool IsModelTurn { get; set; }

        /// <summary>Vrai tant que le modèle écrit cette réponse.</summary>
        public bool IsStreaming
        {
            get => _isStreaming;
            set
            {
                if (_isStreaming == value) return;
                _isStreaming = value;
                Notify();
                Notify(nameof(IsDone));
                Notify(nameof(StreamingText));
                Notify(nameof(Segments));
            }
        }

        public bool IsDone => !_isStreaming;

        /// <summary>Le texte en cours d'écriture, avec un curseur.</summary>
        public string StreamingText => _content.Length == 0 ? "…" : _content + " ▍";

        /// <summary>Petite ligne sous la bulle : modèle, durée, ce que le modèle a lu, ce qui n'a pas été envoyé.</summary>
        public string Footnote
        {
            get => _footnote;
            set
            {
                value ??= string.Empty;
                if (_footnote == value) return;
                _footnote = value;
                Notify();
                Notify(nameof(HasFootnote));
            }
        }

        public bool HasFootnote => _footnote.Length > 0;

        public bool IsUser => Role == "user";

        public string TimeLabel => Timestamp.ToString("HH:mm");

        /// <summary>Couleur de la bulle selon le rôle.</summary>
        public Color BubbleColor => IsUser
            ? Color.FromRgb(0, 122, 204)
            : Role == "system"
                ? Color.FromRgb(60, 50, 20)
                : Color.FromRgb(32, 33, 38);

        /// <summary>Alignement : utilisateur à droite, IA à gauche.</summary>
        public LayoutOptions Alignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;

        /// <summary>
        /// ★ AJOUT (03/09, bouton "copier" manquant sur le code, trouvé par Tom en testant le chantier précédent) : découpe Content en morceaux
        /// texte/code, pour afficher un bloc de code en police mono avec un bouton copier.
        /// ★ CHANGÉ (24/09) : découpage confié à CodeBlocks.Split (Moto.Core, testé) — l'ancien découpage sur « ``` » gardait « csharp Program.cs »
        /// comme première ligne du code (et « Copier » la copiait) dès que la ligne d'ouverture portait un nom de fichier, ce que la consigne du
        /// chat demande désormais. Vide pendant l'écriture (voir Content).
        /// </summary>
        public IReadOnlyList<ChatContentSegment> Segments => _isStreaming ? Array.Empty<ChatContentSegment>() : ParseSegments(_content);

        private static IReadOnlyList<ChatContentSegment> ParseSegments(string content)
        {
            var result = new List<ChatContentSegment>();
            foreach (var part in CodeBlocks.Split(content))
            {
                if (part.Code is { } code)
                {
                    if (code.Code.Length == 0) continue;
                    result.Add(new ChatContentSegment
                    {
                        IsCode = true, Text = code.Code, Language = code.Language, PathHint = code.PathHint, IsComplete = code.IsComplete,
                    });
                }
                else
                {
                    result.Add(new ChatContentSegment { IsCode = false, Text = part.Text });
                }
            }

            if (result.Count == 0) result.Add(new ChatContentSegment { IsCode = false, Text = content });
            return result;
        }
    }

    /// <summary>Un morceau de message : texte normal, ou bloc de code entre ``` .</summary>
    public sealed class ChatContentSegment
    {
        public bool IsCode { get; init; }
        public string Text { get; init; } = string.Empty;

        /// <summary>Bloc de code : langage annoncé (« csharp »…), vide sinon.</summary>
        public string Language { get; init; } = string.Empty;

        /// <summary>Bloc de code : nom de fichier annoncé par le modèle, sinon null.</summary>
        public string? PathHint { get; init; }

        /// <summary>Faux si la réponse s'est arrêtée avant la fin du bloc : le code est incomplet.</summary>
        public bool IsComplete { get; init; } = true;

        /// <summary>En-tête du bloc de code : « csharp · Program.cs », avec un avertissement s'il est incomplet.</summary>
        public string Header
        {
            get
            {
                var parts = new List<string>();
                if (Language.Length > 0) parts.Add(Language);
                if (!string.IsNullOrWhiteSpace(PathHint)) parts.Add(PathHint!);
                if (!IsComplete) parts.Add("⚠ incomplet (réponse coupée)");
                return string.Join(" · ", parts);
            }
        }

        public bool HasHeader => Header.Length > 0;
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
