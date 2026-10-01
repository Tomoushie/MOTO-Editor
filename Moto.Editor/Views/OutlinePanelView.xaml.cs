// Moto.Editor/Views/OutlinePanelView.xaml.cs
// ★ AJOUT (01/10, décision C item 3) : panneau Outline (vue symboles).
// Alimenté par MainPage à chaque changement de document actif : il extrait les
// symboles RÉELS du texte (Services/OutlineExtractor) et les affiche. Aucune
// dépendance DI, aucun contenu inventé.
using System.Collections.ObjectModel;
using Moto.Editor.Controls;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    public partial class OutlinePanelView : ContentView
    {
        private readonly ObservableCollection<OutlineSymbol> _symbols = new();

        public OutlinePanelView()
        {
            InitializeComponent();
            SymbolsList.ItemsSource = _symbols;
        }

        /// <summary>
        /// Charge les symboles du fichier actif. Le langage vient de
        /// <see cref="CodeEditorView.LanguageOf"/> (mêmes codes que la coloration).
        /// </summary>
        public void Load(string? path, string? text)
        {
            _symbols.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;
            var language = CodeEditorView.LanguageOf(path);
            foreach (var symbol in OutlineExtractor.Extract(language, text))
                _symbols.Add(symbol);
        }
    }
}
