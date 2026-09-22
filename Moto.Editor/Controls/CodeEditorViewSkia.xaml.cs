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

            for (int i = 0; i < lines.Length; i++)
            {
                canvas.DrawRect(0, i * (lineNumberPaint.TextSize * 1.5f) + 20, 52, lineNumberPaint.TextSize * 1.5f, lineNumberPaint);

                float x = 60;
                var words = lines[i].Split(new[] { ' ', ',', '.', ';', '(', ')', '[', ']', '{', '}', ':' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (word.StartsWith("//"))
                    {
                        codePaint.Color = SKColor.Parse("#6a9955");
                        canvas.DrawText(word.Substring(2), x, i * (lineNumberPaint.TextSize * 1.5f) + 40, codePaint);
                        x += codePaint.MeasureText(word.Substring(2));
                    }
                    else if (word.StartsWith("\"") && word.EndsWith("\""))
                    {
                        codePaint.Color = SKColor.Parse("#ce9178");
                        canvas.DrawText(word, x, i * (lineNumberPaint.TextSize * 1.5f) + 40, codePaint);
                        x += codePaint.MeasureText(word);
                    }
                    else if (double.TryParse(word, out _))
                    {
                        codePaint.Color = SKColor.Parse("#b5cea8");
                        canvas.DrawText(word, x, i * (lineNumberPaint.TextSize * 1.5f) + 40, codePaint);
                        x += codePaint.MeasureText(word);
                    }
                    else if (new[] { "public", "private", "protected", "internal", "static", "void", "string", "int", "bool", "double", "float", "var", "class", "interface", "namespace", "using", "return", "if", "else", "for", "foreach", "while", "switch", "case", "break", "continue", "new", "async", "await", "true", "false", "null", "this", "get", "set", "readonly" }.Contains(word))
                    {
                        codePaint.Color = SKColor.Parse("#569cd6");
                        canvas.DrawText(word, x, i * (lineNumberPaint.TextSize * 1.5f) + 40, codePaint);
                        x += codePaint.MeasureText(word);
                    }
                    else
                    {
                        codePaint.Color = SKColor.Parse("#dcdfe4");
                        canvas.DrawText(word, x, i * (lineNumberPaint.TextSize * 1.5f) + 40, codePaint);
                        x += codePaint.MeasureText(word);
                    }
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
