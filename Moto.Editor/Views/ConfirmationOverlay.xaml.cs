// Moto.Editor/Views/ConfirmationOverlay.xaml.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Settings;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Overlay modal de confirmation pour les actions sensibles de l'IA.
    /// ★ MODIFIÉ (24/09, agent v2) : les détails défilent (hauteur bornée d'après la fenêtre) et, quand
    /// <see cref="ConfirmationRequest.DetailsAreDiff"/> est vrai, s'affichent comme un diff — police fixe, lignes ajoutées en vert,
    /// retirées en rouge. Les boutons restent toujours visibles.
    /// </summary>
    public partial class ConfirmationOverlay : ContentView
    {
        /// <summary>Au-delà, le diff est tronqué avec une mention explicite (un Label par ligne : on garde la carte réactive).</summary>
        internal const int MaxDiffLines = 400;

        private TaskCompletionSource<bool>? _tcs;

        public ConfirmationOverlay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Affiche la demande de confirmation et attend la réponse.
        /// </summary>
        public async Task<bool> ShowAsync(ConfirmationRequest request)
        {
            // Une demande encore ouverte ne devrait pas exister (AiConfirmationService les sérialise) ; si c'est pourtant le cas,
            // elle est refusée plutôt qu'abandonnée : son appelant ne doit jamais rester bloqué pour toujours.
            _tcs?.TrySetResult(false);
            _tcs = new TaskCompletionSource<bool>();

            var (windowWidth, windowHeight) = AvailableSize();

            TitleLabel.Text = request.Title;
            MessageLabel.Text = request.Message;
            ConfirmBtn.Text = request.ConfirmText;
            CancelBtn.Text = request.CancelText;

            Card.WidthRequest = request.DetailsAreDiff ? Math.Clamp(windowWidth * 0.75, 520, 1000) : 480;
            DetailsScroll.MaximumHeightRequest = Math.Max(120, windowHeight * 0.5);
            RenderDetails(request.Details, request.DetailsAreDiff);
            DetailsFrame.IsVisible = !string.IsNullOrWhiteSpace(request.Details);
            _ = DetailsScroll.ScrollToAsync(0, 0, animated: false); // un nouveau diff s'ouvre toujours en haut

            // Couleur destructive (rouge) pour les actions irréversibles.
            // ★ (25/09) : on change de STYLE (et non la couleur de fond en direct) : une couleur posée en code écrasait
            // les états survol/appui du thème.
            if (Application.Current?.Resources.TryGetValue(request.IsDestructive ? "MotoDangerButton" : "MotoPrimaryButton", out var style) == true
                && style is Style buttonStyle)
                ConfirmBtn.Style = buttonStyle;

            IsVisible = true;

            return await _tcs.Task;
        }

        private void OnConfirmClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
            _tcs?.TrySetResult(true);
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
            _tcs?.TrySetResult(false);
        }

        // ── Contenu des détails ─────────────────────────────────────────────

        private void RenderDetails(string details, bool asDiff)
        {
            DetailsHost.Children.Clear();

            var body = Res("FontSizeBody", 13d);
            if (!asDiff)
            {
                DetailsHost.Children.Add(new Label
                {
                    Text = details,
                    FontSize = body,
                    TextColor = Res("Txt2", Color.FromArgb("#9CA3AF")),
                    Padding = new Thickness(6, 4),
                });
                return;
            }

            var lines = details.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            var shown = Math.Min(lines.Length, MaxDiffLines);
            for (var i = 0; i < shown; i++)
                DetailsHost.Children.Add(BuildDiffLine(lines[i]));

            if (lines.Length > shown)
                DetailsHost.Children.Add(new Label
                {
                    Text = $"… {lines.Length - shown} ligne(s) de plus, non affichée(s) ici (la modification est appliquée en entier).",
                    FontSize = Res("FontSizeSmall", 12d),
                    TextColor = Res("Warning", Color.FromArgb("#D9A227")),
                    Padding = new Thickness(6, 6),
                });
        }

        /// <summary>Une ligne du diff « unifié » : « + » ajoutée (vert), « − » retirée (rouge), « @@ » repère de bloc, « ── » nom de fichier.</summary>
        private static Label BuildDiffLine(string line)
        {
            var mono = Res("FontFamilyMono", "Consolas");
            var text = Res("Txt2", Color.FromArgb("#9CA3AF"));
            var strong = Res("Txt1", Color.FromArgb("#E5E7EB"));

            var label = new Label
            {
                Text = line.Length == 0 ? " " : line,
                FontFamily = mono,
                FontSize = 12,
                TextColor = text,
                Padding = new Thickness(6, 1),
                LineBreakMode = LineBreakMode.CharacterWrap,
            };

            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                label.TextColor = Res("Accent", Color.FromArgb("#007ACC"));
            }
            else if (line.StartsWith("── ", StringComparison.Ordinal))
            {
                label.TextColor = strong;
                label.FontAttributes = FontAttributes.Bold;
                label.Padding = new Thickness(6, 6, 6, 2);
            }
            else if (line.Length > 0 && line[0] == '+')
            {
                label.TextColor = strong;
                label.BackgroundColor = Res("SuccessMuted", Color.FromArgb("#2660BF60"));
            }
            else if (line.Length > 0 && line[0] == '-')
            {
                label.TextColor = strong;
                label.BackgroundColor = Res("DangerMuted", Color.FromArgb("#26DC2626"));
            }
            return label;
        }

        // ── Aides ───────────────────────────────────────────────────────────

        /// <summary>Taille de la zone d'affichage : celle de l'overlay une fois mis en page, sinon celle de la fenêtre.</summary>
        private (double Width, double Height) AvailableSize()
        {
            double width = Width > 0 ? Width : 0;
            double height = Height > 0 ? Height : 0;

            if (width <= 0 || height <= 0)
            {
                var window = Application.Current?.Windows.FirstOrDefault();
                if (window is { Width: > 0, Height: > 0 })
                {
                    width = window.Width;
                    height = window.Height;
                }
            }
            return (width > 0 ? width : 1100, height > 0 ? height : 700);
        }

        /// <summary>Ressource du thème (les dictionnaires fusionnés sont parcourus) ; une valeur de repli si elle manque.</summary>
        private static T Res<T>(string key, T fallback)
        {
            var resources = Application.Current?.Resources;
            return resources is not null && resources.TryGetValue(key, out var value) && value is T typed ? typed : fallback;
        }
    }
}
