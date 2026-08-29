// Moto.Editor/Views/RemoteConnectView.xaml.cs
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Remote;

namespace Moto.Editor.Views
{
    public partial class RemoteConnectView : ContentView
    {
        public event Action<RemoteKind, string, int, string, string> ConnectRequested;

        public RemoteConnectView()
        {
            InitializeComponent();
            KindPicker.SelectedIndex = 0;
        }

        private void OnConnectClicked(object s, EventArgs e)
        {
            var kind = KindPicker.SelectedIndex switch
            {
                1 => RemoteKind.Ssh,
                2 => RemoteKind.RagServer,
                3 => RemoteKind.VsCodeServer,
                _ => RemoteKind.WebSocket
            };

            if (!int.TryParse(PortEntry.Text, out var port)) port = 8080;

            ConnectRequested?.Invoke(
                kind,
                HostEntry.Text ?? "",
                port,
                UserEntry.Text ?? "",
                TokenEntry.Text ?? "");
        }

        public void ShowStatus(string msg) => StatusLabel.Text = msg;
    }
}
