// CustomMenuBarView.xaml.cs
// Barre de titre personnalisée (drag zone + min/max/close). Non utilisée par la
// page principale actuelle (Moto.Editor.MainPage) mais gardée fonctionnelle.
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    public partial class CustomMenuBarView : ContentView
    {
        private Microsoft.UI.Xaml.Window? NativeWindow =>
            Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window
                : null;

        public CustomMenuBarView()
        {
            InitializeComponent();

            BtnMin.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(OnMinClicked) });
            BtnMax.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(OnMaxClicked) });
            BtnClose.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(OnCloseClicked) });
        }

        private void OnMinClicked()
        {
#if WINDOWS
            if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
                p.Minimize();
#endif
        }

        private void OnMaxClicked()
        {
#if WINDOWS
            if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
            {
                if (p.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized) p.Restore();
                else p.Maximize();
            }
#endif
        }

        private void OnCloseClicked()
        {
#if WINDOWS
            NativeWindow?.Close();
#endif
        }
    }
}
