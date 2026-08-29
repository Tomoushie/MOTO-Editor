// Moto.Core/AI/Internal/UiGeneratorEngine.cs
using System;
using System.Collections.Generic;
using System.Text;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// AI UI Generator (MAUI) : génère page XAML + code-behind + ViewModel
    /// + styles + navigation à partir d'une description simple.
    /// </summary>
    public class UiGeneratorEngine
    {
        private enum PageKind { Settings, List, Form, Detail }

        public List<AiFileChange> GeneratePage(string description)
        {
            var pageName = ExtractPageName(description);
            var kind = DetectKind(description);

            var changes = new List<AiFileChange>();

            changes.Add(new AiFileChange
            {
                Path = $"UI/Pages/{pageName}Page.xaml",
                Content = GenerateXaml(pageName, kind),
                Reason = $"Page {pageName} générée.",
                ChangeType = FileChangeType.Create
            });

            changes.Add(new AiFileChange
            {
                Path = $"UI/Pages/{pageName}Page.xaml.cs",
                Content = GenerateCodeBehind(pageName),
                Reason = "Code-behind généré.",
                ChangeType = FileChangeType.Create
            });

            changes.Add(new AiFileChange
            {
                Path = $"ViewModels/{pageName}ViewModel.cs",
                Content = GenerateViewModel(pageName, kind),
                Reason = "ViewModel généré avec bindings.",
                ChangeType = FileChangeType.Create
            });

            changes.Add(new AiFileChange
            {
                Path = $"Resources/Styles/{pageName}Styles.xaml",
                Content = GenerateStyles(pageName),
                Reason = "Styles de la page.",
                ChangeType = FileChangeType.Create
            });

            return changes;
        }

        private string ExtractPageName(string description)
        {
            var lower = description.ToLowerInvariant();

            if (lower.Contains("paramètre") || lower.Contains("parametre") || lower.Contains("setting"))
                return "Settings";

            if (lower.Contains("login") || lower.Contains("connexion"))
                return "Login";

            if (lower.Contains("profil") || lower.Contains("profile"))
                return "Profile";

            if (lower.Contains("joueur") || lower.Contains("player"))
                return "PlayerList";

            return "Custom";
        }

        private PageKind DetectKind(string description)
        {
            var lower = description.ToLowerInvariant();

            if (lower.Contains("paramètre") || lower.Contains("parametre") || lower.Contains("setting") || lower.Contains("option"))
                return PageKind.Settings;

            if (lower.Contains("liste") || lower.Contains("list"))
                return PageKind.List;

            if (lower.Contains("formulaire") || lower.Contains("form") || lower.Contains("login"))
                return PageKind.Form;

            return PageKind.Detail;
        }

        private string GenerateXaml(string name, PageKind kind)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            sb.AppendLine("<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"");
            sb.AppendLine("             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"");
            sb.AppendLine($"             x:Class=\"Moto.Editor.UI.Pages.{name}Page\"");
            sb.AppendLine($"             Title=\"{name}\">");
            sb.AppendLine();
            sb.AppendLine("    <ScrollView Padding=\"20\">");
            sb.AppendLine("        <VerticalStackLayout Spacing=\"12\">");

            switch (kind)
            {
                case PageKind.Settings:
                    sb.AppendLine("            <Label Text=\"Paramètres\" FontSize=\"24\" FontAttributes=\"Bold\" />");
                    sb.AppendLine("            <Switch IsToggled=\"{Binding DarkModeEnabled}\" />");
                    sb.AppendLine("            <Label Text=\"Mode sombre\" />");
                    sb.AppendLine("            <Entry Text=\"{Binding UserName}\" Placeholder=\"Nom d'utilisateur\" />");
                    break;

                case PageKind.List:
                    sb.AppendLine($"            <Label Text=\"Liste\" FontSize=\"24\" FontAttributes=\"Bold\" />");
                    sb.AppendLine("            <CollectionView ItemsSource=\"{Binding Items}\">");
                    sb.AppendLine("                <CollectionView.ItemTemplate>");
                    sb.AppendLine("                    <DataTemplate>");
                    sb.AppendLine("                        <Border StrokeShape=\"RoundRectangle 8\" Padding=\"10\" Margin=\"0,4\">");
                    sb.AppendLine("                            <Label Text=\"{Binding Title}\" />");
                    sb.AppendLine("                        </Border>");
                    sb.AppendLine("                    </DataTemplate>");
                    sb.AppendLine("                </CollectionView.ItemTemplate>");
                    sb.AppendLine("            </CollectionView>");
                    break;

                case PageKind.Form:
                    sb.AppendLine($"            <Label Text=\"{name}\" FontSize=\"24\" FontAttributes=\"Bold\" />");
                    sb.AppendLine("            <Entry Text=\"{Binding Field1}\" Placeholder=\"Champ 1\" />");
                    sb.AppendLine("            <Entry Text=\"{Binding Field2}\" Placeholder=\"Champ 2\" IsPassword=\"True\" />");
                    sb.AppendLine("            <Button Text=\"Valider\" Command=\"{Binding SubmitCommand}\" />");
                    break;

                default:
                    sb.AppendLine($"            <Label Text=\"{name}\" FontSize=\"24\" FontAttributes=\"Bold\" />");
                    sb.AppendLine("            <Label Text=\"{Binding Description}\" />");
                    break;
            }

            sb.AppendLine("        </VerticalStackLayout>");
            sb.AppendLine("    </ScrollView>");
            sb.AppendLine("</ContentPage>");

            return sb.ToString();
        }

        private string GenerateCodeBehind(string name)
        {
            return $@"using Microsoft.Maui.Controls;

namespace Moto.Editor.UI.Pages
{{
    /// <summary>
    /// Page {name} générée par MOTO AI.
    /// </summary>
    public partial class {name}Page : ContentPage
    {{
        public {name}Page()
        {{
            InitializeComponent();
            BindingContext = new ViewModels.{name}ViewModel();
        }}

        /// <summary>
        /// Navigation statique : await {name}Page.ShowAsync(Navigation);
        /// </summary>
        public static System.Threading.Tasks.Task ShowAsync(INavigation navigation)
        {{
            return navigation.PushAsync(new {name}Page());
        }}
    }}
}}";
        }

        private string GenerateViewModel(string name, PageKind kind)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System.Collections.ObjectModel;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Windows.Input;");
            sb.AppendLine();
            sb.AppendLine("namespace Moto.Editor.ViewModels");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>ViewModel de {name}Page, généré par MOTO AI.</summary>");
            sb.AppendLine($"    public class {name}ViewModel : INotifyPropertyChanged");
            sb.AppendLine("    {");

            if (kind == PageKind.Settings)
            {
                sb.AppendLine("        private bool _darkModeEnabled = true;");
                sb.AppendLine("        private string _userName = string.Empty;");
                sb.AppendLine();
                sb.AppendLine("        public bool DarkModeEnabled { get => _darkModeEnabled; set { _darkModeEnabled = value; OnPropertyChanged(); } }");
                sb.AppendLine("        public string UserName { get => _userName; set { _userName = value; OnPropertyChanged(); } }");
            }
            else if (kind == PageKind.List)
            {
                sb.AppendLine("        public ObservableCollection<ItemViewModel> Items { get; } = new ObservableCollection<ItemViewModel>();");
                sb.AppendLine();
                sb.AppendLine("        public class ItemViewModel { public string Title { get; set; } = string.Empty; }");
            }
            else
            {
                sb.AppendLine("        private string _field1 = string.Empty;");
                sb.AppendLine("        private string _field2 = string.Empty;");
                sb.AppendLine();
                sb.AppendLine("        public string Field1 { get => _field1; set { _field1 = value; OnPropertyChanged(); } }");
                sb.AppendLine("        public string Field2 { get => _field2; set { _field2 = value; OnPropertyChanged(); } }");
                sb.AppendLine();
                sb.AppendLine("        public ICommand SubmitCommand { get; }");
            }

            sb.AppendLine();
            sb.AppendLine("        public event PropertyChangedEventHandler PropertyChanged;");
            sb.AppendLine();
            sb.AppendLine("        protected void OnPropertyChanged([CallerMemberName] string name = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateStyles(string name)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<ResourceDictionary xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">

    <!-- Styles générés par MOTO AI pour {name}Page -->
    <Style x:Key=""{name}PrimaryButton"" TargetType=""Button"">
        <Setter Property=""BackgroundColor"" Value=""#0078CC"" />
        <Setter Property=""TextColor"" Value=""White"" />
        <Setter Property=""CornerRadius"" Value=""8"" />
    </Style>

    <Style x:Key=""{name}Card"" TargetType=""Border"">
        <Setter Property=""StrokeShape"" Value=""RoundRectangle 12"" />
        <Setter Property=""BackgroundColor"" Value=""#1A1B1F"" />
    </Style>

</ResourceDictionary>";
        }
    }
}
