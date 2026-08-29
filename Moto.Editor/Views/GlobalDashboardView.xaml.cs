// Moto.Editor/Views/GlobalDashboardView.xaml.cs
using System;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Analytics;

namespace Moto.Editor.Views
{
    public partial class GlobalDashboardView : ContentView
    {
        private GlobalUsageEngine? _engine;

        public GlobalDashboardView() => InitializeComponent();

        public void SetEngine(GlobalUsageEngine engine)
        {
            _engine = engine;
            Refresh();
        }

        public void Refresh()
        {
            if (_engine == null) return;
            ContentArea.Children.Clear();
            var s = _engine.Snapshot();

            AddCard("📁 Fichiers", new[]
            {
                ("Créés", $"+{s.FilesCreated}"),
                ("Modifiés", $"~{s.FilesModified}"),
                ("Supprimés", $"-{s.FilesDeleted}")
            });

            AddCard("📝 Lignes de code", new[]
            {
                ("Ajoutées", $"+{s.LinesCreated:N0}"),
                ("Supprimées", $"-{s.LinesDeleted:N0}"),
                ("Net", $"{s.LinesCreated - s.LinesDeleted:+#;-#;0}")
            });

            AddCard("⏱️ Activité", new[]
            {
                ("Temps de travail", FormatDuration(s.TotalWorkSeconds)),
                ("Premier lancement", s.FirstLaunchUtc.ToString("yyyy-MM-dd")),
                ("Dernière activité", s.LastActivityUtc.ToString("yyyy-MM-dd HH:mm"))
            });

            AddCard("🤖 IA", new[]
            {
                ("Appels totaux", s.AiCallsTotal.ToString()),
                ("Tokens consommés", $"{s.TokensConsumed:N0}"),
                ("Modèles utilisés", s.AiCallsByModel.Count.ToString())
            });

            AddCard("🏆 Top modèles",
                s.AiCallsByModel.OrderByDescending(kv => kv.Value).Take(5)
                    .Select(kv => (kv.Key, kv.Value.ToString())).ToArray());

            AddCard("📤 Exports par format",
                s.ExportsByFormat.OrderByDescending(kv => kv.Value).Take(5)
                    .Select(kv => (kv.Key, kv.Value.ToString())).ToArray());

            AddCard("🧰 Activité projet", new[]
            {
                ("Builds", s.BuildsLaunched.ToString()),
                ("Sessions debug", s.DebugSessionsStarted.ToString())
            });
        }

        private static string FormatDuration(long seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h{ts.Minutes:D2}m" : $"{ts.Minutes}m{ts.Seconds:D2}s";
        }

        private void AddCard(string title, (string Label, string Value)[] rows)
        {
            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(12)
            };
            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(new Label { Text = title, FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current.Resources["Accent"] });
            foreach (var (label, value) in rows)
            {
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Padding = new Thickness(4, 2)
                };
                row.Children.Add(new Label { Text = label, FontSize = 12 });
                var v = new Label { Text = value, FontSize = 12, FontAttributes = FontAttributes.Bold };
                Grid.SetColumn(v, 1);
                row.Children.Add(v);
                stack.Children.Add(row);
            }
            card.Content = stack;
            ContentArea.Children.Add(card);
        }

        private void OnCloseClicked(object? s, EventArgs e) => IsVisible = false;
    }
}
