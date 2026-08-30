// Moto.Editor/Platforms/Windows/App.xaml.cs
// Voir le commentaire dans App.xaml : classe manquante qui, une fois ajoutée,
// donne enfin au SDK MAUI quelque chose de réel à instancier au démarrage sur
// Windows (au lieu du Main() vide généré en son absence).
using Microsoft.UI.Xaml;

namespace Moto.Editor.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        public App()
        {
            InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
