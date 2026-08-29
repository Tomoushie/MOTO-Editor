// Moto.Editor/Views/ConditionalBreakpointDialog.xaml.cs
// Dialogue complet pour les breakpoints conditionnels DAP.
using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Debug;

namespace Moto.Editor.Views
{
    public partial class ConditionalBreakpointDialog : ContentView
    {
        private BreakpointManager? _breakpointManager;
        private NetcoredbgAdapter? _debugAdapter;
        private string? _currentFilePath;
        private int _currentLine;
        private int? _editingBreakpointId;

        public event Action? BreakpointUpdated;

        public ConditionalBreakpointDialog()
        {
            InitializeComponent();
        }

        public void SetServices(BreakpointManager manager, NetcoredbgAdapter? adapter)
        {
            _breakpointManager = manager;
            _debugAdapter = adapter;
        }

        /// <summary>
        /// Ouvre le dialogue pour créer/modifier un breakpoint.
        /// </summary>
        public void Open(string filePath, int line, BreakpointInfo? existing = null)
        {
            _currentFilePath = filePath;
            _currentLine = line;
            _editingBreakpointId = existing?.Id;

            LineLabel.Text = $"📍 {System.IO.Path.GetFileName(filePath)} : ligne {line}";

            if (existing != null)
            {
                ConditionEntry.Text = existing.Condition;
                HitCountEntry.Text = existing.HitCount.ToString();
                EnabledSwitch.IsToggled = existing.Enabled;
                SaveButton.Text = "💾 Modifier";
            }
            else
            {
                ConditionEntry.Text = "";
                HitCountEntry.Text = "0";
                EnabledSwitch.IsToggled = true;
                SaveButton.Text = "➕ Ajouter";
            }

            IsVisible = true;
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            if (_breakpointManager == null || _currentFilePath == null) return;

            var condition = ConditionEntry.Text?.Trim();
            var enabled = EnabledSwitch.IsToggled;

            if (_editingBreakpointId.HasValue)
            {
                // Modifier le breakpoint existant
                _breakpointManager.ToggleBreakpoint(_editingBreakpointId.Value);
            }
            else
            {
                // Créer un nouveau breakpoint
                var bp = _breakpointManager.AddBreakpoint(_currentFilePath, _currentLine, condition);

                // Synchroniser avec le debugger si disponible
                if (_debugAdapter != null)
                {
                    try
                    {
                        var result = await _debugAdapter.SetBreakpointsAsync(
                            _currentFilePath, new[] { _currentLine });

                        if (result.Count > 0)
                        {
                            _breakpointManager.SetVerified(bp.Id, result[0].Verified);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DAP] Erreur sync : {ex.Message}");
                    }
                }
            }

            BreakpointUpdated?.Invoke();
            IsVisible = false;
        }

        private void OnDeleteClicked(object? sender, EventArgs e)
        {
            if (_breakpointManager == null || !_editingBreakpointId.HasValue) return;

            _breakpointManager.RemoveBreakpoint(_editingBreakpointId.Value);
            BreakpointUpdated?.Invoke();
            IsVisible = false;
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
        }
    }
}
