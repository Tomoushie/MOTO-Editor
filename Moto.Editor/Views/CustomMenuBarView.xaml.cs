// Moto.Editor/Views/CustomMenuBarView.xaml.cs — AJOUTS
using Moto.Core.Updates;

namespace Moto.Editor.Views
{
    public partial class CustomMenuBarView : ContentView
    {
        private UpdateManager? _updateManager;
        private UpdateInfo? _pendingUpdate;

        public event Action? UpdateRequested;

        public CustomMenuBarView()
        {
            InitializeComponent();
        }

        public void SetUpdateManager(UpdateManager manager)
        {
            _updateManager = manager;
            _updateManager.UpdateAvailable += OnUpdateAvailable;
        }

        private void OnUpdateAvailable(UpdateInfo update)
        {
            _pendingUpdate = update;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateBadge.IsVisible = true;
                UpdateLabel.Text = $"v{update.Version}";
            });
        }

        private async void OnUpdateClicked(object? sender, EventArgs e)
        {
            if (_pendingUpdate == null) return;

            var confirmed = await Application.Current!.MainPage!.DisplayAlert(
                "Mise à jour disponible",
                $"Version {_pendingUpdate.Version} est disponible.\n\n" +
                $"{_pendingUpdate.Changelog}\n\n" +
                "Un redémarrage sera nécessaire.",
                "Télécharger",
                "Plus tard");

            if (confirmed)
            {
                UpdateRequested?.Invoke();
            }
        }
    }
}
