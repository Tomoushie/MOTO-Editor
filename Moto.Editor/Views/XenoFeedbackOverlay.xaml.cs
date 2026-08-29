// Moto.Editor/Views/XenoFeedbackOverlay.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Snake2000.Engine.AgentIntegrated.Learning;
using Snake2000.Engine.AgentIntegrated.Pipeline;

namespace Moto.Editor.Views
{
    public partial class XenoFeedbackOverlay : ContentView
    {
        private XenoPipelineV5? _pipeline;
        private PipelineResult? _lastResult;

        public XenoFeedbackOverlay()
        {
            InitializeComponent();
        }

        public void SetPipeline(XenoPipelineV5 pipeline)
        {
            _pipeline = pipeline;
        }

        public void ShowResult(PipelineResult result)
        {
            _lastResult = result;
            IsVisible = true;

            SummaryLabel.Text = $"🎯 {result.TotalFindings} finding(s) · " +
                                $"Confiance moyenne : {result.AverageConfidence:P0}";

            FindingsContainer.Children.Clear();

            foreach (var agentResult in result.Results)
            {
                foreach (var finding in agentResult.Findings)
                {
                    FindingsContainer.Children.Add(BuildFindingCard(agentResult, finding));
                }
            }
        }

        private Border BuildFindingCard(Snake2000.Engine.AgentIntegrated.Specialized.AgentResult agent, string finding)
        {
            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(10)
            };

            var stack = new VerticalStackLayout { Spacing = 6 };

            // Agent + confiance
            var header = new HorizontalStackLayout { Spacing = 6 };
            header.Children.Add(new Label
            {
                Text = $"{GetAgentIcon(agent.AgentName)} {agent.AgentName}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Accent"]
            });
            header.Children.Add(new Label
            {
                Text = $"({agent.Confidence:P0})",
                FontSize = 10,
                TextColor = (Color)Application.Current.Resources["Txt2"],
                VerticalOptions = LayoutOptions.Center
            });
            stack.Children.Add(header);

            // Finding
            stack.Children.Add(new Label
            {
                Text = finding,
                FontSize = 12,
                TextColor = (Color)Application.Current.Resources["Txt1"]
            });

            // Boutons feedback : 👍 👎 ✏️
            var actions = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };

            var upBtn = new Button
            {
                Text = "👍 Utile",
                BackgroundColor = Color.FromArgb("#10B981"),
                TextColor = Colors.White,
                FontSize = 10,
                Padding = new Thickness(8, 4)
            };

            var downBtn = new Button
            {
                Text = "👎 Pas pertinent",
                BackgroundColor = Color.FromArgb("#EF4444"),
                TextColor = Colors.White,
                FontSize = 10,
                Padding = new Thickness(8, 4)
            };

            var editBtn = new Button
            {
                Text = "✏️ Modifier",
                BackgroundColor = (Color)Application.Current.Resources["BgHover"],
                TextColor = (Color)Application.Current.Resources["Txt1"],
                FontSize = 10,
                Padding = new Thickness(8, 4)
            };

            var agentName = agent.AgentName;
            var findingText = finding;

            upBtn.Clicked += (s, e) => RecordFeedback(agentName, findingText, FeedbackKind.Accepted, upBtn, downBtn, editBtn);
            downBtn.Clicked += (s, e) => RecordFeedback(agentName, findingText, FeedbackKind.Rejected, upBtn, downBtn, editBtn);
            editBtn.Clicked += async (s, e) => await EditFeedbackAsync(agentName, findingText, upBtn, downBtn, editBtn);

            actions.Children.Add(upBtn);
            actions.Children.Add(downBtn);
            actions.Children.Add(editBtn);
            stack.Children.Add(actions);

            card.Content = stack;
            return card;
        }

        private void RecordFeedback(string agentName, string finding, FeedbackKind kind,
            Button upBtn, Button downBtn, Button editBtn)
        {
            _pipeline?.RecordFeedback(agentName, finding, kind);

            // Désactive les boutons et affiche la confirmation
            upBtn.IsEnabled = downBtn.IsEnabled = editBtn.IsEnabled = false;
            upBtn.Text = kind == FeedbackKind.Accepted ? "✅ Merci" : "👍";
            downBtn.Text = kind == FeedbackKind.Rejected ? "✅ Noté" : "👎";
            editBtn.Text = "✏️";
        }

        private async System.Threading.Tasks.Task EditFeedbackAsync(string agentName, string finding,
            Button upBtn, Button downBtn, Button editBtn)
        {
            var modification = await Application.Current!.MainPage!.DisplayPromptAsync(
                "Modifier la suggestion",
                "Comment auriez-vous formulé cette suggestion ?",
                "Valider",
                "Annuler",
                initialValue: finding);

            if (!string.IsNullOrWhiteSpace(modification))
            {
                _pipeline?.RecordFeedback(agentName, finding, FeedbackKind.Modified, modification);
                upBtn.IsEnabled = downBtn.IsEnabled = editBtn.IsEnabled = false;
                editBtn.Text = "✅ Modifié";
            }
        }

        private static string GetAgentIcon(string agentName) => agentName.ToLowerInvariant() switch
        {
            "architecture" => "🏛️",
            "performance" => "⚡",
            "security" => "🔒",
            "testing" => "🧪",
            _ => "🤖"
        };

        private void OnCloseClicked(object? sender, EventArgs e) => IsVisible = false;
    }
}
