// Moto.Editor/Models/EditorDocument.cs (v2)
using System; // ★ AJOUT (28/09) : StringComparison (réglages tabs_show_close / tabs_close_position).
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

        public string Title { get => _title; set { if (SetField(ref _title, value)) OnFileTypeChanged(); } }
        public string Text { get => _text; set => SetField(ref _text, value); }
        public string Path { get => _path; set { if (SetField(ref _path, value)) OnFileTypeChanged(); } }

        /// <summary>★ AJOUT (25/09) : icône et couleur du type de fichier (onglet de l'éditeur), voir Controls/FileTypeVisual.</summary>
        public string FileGlyph => Moto.Editor.Controls.FileTypeVisual.Glyph(string.IsNullOrEmpty(_path) ? _title : _path);
        public Microsoft.Maui.Graphics.Color FileGlyphColor => Moto.Editor.Controls.FileTypeVisual.Tint(string.IsNullOrEmpty(_path) ? _title : _path);

        private void OnFileTypeChanged()
        {
            OnPropertyChanged(nameof(FileGlyph));
            OnPropertyChanged(nameof(FileGlyphColor));
        }

        private bool _isActive;

        /// <summary>
        /// ★ AJOUT (25/09) : onglet affiché dans l'éditeur (fond éditeur + trait d'accent). Posé par EditorPaneView.SelectTab :
        /// l'état visuel « Selected » du CollectionView ne s'appliquait pas sous Windows (vérifié sur capture).
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (!SetField(ref _isActive, value)) return;
                // ★ AJOUT (28/09) : en mode de fermeture « Hover », l'onglet ACTIF garde sa croix visible (l'onglet survolé
                // l'affiche aussi) — sans ça, la croix ne serait atteignable qu'à la souris, et un survol non détecté
                // rendrait la fermeture impossible. Recalculé ici parce que l'état actif change après un simple clic d'onglet.
                OnPropertyChanged(nameof(ShowCloseLeft));
                OnPropertyChanged(nameof(ShowCloseRight));
            }
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (01/10, décision C item 2) : concept d'« onglet aperçu » (preview tab).
        // Un onglet aperçu est TEMPORAIRE : affiché en italique, il est remplacé par le
        // prochain fichier ouvert en aperçu (au lieu d'ajouter un onglet), et devient
        // définitif dès que l'utilisateur le modifie. Même patron d'état visuel que
        // IsActive/ShowFileGlyph : le modèle est la seule source pour le DataTemplate.
        // ------------------------------------------------------------------

        private bool _isPreview;

        /// <summary>Vrai si l'onglet est un aperçu temporaire (réglages preview_*).</summary>
        public bool IsPreview
        {
            get => _isPreview;
            set
            {
                if (!SetField(ref _isPreview, value)) return;
                OnPropertyChanged(nameof(IsPreviewStyle));
            }
        }

        /// <summary>★ (01/10) : la police du titre passe en italique quand l'onglet est un aperçu.</summary>
        public Microsoft.Maui.Controls.FontAttributes IsPreviewStyle => _isPreview ? Microsoft.Maui.Controls.FontAttributes.Italic : Microsoft.Maui.Controls.FontAttributes.None;

        // ------------------------------------------------------------------
        // ★ AJOUT (28/09) : état visuel de l'onglet piloté par les réglages de la
        // famille « Fenêtre & Layout / Tab Bar » (clés tabs_*). Ces réglages étaient
        // déclarés au catalogue mais lus par AUCUN code : la barre d'onglets les
        // ignorait. Le mappage réglage → propriété vit dans Moto.Editor/Settings/
        // TabBarSettings.cs ; chaque onglet (y compris ceux ouverts plus tard) reçoit
        // sa copie, l'onglet lui-même restant la seule source pour le DataTemplate.
        // ------------------------------------------------------------------

        private bool _showFileGlyph = true;

        /// <summary>Réglage <c>tabs_file_icons</c> : icône de type de fichier dans l'onglet.</summary>
        public bool ShowFileGlyph { get => _showFileGlyph; set => SetField(ref _showFileGlyph, value); }

        private bool _showDiagnosticsBadge = true;

        /// <summary>Réglage <c>tabs_show_diagnostics</c> : pastille erreurs/warnings dans l'onglet.</summary>
        public bool ShowDiagnosticsBadge
        {
            get => _showDiagnosticsBadge;
            set
            {
                if (!SetField(ref _showDiagnosticsBadge, value)) return;
                OnPropertyChanged(nameof(ShowErrorBadge));
            }
        }

        /// <summary>Vrai quand la pastille doit être peinte (erreurs réelles ET réglage actif) — jamais une donnée inventée.</summary>
        public bool ShowErrorBadge => HasErrors && _showDiagnosticsBadge;

        private bool _closeOnLeft;

        /// <summary>Réglage <c>tabs_close_position</c> : la croix de fermeture passe avant le titre.</summary>
        public bool CloseOnLeft
        {
            get => _closeOnLeft;
            set
            {
                if (!SetField(ref _closeOnLeft, value)) return;
                OnPropertyChanged(nameof(ShowCloseLeft));
                OnPropertyChanged(nameof(ShowCloseRight));
            }
        }

        private string _closeMode = "Always";
        private bool _closeHovered;

        /// <summary>
        /// Réglage <c>tabs_show_close</c> : « Always », « Hover » ou « Hidden ». Valeur non reconnue
        /// traitée comme « Always » (on n'éteint jamais une croix sur une valeur inattendue).
        /// </summary>
        public string CloseMode
        {
            get => _closeMode;
            set
            {
                if (!SetField(ref _closeMode, value)) return;
                OnPropertyChanged(nameof(ShowCloseLeft));
                OnPropertyChanged(nameof(ShowCloseRight));
            }
        }

        /// <summary>Survol de l'onglet (mode « Hover » uniquement), posé par EditorPaneView.</summary>
        public void SetCloseHovered(bool hovered)
        {
            if (_closeHovered == hovered) return;
            _closeHovered = hovered;
            OnPropertyChanged(nameof(ShowCloseLeft));
            OnPropertyChanged(nameof(ShowCloseRight));
        }

        private bool CloseVisibleByMode
            => !string.Equals(_closeMode, "Hidden", StringComparison.OrdinalIgnoreCase)
               && (!string.Equals(_closeMode, "Hover", StringComparison.OrdinalIgnoreCase) || _closeHovered || IsActive);

        /// <summary>Croix de fermeture placée AVANT le titre (réglage tabs_close_position = Left).</summary>
        public bool ShowCloseLeft => CloseVisibleByMode && _closeOnLeft;

        /// <summary>Croix de fermeture placée APRÈS le titre (comportement historique, réglage = Right).</summary>
        public bool ShowCloseRight => CloseVisibleByMode && !_closeOnLeft;

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
                    OnPropertyChanged(nameof(ShowErrorBadge)); // ★ AJOUT (28/09) : la pastille dépend aussi du réglage tabs_show_diagnostics.
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
