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
        }

        private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (CodeEditorViewSkia)bindable;
            view.Canvas.InvalidateSurface();
        }

        private static void OnFontSizeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (CodeEditorViewSkia)bindable;
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

            for (int i = 0; i < lines.Length; i++)
            {
                float rowTop = i * (lineNumberPaint.TextSize * 1.5f) + 20;
                canvas.DrawRect(0, rowTop, 52, lineNumberPaint.TextSize * 1.5f, lineNumberPaint);
                canvas.DrawText((i + 1).ToString(), 44, rowTop + lineNumberPaint.TextSize * 1.15f, gutterTextPaint);

                float x = 60;
                float y = i * (lineNumberPaint.TextSize * 1.5f) + 40;
                string line = lines[i];
                int pos = 0;
                foreach (Match m in TokenRegex.Matches(line))
                {
                    if (m.Index > pos)
                    {
                        // Segment NON coloré entre 2 correspondances : espaces,
                        // ponctuation, identifiants -- dessiné tel quel, jamais
                        // supprimé (voir commentaire sur TokenRegex plus haut).
                        string plain = line.Substring(pos, m.Index - pos);
                        codePaint.Color = SKColor.Parse("#dcdfe4");
                        canvas.DrawText(plain, x, y, codePaint);
                        x += codePaint.MeasureText(plain);
                    }

                    string color = m.Groups[1].Success ? "#6a9955"   // commentaire //...
                                 : m.Groups[2].Success ? "#ce9178"   // chaîne "..."
                                 : m.Groups[3].Success ? "#b5cea8"   // nombre
                                 : "#569cd6";                        // mot-clé
                    codePaint.Color = SKColor.Parse(color);
                    canvas.DrawText(m.Value, x, y, codePaint);
                    x += codePaint.MeasureText(m.Value);
                    pos = m.Index + m.Length;
                }

                if (pos < line.Length)
                {
                    string tail = line.Substring(pos);
                    codePaint.Color = SKColor.Parse("#dcdfe4");
                    canvas.DrawText(tail, x, y, codePaint);
                }
            }
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
            return string.Empty;
        }
    }
}
