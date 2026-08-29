// Moto.Editor/Views/DebugPanelView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Debug;

namespace Moto.Editor.Views
{
    public partial class DebugPanelView : ContentView
    {
        private DebugEngine _engine;
        private readonly List<BreakpointInfo> _breakpoints = new();
        private readonly List<string> _watchExpressions = new();

        /// <summary>Déclenché quand un breakpoint est atteint → naviguer dans l'éditeur.</summary>
        public event Action<string, int>? BreakpointHit;

        public DebugPanelView()
        {
            InitializeComponent();
        }

        public void SetEngine(DebugEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));

            _engine.OutputReceived += OnDebugOutput;
            _engine.BreakpointHit += OnBreakpointHit;
            _engine.SessionEnded += OnSessionEnded;
        }

        public async void StartSession(DebugSession session)
        {
            if (_engine == null)
            {
                StatusLabel.Text = "❌ Moteur de debug non initialisé.";
                return;
            }

            StatusLabel.Text = "Démarrage de la session…";
            var ok = await _engine.StartAsync(session);

            if (ok)
            {
                StatusLabel.Text = "✅ Session démarrée.";
                EnableControls();
            }
            else
            {
                StatusLabel.Text = "❌ Échec du démarrage.";
            }
        }

        public void AddBreakpoint(string filePath, int line)
        {
            _breakpoints.Add(new BreakpointInfo
            {
                Id = _breakpoints.Count + 1,
                FilePath = filePath,
                Line = line,
                Verified = false
            });
            StatusLabel.Text = $"📍 Breakpoint ajouté : {System.IO.Path.GetFileName(filePath)}:{line}";
        }

        private async void OnContinueClicked(object sender, EventArgs e)
        {
            if (_engine == null) return;
            await _engine.ContinueAsync();
            StatusLabel.Text = "▶ Continue…";
        }

        private async void OnStepOverClicked(object sender, EventArgs e)
        {
            if (_engine == null) return;
            await _engine.StepOverAsync();
            StatusLabel.Text = "⏭ Step over…";
        }

        private async void OnStepIntoClicked(object sender, EventArgs e)
        {
            if (_engine == null) return;
            await _engine.StepIntoAsync();
            StatusLabel.Text = "⏬ Step into…";
        }

        private async void OnStopClicked(object sender, EventArgs e)
        {
            if (_engine == null) return;
            await _engine.StopAsync();
            DisableControls();
            StatusLabel.Text = "⏹ Session arrêtée.";
        }

        private void OnAddWatchClicked(object sender, EventArgs e)
        {
            var expr = WatchEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(expr)) return;

            _watchExpressions.Add(expr);
            WatchEntry.Text = string.Empty;
            RenderVariables();
        }

        private void OnDebugOutput(string output)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OutputLabel.Text = output.Length > 500 ? output.Substring(0, 500) + "…" : output;
            });
        }

        private void OnBreakpointHit(BreakpointInfo bp)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = $"⏸ Breakpoint : {System.IO.Path.GetFileName(bp.FilePath)}:{bp.Line}";
                BreakpointHit?.Invoke(bp.FilePath, bp.Line);
                _ = LoadStackTraceAsync();
            });
        }

        private void OnSessionEnded()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisableControls();
                StatusLabel.Text = "Session terminée.";
            });
        }

        private async System.Threading.Tasks.Task LoadStackTraceAsync()
        {
            if (_engine == null) return;

            var frames = await _engine.GetStackTraceAsync();
            CallStackList.Children.Clear();

            foreach (var frame in frames.Take(10))
            {
                var label = new Label
                {
                    Text = $"{frame.Name} ({frame.Line})",
                    FontSize = 11,
                    TextColor = (Color)Application.Current.Resources["Txt1"],
                    Padding = new Thickness(4, 2)
                };
                CallStackList.Children.Add(label);
            }
        }

        private void RenderVariables()
        {
            VariablesList.Children.Clear();
            foreach (var expr in _watchExpressions)
            {
                var row = new Label
                {
                    Text = $"• {expr} = ?",
                    FontSize = 11,
                    TextColor = (Color)Application.Current.Resources["Txt2"]
                };
                VariablesList.Children.Add(row);
            }
        }

        private void EnableControls()
        {
            ContinueBtn.IsEnabled = true;
            StepOverBtn.IsEnabled = true;
            StepIntoBtn.IsEnabled = true;
            StopBtn.IsEnabled = true;
        }

        private void DisableControls()
        {
            ContinueBtn.IsEnabled = false;
            StepOverBtn.IsEnabled = false;
            StepIntoBtn.IsEnabled = false;
            StopBtn.IsEnabled = false;
        }
    }
}
