// Moto.Editor/Controls/CodeEditorView.xaml.cs (v3)
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    public partial class CodeEditorView : ContentView
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(CodeEditorView),
                string.Empty, BindingMode.TwoWay,
                propertyChanged: OnTextPropertyChanged);

        public static readonly BindableProperty FontSizeProperty =
            BindableProperty.Create(nameof(FontSizeMode), typeof(double), typeof(CodeEditorView),
                14.0, propertyChanged: OnFontSizePropertyChanged);

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>Taille de police de l'éditeur (réglable via paramètres).</summary>
        public double FontSizeMode
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public event EventHandler<TextChangedEventArgs> TextChanged;

        public CodeEditorView()
        {
            InitializeComponent();
        }

        /// <summary>Navigation + surlignage d'une ligne (Navigation Assistant).</summary>
        public void GoToLine(int line)
        {
            if (string.IsNullOrEmpty(Text) || line < 1)
            {
                return;
            }

            var lines = Text.Split('\n');

            if (line > lines.Length)
            {
                return;
            }

            int index = 0;

            for (int i = 0; i < line - 1; i++)
            {
                index += lines[i].Length + 1;
            }

            Editor.Focus();
            Editor.CursorPosition = index;
            Editor.SelectionLength = lines[line - 1].Length;
        }

        /// <summary>Texte sélectionné (pour /selection du chat).</summary>
        public string GetSelectedText()
        {
            if (Editor.SelectionLength <= 0 || string.IsNullOrEmpty(Text))
            {
                return string.Empty;
            }

            var start = Editor.CursorPosition - Editor.SelectionLength;

            if (start < 0)
            {
                start = 0;
            }

            return Text.Substring(start, Editor.SelectionLength);
        }

        private static void OnFontSizePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is CodeEditorView view)
            {
                view.Editor.FontSize = (double)newValue;
            }
        }

        private static void OnTextPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is CodeEditorView view)
            {
                var newText = (string)newValue;

                if (view.Editor.Text != newText)
                {
                    view.Editor.Text = newText;
                }
            }
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Text != e.NewTextValue)
            {
                Text = e.NewTextValue;
            }

            TextChanged?.Invoke(sender, e);
        }
    }
}
