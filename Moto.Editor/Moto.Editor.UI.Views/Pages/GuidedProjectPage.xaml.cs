// Pages/GuidedProjectPage.xaml.cs
using Microsoft.Maui.Controls;

namespace Moto.Editor.Pages
{
    /// <summary>
    /// Assistant de création guidée.
    /// Remplace GuidedProjectWizard WinForms.
    /// </summary>
    public partial class GuidedProjectPage : ContentPage
    {
        public GuidedProjectPage()
        {
            InitializeComponent();

            if (TemplatePicker.ItemsSource != null)
            {
                TemplatePicker.SelectedIndex = 0;
            }
        }

        private async void OnCreateClicked(object sender, EventArgs e)
        {
            var name = ProjectName.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                await DisplayAlert("MOTO", "Donne un nom à ton projet.", "OK");
                return;
            }

            var template = TemplatePicker.SelectedItem?.ToString() ?? "Projet vide";

            await DisplayAlert(
                "MOTO",
                $"Le projet '{name}' sera créé avec le modèle : {template}.\n\n" +
                "La génération réelle doit être déléguée à XENO-SSS∞ via le bridge.",
                "OK"
            );
        }
    }
}
