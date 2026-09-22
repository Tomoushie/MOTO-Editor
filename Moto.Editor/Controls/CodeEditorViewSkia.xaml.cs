// Moto.Editor/Controls/CodeEditorViewSkia.xaml.cs
// Incrément 1 (rendu statique) du chantier "rendu direct SkiaSharp" —
// voir Docs/design/Cadrage-CodeEditor-SkiaSharp.md. Pas encore éditable :
// aucune saisie clavier/souris, pas de mini-map, pas de ghost text — ces
// parties viennent dans les incréments suivants. Généré par l'Orchestrator
// (qwen2.5-coder:latest), une correction manuelle après relecture :
// la propriété s'appelait "FontSize" au lieu de "FontSizeMode", ce qui
// aurait cassé le contrat public existant (SettingsApplier.cs appelle
// editor.FontSizeMode).
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Moto.Editor.Controls
{
    public partial class CodeEditorViewSkia : ContentView
    {
        public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(CodeEditorViewSkia), string.Empty, BindingMode.TwoWay, propertyChanged: OnTextChanged);

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSizeMode), typeof(double), typeof(CodeEditorViewSkia), 14.0, BindingMode.TwoWay, propertyChanged: OnFontSizeChanged);

        public double FontSizeMode
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public event EventHandler<string> EditorChanged;

        // Garde anti-boucle pour la synchro Text <-> HiddenInput.Text (incrément
        // 2a) : sans elle, écrire dans l'un déclenche l'autre qui réécrit dans le
        // premier, indéfiniment.
        private bool _syncInProgress;

        // Même regex que l'ancien CodeEditorView (JS) : les groupes NON reconnus
        // (espaces, ponctuation, identifiants) ne sont volontairement PAS
        // capturés ici -- ils sont dessinés tels quels entre deux correspondances,
        // jamais supprimés (contrairement au découpage par espaces d'origine, qui
        // avalait espaces ET ponctuation -- corrigé le 22/09, signalé par Tom :
        // le texte s'affichait collé sans le moindre espace).
        private static readonly Regex TokenRegex = new(
            "(//.*)|(\"[^\"]*\")|\\b(\\d+(?:\\.\\d+)?)\\b|\\b(public|private|protected|internal|static|void|string|int|bool|double|float|var|class|interface|namespace|using|return|if|else|for|foreach|while|switch|case|break|continue|new|async|await|true|false|null|this|get|set|readonly)\\b",
            RegexOptions.Compiled);

        public CodeEditorViewSkia()
        {
            InitializeComponent();
            Canvas.PaintSurface += OnPaintSurface;
            HiddenInput.TextChanged += OnHiddenInputTextChanged;
        }

        private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (CodeEditorViewSkia)bindable;
            if (!view._syncInProgress)
            {
                view._syncInProgress = true;
                string text = (string)newValue ?? string.Empty;
                if (view.HiddenInput.Text != text)
                    view.HiddenInput.Text = text;
                view._syncInProgress = false;
            }
            view.Canvas.InvalidateSurface();
        }

        // Incrément 2a : HiddenInput (Editor MAUI caché, voir CodeEditorViewSkia.xaml)
        // reçoit la vraie saisie clavier/IME. Ce gestionnaire répercute chaque frappe
        // vers la propriété publique Text -- via le même chemin (BindableProperty)
        // que tout appelant externe, donc EditorPaneView/le binding two-way voient
        // la frappe sans code spécifique de leur côté.
        private void OnHiddenInputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncInProgress)
                return;
            _syncInProgress = true;
            Text = e.NewTextValue ?? string.Empty;
            _syncInProgress = false;
            // Contrat public (voir Cadrage-CodeEditor-SkiaSharp.md §2) : "Levé à
            // chaque frappe" -- jamais câblé jusqu'ici (aucune saisie n'existait
            // avant 2a), d'où l'avertissement CS0067 vu depuis l'incrément 1.
            EditorChanged?.Invoke(this, Text);
        }

        private static void OnFontSizeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (CodeEditorViewSkia)bindable;
            view.HiddenInput.FontSize = (double)newValue;
            view.Canvas.InvalidateSurface();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var surface = e.Surface;
            var canvas = surface.Canvas;
            canvas.Clear(SKColor.Parse("#1e2025"));

            if (string.IsNullOrEmpty(Text))
                return;

            var lines = Text.Split('\n');

            var lineNumberPaint = new SKPaint
            {
                Color = SKColor.Parse("#1a1c20"),
                Style = SKPaintStyle.Fill,
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal) ?? SKTypeface.FromFamilyName(null, SKFontStyle.Normal),
                TextSize = (float)FontSizeMode
            };

            var codePaint = new SKPaint
            {
                Color = SKColor.Parse("#dcdfe4"),
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal) ?? SKTypeface.FromFamilyName(null, SKFontStyle.Normal),
                TextSize = (float)FontSizeMode
            };

            // Le brouillon de l'Orchestrator dessinait le bandeau de gouttière
            // (DrawRect ci-dessous) mais n'y écrivait jamais le numéro -- gouttière
            // vide. Trouvé le 22/09 en relisant ce fichier, corrigé au passage.
            var gutterTextPaint = new SKPaint
            {
                Color = SKColor.Parse("#6b7280"),
                Typeface = lineNumberPaint.Typeface,
                TextSize = (float)FontSizeMode,
                TextAlign = SKTextAlign.Right
            };

            const float GutterWidth = 52f;
            const float TextStartX = GutterWidth + 8f;
            const float RightMargin = 8f;
            float lineHeight = lineNumberPaint.TextSize * 1.5f;

            // Retour à la ligne visuel (22/09, demandé par Tom : le texte ne
            // remplissait pas la largeur du panneau). Une ligne source peut
            // maintenant occuper plusieurs "lignes visuelles" successives ;
            // le numéro de ligne (gouttière) n'apparaît que sur la première.
            // Le +40 plancher évite une boucle si le panneau est réduit à rien.
            float maxX = Math.Max(TextStartX + 40f, e.Info.Width - RightMargin);

            int visualRow = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Mêmes segments qu'avant (voir TokenRegex) : un token reconnu,
                // ou un morceau brut entre deux tokens (espaces, ponctuation,
                // identifiants) -- jamais de caractère perdu. Construits en
                // liste ici (plutôt que dessinés au fil de l'eau comme avant)
                // pour pouvoir couper au bon endroit si la ligne déborde.
                var segments = new List<(string Text, SKColor Color)>();
                int pos = 0;
                foreach (Match m in TokenRegex.Matches(line))
                {
                    if (m.Index > pos)
                        segments.Add((line.Substring(pos, m.Index - pos), SKColor.Parse("#dcdfe4")));

                    string color = m.Groups[1].Success ? "#6a9955"   // commentaire //...
                                 : m.Groups[2].Success ? "#ce9178"   // chaîne "..."
                                 : m.Groups[3].Success ? "#b5cea8"   // nombre
                                 : "#569cd6";                        // mot-clé
                    segments.Add((m.Value, SKColor.Parse(color)));
                    pos = m.Index + m.Length;
                }
                if (pos < line.Length)
                    segments.Add((line.Substring(pos), SKColor.Parse("#dcdfe4")));

                float x = TextStartX;
                DrawGutterRow(canvas, lineNumberPaint, gutterTextPaint, visualRow, lineHeight, GutterWidth, i + 1);

                foreach (var (text, color) in segments)
                {
                    if (text.Length == 0)
                        continue;

                    codePaint.Color = color;
                    float width = codePaint.MeasureText(text);

                    // Le segment ne tient pas dans la largeur restante :
                    // nouvelle ligne visuelle -- sauf en tout début de ligne
                    // (sinon un seul token plus large que le panneau boucle
                    // sans fin sans jamais avancer).
                    if (x + width > maxX && x > TextStartX)
                    {
                        visualRow++;
                        x = TextStartX;
                        DrawGutterRow(canvas, lineNumberPaint, gutterTextPaint, visualRow, lineHeight, GutterWidth, lineNumber: null);
                    }

                    float y = visualRow * lineHeight + 40;
                    canvas.DrawText(text, x, y, codePaint);
                    x += width;
                }

                visualRow++;
            }
        }

        // lineNumber == null : ligne de continuation d'un retour à la ligne
        // visuel -- gouttière dessinée (fond continu) mais sans numéro.
        private static void DrawGutterRow(SKCanvas canvas, SKPaint gutterBgPaint, SKPaint gutterTextPaint, int visualRow, float lineHeight, float gutterWidth, int? lineNumber)
        {
            float top = visualRow * lineHeight + 20;
            canvas.DrawRect(0, top, gutterWidth, lineHeight, gutterBgPaint);
            if (lineNumber.HasValue)
                canvas.DrawText(lineNumber.Value.ToString(), gutterWidth - 8, top + gutterBgPaint.TextSize * 1.15f, gutterTextPaint);
        }

        public void GoToLine(int line)
        {
            // incrément futur : saisie/scroll
        }

        public void SetMinimapVisible(bool visible)
        {
            // incrément futur : mini-map
        }

        public void SetGhost(string suggestion)
        {
            // incrément futur : ghost text
        }

        public string GetSelectedText()
        {
            // Incrément 2a : HiddenInput porte maintenant une vraie sélection
            // (CursorPosition/SelectionLength, vérifiés existants sur MAUI 8.0.100
            // -- voir Cadrage-CodeEditor-SkiaSharp.md §10). Bornes défensives : ces
            // valeurs viennent d'un contrôle natif par plateforme, pas garanties
            // alignées au caractère près dans tous les cas.
            string text = HiddenInput.Text ?? string.Empty;
            int start = Math.Clamp(HiddenInput.CursorPosition, 0, text.Length);
            int length = Math.Clamp(HiddenInput.SelectionLength, 0, text.Length - start);
            return length > 0 ? text.Substring(start, length) : string.Empty;
        }
    }
}
