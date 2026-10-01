// Moto.Editor/Views/OutlinePanelView.xaml.cs
// ★ AJOUT (01/10, décision C item 3) : panneau Outline (vue symboles).
// Alimenté par MainPage à chaque changement de document actif : il extrait les
// symboles RÉELS du texte (Services/OutlineExtractor) et les affiche. Aucune
// dépendance DI, aucun contenu inventé.
//
// Réglages de la famille op_* applicables à cette vue :
//   • op_auto_reveal : surligne et défile vers le symbole correspondant au curseur
//     (position reçue de MainPage via SetCursorLine — la position du curseur est
//     RÉELLE, calculée par MainPage depuis la sélection du WebView, jamais inventée).
//   • op_auto_fold / op_indent_guides : sans point d'application dans une LISTE PLATE
//     (pas d'arbre à replier ni de guides à dessiner) — voir OutlineSettings.cs.
using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;
using Moto.Editor.Services;
using Moto.Editor.Settings;

namespace Moto.Editor.Views
{
    public partial class OutlinePanelView : ContentView
    {
        private readonly ObservableCollection<OutlineSymbol> _symbols = new();
        private bool _autoReveal = true; // défaut déclaré au catalogue (op_auto_reveal = true)
        private int _lastCursorLine;

        public OutlinePanelView()
        {
            InitializeComponent();
            SymbolsList.ItemsSource = _symbols;
        }

        /// <summary>
        /// Charge les symboles réels du fichier actif. Le langage est résolu par
        /// <see cref="Services.OutlineExtractor"/> d'après <c>path</c> (même classification
        /// que la coloration syntaxique). Liste vide si aucun motif n'est reconnu.
        /// </summary>
        public void Load(string? path, string? text)
        {
            _lastCursorLine = 0;
            _symbols.Clear();
            foreach (var symbol in OutlineExtractor.Extract(path, text))
                _symbols.Add(symbol);
        }

        /// <summary>Applique les réglages op_* qui concernent cette vue (op_auto_reveal).</summary>
        public void ApplySettings(SettingsEngine settings)
        {
            if (settings is null) return;
            _autoReveal = OutlineSettings.AutoReveal(settings);
            if (_autoReveal && _lastCursorLine > 0) HighlightAtLine(_lastCursorLine);
            else if (!_autoReveal) ClearHighlight();
        }

        /// <summary>
        /// ★ op_auto_reveal : position du curseur (numéro de ligne 1-based, déjà calculé
        /// par MainPage depuis la sélection du WebView). Surligne et défile vers le
        /// symbole qui contient cette ligne.
        /// </summary>
        public void SetCursorLine(int line)
        {
            _lastCursorLine = line;
            if (_autoReveal) HighlightAtLine(line);
        }

        /// <summary>Surligne le symbole dont la ligne est la plus proche ≤ <paramref name="line"/>.</summary>
        private void HighlightAtLine(int line)
        {
            OutlineSymbol? current = null;
            foreach (var symbol in _symbols)
            {
                if (symbol.Line <= line && (current is null || symbol.Line > current.Line))
                    current = symbol;
            }

            foreach (var symbol in _symbols)
                symbol.IsCurrent = ReferenceEquals(symbol, current);

            if (current is not null)
                SymbolsList.ScrollTo(current, position: ScrollToPosition.Center, animate: false);
        }

        private void ClearHighlight()
        {
            foreach (var symbol in _symbols)
                symbol.IsCurrent = false;
        }
    }
}
