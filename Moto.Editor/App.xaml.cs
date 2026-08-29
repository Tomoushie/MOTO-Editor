// App.xaml.cs
using Microsoft.Maui.Controls;

namespace Moto.Editor
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new MainPage();
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

#if WINDOWS
            window.HandlerChanged += (s, e) =>
            {
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
                {
                    // Masque la barre de titre native : le contenu s'étend dessous.
                    native.ExtendsContentIntoTitleBar = true;
                }
            };
#endif
            return window;
        }
    }
}
