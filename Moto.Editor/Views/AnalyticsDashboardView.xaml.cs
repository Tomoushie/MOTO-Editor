// Moto.Editor/Views/AnalyticsDashboardView.xaml.cs
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Analytics;

namespace Moto.Editor.Views
{
    public partial class AnalyticsDashboardView : ContentView
    {
        private ProactiveAnalyticsEngine? _analytics;
        private enum TabKind { Top, Suggestions, Heatmap }
        private TabKind _currentTab = TabKind.Top;

        public AnalyticsDashboardView() => InitializeComponent();

        public void SetAnalytics(ProactiveAnalyticsEngine analytics)
        {
            _analytics = analytics;
            RefreshView();
        }

        public void RefreshView()
        {
            ContentArea.Children.Clear();
            switch (_currentTab)
            {
                case TabKind.Top: RenderTopCommands(); break;
                case TabKind.Suggestions: RenderSuggestions(); break;
                case TabKind.Heatmap: RenderHeatmap(); break;
            }
        }

        private void RenderTopCommands()
        {
            if (_analytics == null) return;
            AddSectionHeader("🏆 Top 10 commandes palette");
            foreach (var cmd in _analytics.GetTopPaletteCommands(10))
                AddStatRow(cmd.ItemId.Split('.').Last(), $"{cmd.ExecutedCount}×");
        }

        private void RenderSuggestions()
        {
            if (_analytics == null) return;
            AddSectionHeader("💡 Top suggestions");
            foreach (var s in _analytics.GetTopSuggestions(10))
                AddStatRow(s.ItemId.Split('.').Last(), $"{s.ExecutionRate * 100:F1}%");
            AddSectionHeader("⚠️ À améliorer");
            foreach (var s in _analytics.GetUnderperformingSuggestions(5))
                AddStatRow(s.ItemId.Split('.').Last(), $"{s.ExecutionRate * 100:F1}%");
        }

        private void RenderHeatmap()
        {
            if (_analytics == null) return;
            AddSectionHeader("🔥 Heatmap");
            foreach (var s in _analytics.GetAllStats().OrderByDescending(x => x.ExecutedCount).Take(15))
                AddStatRow(s.ItemId.Split('.').Last(), $"{s.ExecutedCount}");
        }

        private async void OnExportClicked(object? sender, EventArgs e)
        {
            if (_analytics == null) return;
            var data = new { GeneratedUtc = DateTime.UtcNow, Report = _analytics.GetReport(), Stats = _analytics.GetAllStats() };
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MotoEditor");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"analytics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            FooterLabel.Text = $"✅ {Path.GetFileName(path)}";
        }

        private void OnTabTopClicked(object? s, EventArgs e) { _currentTab = TabKind.Top; RefreshView(); }
        private void OnTabSuggestionsClicked(object? s, EventArgs e) { _currentTab = TabKind.Suggestions; RefreshView(); }
        private void OnTabHeatmapClicked(object? s, EventArgs e) { _currentTab = TabKind.Heatmap; RefreshView(); }
        private void OnCloseClicked(object? s, EventArgs e) => IsVisible = false;

        private void AddSectionHeader(string t) => ContentArea.Children.Add(new Label { Text = t, FontAttributes = FontAttributes.Bold, TextColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Accent"] });
        private void AddStatRow(string l, string v)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(8, 4) };
            row.Children.Add(new Label { Text = l, FontSize = 12 });
            var val = new Label { Text = v, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Accent"] };
            Grid.SetColumn(val, 1);
            row.Children.Add(val);
            ContentArea.Children.Add(row);
        }
    }
}
