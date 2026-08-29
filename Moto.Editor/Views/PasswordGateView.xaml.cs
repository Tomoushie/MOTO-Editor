// Moto.Editor/Views/PasswordGateView.xaml.cs
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Security;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Écran de verrouillage affiché à l'ouverture d'un projet protégé.
    /// </summary>
    public partial class PasswordGateView : ContentView
    {
        private readonly ProjectLockEngine _lock = new ProjectLockEngine();
        private string _projectPath = string.Empty;

        /// <summary>Déclenché quand le mot de passe est correct.</summary>
        public event Action Unlocked;

        public PasswordGateView()
        {
            InitializeComponent();
        }

        public void Lock(string projectPath)
        {
            _projectPath = projectPath;
            IsVisible = true;
            PasswordEntry.Text = string.Empty;
            ErrorLabel.IsVisible = false;
            PasswordEntry.Focus();
        }

        private void OnUnlockClicked(object sender, EventArgs e)
        {
            if (_lock.Verify(_projectPath, PasswordEntry.Text))
            {
                IsVisible = false;
                Unlocked?.Invoke();
            }
            else
            {
                ErrorLabel.Text = "Mot de passe incorrect.";
                ErrorLabel.IsVisible = true;
            }
        }
    }
}
