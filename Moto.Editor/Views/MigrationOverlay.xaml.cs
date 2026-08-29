// Moto.Editor/Views/MigrationOverlay.xaml.cs
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views
{
    public partial class MigrationOverlay : ContentView
    {
        public MigrationOverlay()
        {
            InitializeComponent();
        }

        public async Task ShowAsync(int migratedKeys, string? backupPath)
        {
            if (migratedKeys <= 0)
                return;

            MessageLabel.Text = $"{migratedKeys} paramètre(s) migré(s).";

            if (!string.IsNullOrWhiteSpace(backupPath))
                MessageLabel.Text += $" Backup : {System.IO.Path.GetFileName(backupPath)}";

            IsVisible = true;
            await Task.Delay(TimeSpan.FromSeconds(6));
            IsVisible = false;
        }
    }
}
