// Moto.Editor/Views/CortexView.xaml.cs
using System;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Cortex;

namespace Moto.Editor.Views
{
    /// <summary>
    /// UI du Cortex Engine : mémoire cognitive, mode adaptatif,
    /// conventions apprises, suggestions proactives.
    /// </summary>
    public partial class CortexView : ContentView
    {
        private readonly CortexEngine? _engine;

        /// <summary>Déclenché quand l'utilisateur change de mode.</summary>
        public event Action<CortexBehaviorMode> ModeChanged;

        // ★ CORRECTION (30/08) : engine désormais nullable. MainPage.WirePanels()
        // construit ce panneau AVANT qu'un dossier de travail soit ouvert (donc sans
        // moteur réel) ; MainPage.RebindPanels() le reconstruit avec le vrai moteur
        // une fois un dossier ouvert. Le déréférencement inconditionnel de `engine`
        // ici plantait au tout premier lancement (NullReferenceException) — c'est ce
        // qui bloquait le démarrage de MOTO Editor.
        public CortexView(CortexEngine? engine)
        {
            InitializeComponent();
            _engine = engine;

            if (_engine is null) return;

            _engine.BehaviorChanged += config => MainThread.BeginInvokeOnMainThread(() =>
                UpdateBehaviorUI(config.Mode));

            _engine.MemoryUpdated += _ => MainThread.BeginInvokeOnMainThread(Refresh);

            UpdateBehaviorUI(_engine.CurrentBehavior.Mode);
            Refresh();
        }

        /// <summary>Rafraîchit l'affichage des stats et suggestions.</summary>
        public void Refresh()
        {
            if (_engine is null) return;

            var stats = _engine.GetStats();

            StatsLabel.Text =
                $"Mémoire : {stats.TotalHabits} habitudes · " +
                $"{stats.TotalPatterns} patterns · " +
                $"{stats.TotalCorrections} corrections · " +
                $"{stats.TotalConventions} conventions";

            // Conventions apprises (top 5)
            var conventions = _engine.GetStats(); // placeholder : via GetNamingConventions
            var convList = new System.Collections.Generic.Dictionary<string, string>();
            // Récupération directe via la mémoire (simplifié)
            try
            {
                var mem = typeof(CortexEngine)
                    .GetField("_memory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(_engine) as CortexMemory;

                if (mem != null)
                {
                    var convs = mem.GetNamingConventions();
                    convList = convs;
                }
            }
            catch { }

            if (convList.Count > 0)
            {
                ConventionsLabel.Text = "Conventions : " +
                    string.Join(" · ", convList.Select(kv => $"{kv.Key}={kv.Value}"));
            }
        }

        /// <summary>Charge les suggestions pour un fichier actif.</summary>
        public void LoadSuggestions(string filePath, string content)
        {
            if (_engine is null) return;

            var suggestions = _engine.GetSuggestions(filePath, content);
            SuggestionsList.ItemsSource = suggestions;
        }

        private void OnModeClicked(object sender, EventArgs e)
        {
            if (_engine is null) return;

            var mode =
                sender == BtnBeginner ? CortexBehaviorMode.Beginner :
                sender == BtnExpert ? CortexBehaviorMode.Expert :
                sender == BtnTurbo ? CortexBehaviorMode.Turbo :
                sender == BtnUltra ? CortexBehaviorMode.Ultra :
                CortexBehaviorMode.Balanced;

            _engine.SetBehaviorMode(mode);
            ModeChanged?.Invoke(mode);
        }

        private void UpdateBehaviorUI(CortexBehaviorMode mode)
        {
            // Reset couleurs
            BtnBeginner.BackgroundColor = Colors.Transparent;
            BtnBalanced.BackgroundColor = Colors.Transparent;
            BtnExpert.BackgroundColor = Colors.Transparent;
            BtnTurbo.BackgroundColor = Colors.Transparent;
            BtnUltra.BackgroundColor = Colors.Transparent;

            // Highlight du mode actif
            var active = mode switch
            {
                CortexBehaviorMode.Beginner => BtnBeginner,
                CortexBehaviorMode.Expert => BtnExpert,
                CortexBehaviorMode.Turbo => BtnTurbo,
                CortexBehaviorMode.Ultra => BtnUltra,
                _ => BtnBalanced
            };

            active.BackgroundColor = Color.FromArgb("#0078CC");
        }
    }
}
