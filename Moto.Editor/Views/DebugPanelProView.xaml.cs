// Moto.Editor/Views/DebugPanelProView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Debug;

namespace Moto.Editor.Views
{
    public partial class DebugPanelProView : ContentView
    {
        private NetcoredbgAdapter? _adapter;
        private BreakpointManager? _breakpointManager;
        private readonly List<WatchExpression> _watches = new();
        private enum TabKind { Variables, Watch, CallStack, Breakpoints }
        private TabKind _currentTab = TabKind.Variables;
        private int _currentThreadId = 1;

        public event Action<string, int>? BreakpointHit;
        public event Action<string, int>? ToggleBreakpointRequested;

        public DebugPanelProView() => InitializeComponent();

        public void SetServices(NetcoredbgAdapter adapter, BreakpointManager bpManager)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _breakpointManager = bpManager ?? throw new ArgumentNullException(nameof(bpManager));

            _adapter.Stopped += OnDebugStopped;
            _adapter.Terminated += OnDebugTerminated;
            _adapter.OutputReceived += OnDebugOutput;

            _breakpointManager.BreakpointsChanged += bps =>
            {
                if (_currentTab == TabKind.Breakpoints)
                    MainThread.BeginInvokeOnMainThread(RefreshView);
            };
        }

        public async void LaunchDebug(DebugSession session)
        {
            if (_adapter == null)
            {
                StatusLabel.Text = "❌ DAP non initialisé";
                return;
            }

            StatusLabel.Text = "Lancement…";
            var ok = await _adapter.LaunchAsync(session);
            StatusLabel.Text = ok ? "✅ Session démarrée" : "❌ Échec";
            if (ok) EnableControls();
        }

        // ── Contrôles ──
        private async void OnContinueClicked(object? s, EventArgs e)
        {
            if (_adapter == null) return;
            StatusLabel.Text = "▶ Continue";
            await _adapter.ContinueAsync(_currentThreadId);
        }

        private async void OnStepOverClicked(object? s, EventArgs e)
        {
            if (_adapter == null) return;
            StatusLabel.Text = "⏭ Step over";
            await _adapter.NextAsync(_currentThreadId);
        }

        private async void OnStepIntoClicked(object? s, EventArgs e)
        {
            if (_adapter == null) return;
            StatusLabel.Text = "⏬ Step into";
            await _adapter.StepInAsync(_currentThreadId);
        }

        private async void OnStepOutClicked(object? s, EventArgs e)
        {
            if (_adapter == null) return;
            StatusLabel.Text = "⏫ Step out";
            await _adapter.StepOutAsync(_currentThreadId);
        }

        private async void OnStopClicked(object? s, EventArgs e)
        {
            if (_adapter == null) return;
            StatusLabel.Text = "⏹ Arrêt";
            await _adapter.DisconnectAsync();
            DisableControls();
        }

        // ── Onglets ──
        private void OnTabVariablesClicked(object? s, EventArgs e) { _currentTab = TabKind.Variables; UpdateTabStyles(); RefreshView(); }
        private void OnTabWatchClicked(object? s, EventArgs e) { _currentTab = TabKind.Watch; UpdateTabStyles(); RefreshView(); }
        private void OnTabCallStackClicked(object? s, EventArgs e) { _currentTab = TabKind.CallStack; UpdateTabStyles(); _ = RenderCallStackAsync(); }
        private void OnTabBreakpointsClicked(object? s, EventArgs e) { _currentTab = TabKind.Breakpoints; UpdateTabStyles(); RefreshView(); }

        private void UpdateTabStyles()
        {
            var accent = (Color)Application.Current.Resources["Accent"];
            var side = (Color)Application.Current.Resources["BgSide"];
            var txt1 = (Color)Application.Current.Resources["Txt1"];

            TabVariablesBtn.BackgroundColor = _currentTab == TabKind.Variables ? accent : side;
            TabVariablesBtn.TextColor = _currentTab == TabKind.Variables ? Colors.White : txt1;
            TabWatchBtn.BackgroundColor = _currentTab == TabKind.Watch ? accent : side;
            TabWatchBtn.TextColor = _currentTab == TabKind.Watch ? Colors.White : txt1;
            TabCallStackBtn.BackgroundColor = _currentTab == TabKind.CallStack ? accent : side;
            TabCallStackBtn.TextColor = _currentTab == TabKind.CallStack ? Colors.White : txt1;
            TabBreakpointsBtn.BackgroundColor = _currentTab == TabKind.Breakpoints ? accent : side;
            TabBreakpointsBtn.TextColor = _currentTab == TabKind.Breakpoints ? Colors.White : txt1;
        }

        private void RefreshView()
        {
            ContentArea.Children.Clear();
            switch (_currentTab)
            {
                case TabKind.Watch: RenderWatches(); break;
                case TabKind.Breakpoints: RenderBreakpoints(); break;
                case TabKind.Variables: RenderVariablesPlaceholder(); break;
            }
        }

        private void RenderVariablesPlaceholder()
        {
            ContentArea.Children.Add(new Label
            {
                Text = "Variables affichées lors d'un arrêt sur breakpoint",
                FontSize = 11, TextColor = (Color)Application.Current.Resources["Txt2"],
                Padding = new Thickness(4)
            });
        }

        private async System.Threading.Tasks.Task RenderCallStackAsync()
        {
            if (_adapter == null) return;
            var frames = await _adapter.GetStackTraceAsync(_currentThreadId);
            ContentArea.Children.Clear();

            foreach (var frame in frames)
            {
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 6, Padding = new Thickness(4, 2)
                };
                row.Children.Add(new Label
                {
                    Text = $"#{frame.Id}", FontSize = 11,
                    TextColor = (Color)Application.Current.Resources["Accent"]
                });
                var label = new Label
                {
                    Text = $"{frame.Name} ({System.IO.Path.GetFileName(frame.FilePath ?? "")}:{frame.Line})",
                    FontSize = 11, TextColor = (Color)Application.Current.Resources["Txt1"]
                };
                Grid.SetColumn(label, 1);
                row.Children.Add(label);
                ContentArea.Children.Add(row);
            }
        }

        private void RenderWatches()
        {
            foreach (var w in _watches)
            {
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    ColumnSpacing = 6, Padding = new Thickness(4, 2)
                };
                row.Children.Add(new Label
                {
                    Text = w.Expression, FontSize = 11,
                    TextColor = (Color)Application.Current.Resources["Txt1"]
                });
                var valueLabel = new Label
                {
                    Text = w.Value ?? "…",
                    FontSize = 11,
                    TextColor = w.HasError
                        ? Color.FromArgb("#DC2626")
                        : (Color)Application.Current.Resources["Accent"]
                };
                Grid.SetColumn(valueLabel, 1);
                row.Children.Add(valueLabel);
                ContentArea.Children.Add(row);
            }
        }

        private void RenderBreakpoints()
        {
            if (_breakpointManager == null) return;
            foreach (var bp in _breakpointManager.GetAllBreakpoints())
            {
                var row = new Grid
                {
                    ColumnDefinitions = {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 6, Padding = new Thickness(4, 2)
                };

                var dot = new Border
                {
                    WidthRequest = 12, HeightRequest = 12,
                    StrokeShape = new Ellipse(),
                    BackgroundColor = bp.Enabled
                        ? (bp.Verified ? Color.FromArgb("#DC2626") : Color.FromArgb("#9CA3AF"))
                        : Color.FromArgb("#6B7280")
                };

                var info = new VerticalStackLayout { Spacing = 2 };
                info.Children.Add(new Label
                {
                    Text = $"{System.IO.Path.GetFileName(bp.FilePath)}:{bp.Line}",
                    FontSize = 11, TextColor = (Color)Application.Current.Resources["Txt1"]
                });
                if (!string.IsNullOrWhiteSpace(bp.Condition))
                    info.Children.Add(new Label
                    {
                        Text = $"if {bp.Condition}",
                        FontSize = 10, FontAttributes = FontAttributes.Italic,
                        TextColor = (Color)Application.Current.Resources["Txt2"]
                    });

                var toggleBtn = new Button
                {
                    Text = bp.Enabled ? "ON" : "OFF",
                    FontSize = 9, WidthRequest = 40,
                    BackgroundColor = bp.Enabled
                        ? (Color)Application.Current.Resources["Accent"]
                        : (Color)Application.Current.Resources["BgHover"],
                    TextColor = Colors.White
                };
                var bpId = bp.Id;
                toggleBtn.Clicked += (s, e) => _breakpointManager.ToggleBreakpoint(bpId);

                Grid.SetColumn(info, 1);
                Grid.SetColumn(toggleBtn, 2);
                row.Children.Add(dot);
                row.Children.Add(info);
                row.Children.Add(toggleBtn);
                ContentArea.Children.Add(row);
            }
        }

        private async void OnAddWatchClicked(object? s, EventArgs e)
        {
            var expr = WatchEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(expr) || _adapter == null) return;

            var watch = new WatchExpression { Expression = expr };
            _watches.Add(watch);
            WatchEntry.Text = string.Empty;

            var value = await _adapter.EvaluateAsync(expr);
            watch.Value = value ?? "<erreur>";
            watch.HasError = value == null;
            RefreshView();
        }

        // ── Handlers DAP ──
        private void OnDebugStopped(int threadId, string reason, int line)
        {
            _currentThreadId = threadId;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = $"⏸ {reason} (thread {threadId})";
                EnableControls();
                _ = RenderCallStackAsync();
            });
        }

        private void OnDebugTerminated()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "Session terminée";
                DisableControls();
            });
        }

        private void OnDebugOutput(string output)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[DAP] {output}");
            });
        }

        private void EnableControls() =>
            ContinueBtn.IsEnabled = StepOverBtn.IsEnabled =
            StepIntoBtn.IsEnabled = StepOutBtn.IsEnabled = StopBtn.IsEnabled = true;

        private void DisableControls() =>
            ContinueBtn.IsEnabled = StepOverBtn.IsEnabled =
            StepIntoBtn.IsEnabled = StepOutBtn.IsEnabled = StopBtn.IsEnabled = false;
    }
}
