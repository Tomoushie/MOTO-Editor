// Moto.Editor/UI/Controls/SmartTooltip.cs
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Moto.Editor.UI.Controls
{
    /// <summary>
    /// Contrat pour générer une explication IA au survol.
    /// </summary>
    public interface ISmartTooltipProvider
    {
        Task<string> ExplainWordAsync(string word, string context);
    }

    /// <summary>
    /// Tooltip intelligent.
    /// Quand l'utilisateur survole un mot dans l'éditeur,
    /// MOTO AI explique ce que c'est, donne un exemple, propose une amélioration.
    ///
    /// Conception :
    /// - debounce via Timer (évite les appels Ollama à chaque pixel)
    /// - popup léger au-dessus du curseur
    /// - annulation automatique si la souris bouge
    /// </summary>
    public class SmartTooltip
    {
        private readonly Control _host;
        private readonly ISmartTooltipProvider _provider;

        private readonly Timer _debounceTimer = new Timer { Interval = 400 };
        private readonly ToolTip _popup = new ToolTip
        {
            AutoPopDelay = 15000,
            InitialDelay = 0,
            ReshowDelay = 0,
            IsBalloon = true,
            BackColor = Color.FromArgb(27, 28, 31),
            ForeColor = Color.FromArgb(230, 232, 236)
        };

        private Point _lastHoverPoint = Point.Empty;
        private string _lastWord = string.Empty;
        private bool _busy;

        /// <summary>
        /// Contexte utilisé pour améliorer l'explication.
        /// Exemple : les 5 lignes autour du mot survolé.
        /// </summary>
        public Func<Point, string> ContextProvider { get; set; }

        /// <summary>
        /// Extrait le mot sous une position donnée.
        /// Doit être fourni par l'éditeur (RichTextBox, custom control, etc.).
        /// </summary>
        public Func<Point, string> WordExtractor { get; set; }

        public SmartTooltip(Control host, ISmartTooltipProvider provider)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            _debounceTimer.Tick += DebounceTimer_Tick;
            _host.MouseMove += Host_MouseMove;
            _host.MouseLeave += (s, e) => _debounceTimer.Stop();
        }

        private void Host_MouseMove(object sender, MouseEventArgs e)
        {
            // Ignore les micro-mouvements pour éviter de spammer.
            if (Math.Abs(e.X - _lastHoverPoint.X) < 4 &&
                Math.Abs(e.Y - _lastHoverPoint.Y) < 4)
            {
                return;
            }

            _lastHoverPoint = e.Location;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            if (WordExtractor == null)
            {
                return;
            }

            var word = WordExtractor(_lastHoverPoint);

            if (string.IsNullOrWhiteSpace(word) || word.Length < 2 || word.Length > 60)
            {
                return;
            }

            if (_busy || string.Equals(word, _lastWord, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastWord = word;
            _busy = true;

            try
            {
                var context = ContextProvider?.Invoke(_lastHoverPoint) ?? string.Empty;
                var explanation = await _provider.ExplainWordAsync(word, context);

                if (!string.IsNullOrWhiteSpace(explanation))
                {
                    // ToolTip WinForms : affiche à la position courante de la souris.
                    _popup.Show(Truncate(explanation, 400), _host, _lastHoverPoint.X + 12, _lastHoverPoint.Y + 18);
                }
            }
            catch
            {
                // Une erreur IA ne doit pas casser l'éditeur.
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Masque le tooltip.
        /// </summary>
        public void Hide()
        {
            _popup.Hide(_host);
            _lastWord = string.Empty;
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max - 1) + "…";
        }
    }

    /// <summary>
    /// Provider Ollama pour Smart Tooltip.
    /// Génère une explication courte, pédagogique, avec exemple.
    /// </summary>
    public class OllamaSmartTooltipProvider : ISmartTooltipProvider
    {
        private readonly Func<string, Task<string>> _generator;

        /// <summary>
        /// Le générateur est injecté pour garder cette classe testable
        /// sans dépendre directement de HttpClient.
        /// </summary>
        public OllamaSmartTooltipProvider(Func<string, Task<string>> generator)
        {
            _generator = generator;
        }

        public async Task<string> ExplainWordAsync(string word, string context)
        {
            var prompt =
                "Tu es MOTO AI, un assistant pédagogique pour débutants.\n" +
                $"L'utilisateur survole le mot ou symbole suivant : '{word}'.\n" +
                "Réponds en 3 parties très courtes :\n" +
                "1. C'est quoi ? (1 phrase simple)\n" +
                "2. Exemple (2-3 lignes max)\n" +
                "3. Astuce (1 phrase)\n" +
                "Langue : français. Pas de jargon inutile.\n\n" +
                $"Contexte autour du mot :\n{Truncate(context, 600)}";

            var answer = await _generator(prompt);
            return string.IsNullOrWhiteSpace(answer) ? $"Pas d'explication disponible pour '{word}'." : answer.Trim();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max - 1) + "…";
        }
    }
}
